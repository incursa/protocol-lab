// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

internal sealed record DockerMetricsCaptureResult(
    LoadToolRun Run,
    string? ContainerId,
    DockerContainerMetricsSummary? Summary,
    IReadOnlyList<string> Warnings,
    Dictionary<string, string> Artifacts);

internal sealed record DockerMetricsSamplingResult(
    string? ContainerId,
    DockerContainerMetricsSummary Summary,
    IReadOnlyList<string> Warnings,
    Dictionary<string, string> Artifacts,
    bool SamplerStopped);

internal static partial class DockerContainerMetricsCapture
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static async Task<DockerMetricsCaptureResult> CaptureWhileRunningAsync(
        string docker,
        string containerName,
        ArtifactPaths paths,
        TimeSpan interval,
        Func<Task<LoadToolRun>> runAsync)
    {
        Directory.CreateDirectory(paths.CellDirectory);
        await File.WriteAllTextAsync(paths.LoadToolDockerStatsRawTxt, "");
        await File.WriteAllTextAsync(paths.LoadToolDockerStatsJsonl, "");
        await File.WriteAllTextAsync(paths.LoadToolDockerMetricsSummaryJson, "");

        using var cts = new CancellationTokenSource();
        var runTask = runAsync();

        var sampler = CaptureUntilCanceledAsync(
            docker,
            containerName,
            paths.LoadToolDockerStatsRawTxt,
            paths.LoadToolDockerStatsJsonl,
            paths.LoadToolDockerMetricsSummaryJson,
            interval,
            cts.Token,
            loadToolArtifacts: true);
        var run = await runTask;
        cts.Cancel();

        var sample = await sampler;
        var warnings = sample.Warnings;
        return new DockerMetricsCaptureResult(
            run,
            sample.ContainerId,
            sample.Summary,
            warnings,
            sample.Artifacts);
    }

    public static (string Status, IReadOnlyList<string> Warnings) AssessSaturation(
        DockerContainerMetricsSummary? summary,
        DockerResourceLimits? requested,
        DockerResourceLimits? effective)
    {
        if (summary is null || summary.Samples.Count == 0)
        {
            return (LoadGeneratorSaturationStatuses.Unknown, [BenchmarkEvidenceReasons.LoadGeneratorMetricsMissing]);
        }

        var warnings = new List<string>();
        var possible = false;
        var unknown = false;

        if (summary.Samples.Count == 1)
        {
            warnings.Add(BenchmarkEvidenceReasons.LoadGeneratorSingleSample);
            unknown = true;
        }

        if (!summary.CpuMaxPercent.HasValue)
        {
            warnings.Add(BenchmarkEvidenceReasons.LoadGeneratorCpuUnknown);
            unknown = true;
        }
        else
        {
            var cpuCapacity = TryParseDouble(effective?.Cpus) ?? TryParseDouble(requested?.Cpus) ?? 1d;
            var highThreshold = Math.Max(85d, cpuCapacity * 100d * 0.85d);
            if (summary.CpuMaxPercent.Value >= highThreshold)
            {
                warnings.Add(BenchmarkEvidenceReasons.LoadGeneratorCpuHigh);
                possible = true;
            }
        }

        if (summary.MemoryMaxPercent is >= 85d)
        {
            warnings.Add(BenchmarkEvidenceReasons.LoadGeneratorMemoryHigh);
            possible = true;
        }

        if (summary.ParseWarnings.Count > 0 ||
            summary.Samples.Any(static sample => !sample.CpuPercent.HasValue || !sample.MemoryUsageBytes.HasValue))
        {
            warnings.Add(BenchmarkEvidenceReasons.LoadGeneratorMetricsPartial);
        }

        if (summary.NetworkRxBytesDelta.HasValue || summary.NetworkTxBytesDelta.HasValue)
        {
            warnings.Add("load-generator-network-observed-local-docker-traffic");
        }

        if (possible)
        {
            warnings.Add(BenchmarkEvidenceReasons.LoadGeneratorSaturationPossible);
            return (LoadGeneratorSaturationStatuses.Possible, warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }

        if (unknown)
        {
            warnings.Add(BenchmarkEvidenceReasons.LoadGeneratorSaturationUnknown);
            return (LoadGeneratorSaturationStatuses.Unknown, warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }

        warnings.Add(BenchmarkEvidenceReasons.LoadGeneratorSaturationNotDetected);
        return (LoadGeneratorSaturationStatuses.NotDetected, warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public static async Task<DockerMetricsSamplingResult> CaptureUntilCanceledAsync(
        string docker,
        string containerName,
        string rawPath,
        string jsonlPath,
        string summaryPath,
        TimeSpan interval,
        CancellationToken cancellationToken,
        bool loadToolArtifacts = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(rawPath)!);
        await File.WriteAllTextAsync(rawPath, "", cancellationToken);
        await File.WriteAllTextAsync(jsonlPath, "", cancellationToken);
        await File.WriteAllTextAsync(summaryPath, "", cancellationToken);

        var warnings = new List<string>();
        var samples = new List<DockerContainerMetricSample>();
        var collectionStart = DateTimeOffset.UtcNow;

        try
        {
            await WaitForContainerAsync(docker, containerName, TimeSpan.FromSeconds(5), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            warnings.Add(BenchmarkEvidenceReasons.ContainerExitedTooQuickly);
        }

        var samplerStopped = true;
        try
        {
            await SampleUntilCanceledAsync(docker, containerName, rawPath, jsonlPath, interval, samples, warnings, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            samplerStopped = false;
            warnings.Add($"docker-stats-sampler-stop-failed: {ex.Message}");
        }

        var collectionEnd = DateTimeOffset.UtcNow;
        var summary = BuildSummary(samples, collectionStart, collectionEnd, warnings);
        if (summary.Samples.Count == 0)
        {
            warnings.Add(loadToolArtifacts
                ? BenchmarkEvidenceReasons.LoadGeneratorMetricsMissing
                : BenchmarkEvidenceReasons.TargetContainerMetricsMissing);
        }

        await File.WriteAllTextAsync(summaryPath, ResultJson.Serialize(summary), CancellationToken.None);
        var artifacts = loadToolArtifacts
            ? new Dictionary<string, string>
            {
                ["loadToolDockerStatsRaw"] = rawPath,
                ["loadToolDockerStatsJsonl"] = jsonlPath,
                ["loadToolDockerMetricsSummary"] = summaryPath
            }
            : new Dictionary<string, string>
            {
                ["targetDockerStatsRaw"] = rawPath,
                ["targetDockerStatsJsonl"] = jsonlPath,
                ["targetDockerMetricsSummary"] = summaryPath
            };

        return new DockerMetricsSamplingResult(
            samples.LastOrDefault()?.ContainerId,
            summary,
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            artifacts,
            samplerStopped);
    }

    public static TargetDockerMetricsSummary ToTargetSummary(DockerContainerMetricsSummary summary)
    {
        return new TargetDockerMetricsSummary
        {
            Samples = summary.Samples,
            CollectionStartUtc = summary.CollectionStartUtc,
            CollectionEndUtc = summary.CollectionEndUtc,
            CpuMeanPercent = summary.CpuMeanPercent,
            CpuMaxPercent = summary.CpuMaxPercent,
            MemoryMeanBytes = summary.MemoryMeanBytes,
            MemoryMaxBytes = summary.MemoryMaxBytes,
            MemoryLimitBytes = summary.MemoryLimitBytes,
            MemoryMaxPercent = summary.MemoryMaxPercent,
            NetworkRxBytesDelta = summary.NetworkRxBytesDelta,
            NetworkTxBytesDelta = summary.NetworkTxBytesDelta,
            BlockReadBytesDelta = summary.BlockReadBytesDelta,
            BlockWriteBytesDelta = summary.BlockWriteBytesDelta,
            PidsMax = summary.PidsMax,
            ParseWarnings = summary.ParseWarnings
        };
    }

    public static (string Status, IReadOnlyList<string> Warnings) AssessTargetSaturation(
        TargetDockerMetricsSummary? summary,
        DockerResourceLimits? requested,
        DockerResourceLimits? effective)
    {
        if (summary is null || summary.Samples.Count == 0)
        {
            return (TargetSaturationStatuses.Unknown, [BenchmarkEvidenceReasons.TargetContainerMetricsMissing]);
        }

        var warnings = new List<string>();
        var possible = false;
        var unknown = false;

        if (summary.Samples.Count == 1)
        {
            warnings.Add(BenchmarkEvidenceReasons.TargetContainerSingleSample);
            unknown = true;
        }

        if (!summary.CpuMaxPercent.HasValue)
        {
            warnings.Add(BenchmarkEvidenceReasons.TargetContainerCpuUnknown);
            unknown = true;
        }
        else
        {
            var cpuCapacity = TryParseDouble(effective?.Cpus) ?? TryParseDouble(requested?.Cpus) ?? 1d;
            var highThreshold = Math.Max(85d, cpuCapacity * 100d * 0.85d);
            if (summary.CpuMaxPercent.Value >= highThreshold)
            {
                warnings.Add(BenchmarkEvidenceReasons.TargetContainerCpuHigh);
                possible = true;
            }
        }

        if (summary.MemoryMaxPercent is >= 85d)
        {
            warnings.Add(BenchmarkEvidenceReasons.TargetContainerMemoryHigh);
            possible = true;
        }

        if (summary.ParseWarnings.Count > 0 ||
            summary.Samples.Any(static sample => !sample.CpuPercent.HasValue || !sample.MemoryUsageBytes.HasValue))
        {
            warnings.Add(BenchmarkEvidenceReasons.TargetContainerMetricsPartial);
        }

        if (summary.NetworkRxBytesDelta.HasValue || summary.NetworkTxBytesDelta.HasValue)
        {
            warnings.Add(BenchmarkEvidenceReasons.TargetContainerNetworkHigh);
        }

        if (possible)
        {
            warnings.Add(BenchmarkEvidenceReasons.TargetContainerSaturationPossible);
            return (TargetSaturationStatuses.Possible, warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }

        if (unknown)
        {
            warnings.Add(BenchmarkEvidenceReasons.TargetContainerSaturationUnknown);
            return (TargetSaturationStatuses.Unknown, warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }

        warnings.Add(BenchmarkEvidenceReasons.TargetContainerSaturationNotDetected);
        return (TargetSaturationStatuses.NotDetected, warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    internal static DockerContainerMetricSample? ParseDockerStatsJsonLine(string line, DateTimeOffset timestampUtc, ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var (memoryUsage, memoryLimit) = ParsePairBytes(GetString(root, "MemUsage"), warnings, "memory");
            var (networkRx, networkTx) = ParsePairBytes(GetString(root, "NetIO"), warnings, "network");
            var (blockRead, blockWrite) = ParsePairBytes(GetString(root, "BlockIO"), warnings, "block");

            return new DockerContainerMetricSample
            {
                TimestampUtc = timestampUtc,
                ContainerId = GetString(root, "ID") ?? GetString(root, "Container"),
                ContainerName = GetString(root, "Name") ?? GetString(root, "Name"),
                CpuPercent = ParsePercent(GetString(root, "CPUPerc"), warnings, "cpu"),
                MemoryUsageBytes = memoryUsage,
                MemoryLimitBytes = memoryLimit,
                MemoryPercent = ParsePercent(GetString(root, "MemPerc"), warnings, "memory-percent"),
                NetworkRxBytes = networkRx,
                NetworkTxBytes = networkTx,
                BlockReadBytes = blockRead,
                BlockWriteBytes = blockWrite,
                PidsCurrent = ParseInt(GetString(root, "PIDs"), warnings, "pids")
            };
        }
        catch (JsonException ex)
        {
            warnings.Add($"{BenchmarkEvidenceReasons.MetricsParserFailed}: {ex.Message}");
            return null;
        }
    }

    internal static long? ParseByteSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = ByteSizeRegex().Match(value.Trim());
        if (!match.Success ||
            !double.TryParse(match.Groups["number"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        var unit = match.Groups["unit"].Value.ToUpperInvariant();
        var multiplier = unit switch
        {
            "" or "B" => 1d,
            "KB" => 1_000d,
            "MB" => 1_000_000d,
            "GB" => 1_000_000_000d,
            "TB" => 1_000_000_000_000d,
            "KIB" => 1024d,
            "MIB" => 1024d * 1024d,
            "GIB" => 1024d * 1024d * 1024d,
            "TIB" => 1024d * 1024d * 1024d * 1024d,
            _ => double.NaN
        };

        return double.IsNaN(multiplier) ? null : Convert.ToInt64(number * multiplier);
    }

    private static async Task SampleUntilCanceledAsync(
        string docker,
        string containerName,
        string rawPath,
        string jsonlPath,
        TimeSpan interval,
        List<DockerContainerMetricSample> samples,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var timestamp = DateTimeOffset.UtcNow;
            var stats = await RunProcessAsync(
                docker,
                ["stats", "--no-stream", "--format", "{{json .}}", containerName],
                cancellationToken);

            var output = string.IsNullOrWhiteSpace(stats.Stdout) ? stats.Stderr : stats.Stdout;
            if (!string.IsNullOrWhiteSpace(output))
            {
                await File.AppendAllTextAsync(rawPath, output.TrimEnd() + Environment.NewLine, cancellationToken);
            }

            if (stats.ExitCode == 0)
            {
                foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var sampleWarnings = new List<string>();
                    var sample = ParseDockerStatsJsonLine(line, timestamp, sampleWarnings);
                    warnings.AddRange(sampleWarnings);
                    if (sample is not null)
                    {
                        samples.Add(sample);
                        await File.AppendAllTextAsync(jsonlPath, JsonSerializer.Serialize(sample, JsonOptions) + Environment.NewLine, cancellationToken);
                    }
                }
            }
            else if (samples.Count == 0)
            {
                warnings.Add($"{BenchmarkEvidenceReasons.DockerStatsUnavailable}: {FirstLine(output) ?? "docker stats returned no output"}");
            }

            try
            {
                await Task.Delay(interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static async Task WaitForContainerAsync(string docker, string containerName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            var inspect = await RunProcessAsync(docker, ["inspect", "--format", "{{.Id}}", containerName], cancellationToken);
            if (inspect.ExitCode == 0 && !string.IsNullOrWhiteSpace(inspect.Stdout))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private static DockerContainerMetricsSummary BuildSummary(
        IReadOnlyList<DockerContainerMetricSample> samples,
        DateTimeOffset collectionStart,
        DateTimeOffset collectionEnd,
        IReadOnlyList<string> warnings)
    {
        var cpuSamples = samples.Select(static sample => sample.CpuPercent).Where(static value => value.HasValue).Select(static value => value!.Value).ToArray();
        var memorySamples = samples.Select(static sample => sample.MemoryUsageBytes).Where(static value => value.HasValue).Select(static value => value!.Value).ToArray();
        var memoryPercentSamples = samples.Select(static sample => sample.MemoryPercent).Where(static value => value.HasValue).Select(static value => value!.Value).ToArray();
        var pidsSamples = samples.Select(static sample => sample.PidsCurrent).Where(static value => value.HasValue).Select(static value => value!.Value).ToArray();

        return new DockerContainerMetricsSummary
        {
            Samples = samples.ToArray(),
            CollectionStartUtc = collectionStart,
            CollectionEndUtc = collectionEnd,
            CpuMeanPercent = cpuSamples.Length == 0 ? null : cpuSamples.Average(),
            CpuMaxPercent = cpuSamples.Length == 0 ? null : cpuSamples.Max(),
            MemoryMeanBytes = memorySamples.Length == 0 ? null : Convert.ToInt64(memorySamples.Average()),
            MemoryMaxBytes = memorySamples.Length == 0 ? null : memorySamples.Max(),
            MemoryLimitBytes = samples.Select(static sample => sample.MemoryLimitBytes).FirstOrDefault(static value => value.HasValue),
            MemoryMaxPercent = memoryPercentSamples.Length == 0 ? null : memoryPercentSamples.Max(),
            NetworkRxBytesDelta = Delta(samples, static sample => sample.NetworkRxBytes),
            NetworkTxBytesDelta = Delta(samples, static sample => sample.NetworkTxBytes),
            BlockReadBytesDelta = Delta(samples, static sample => sample.BlockReadBytes),
            BlockWriteBytesDelta = Delta(samples, static sample => sample.BlockWriteBytes),
            PidsMax = pidsSamples.Length == 0 ? null : pidsSamples.Max(),
            ParseWarnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static long? Delta(IReadOnlyList<DockerContainerMetricSample> samples, Func<DockerContainerMetricSample, long?> selector)
    {
        var values = samples.Select(selector).Where(static value => value.HasValue).Select(static value => value!.Value).ToArray();
        return values.Length < 2 ? null : Math.Max(0, values[^1] - values[0]);
    }

    private static (long? Left, long? Right) ParsePairBytes(string? value, ICollection<string> warnings, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            warnings.Add($"docker-stats-{field}-missing");
            return (null, null);
        }

        var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            warnings.Add($"docker-stats-{field}-parse-failed");
            return (ParseByteSize(value), null);
        }

        return (ParseByteSize(parts[0]), ParseByteSize(parts[1]));
    }

    private static double? ParsePercent(string? value, ICollection<string> warnings, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            warnings.Add($"docker-stats-{field}-missing");
            return null;
        }

        var text = value.Trim().TrimEnd('%');
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        warnings.Add($"docker-stats-{field}-parse-failed");
        return null;
    }

    private static int? ParseInt(string? value, ICollection<string> warnings, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        warnings.Add($"docker-stats-{field}-parse-failed");
        return null;
    }

    private static double? TryParseDouble(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static async Task<LoadToolRun> RunProcessAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{executable}'.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new LoadToolRun(process.ExitCode, await stdout, await stderr);
    }

    private static string? FirstLine(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
    }

    [GeneratedRegex(@"^(?<number>[\d.]+)\s*(?<unit>[a-zA-Z]+)?$")]
    private static partial Regex ByteSizeRegex();
}
