// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

internal sealed record RuntimeCounterCaptureOptions(
    bool Enabled,
    string Tool,
    int RefreshIntervalSeconds,
    string Format,
    string WorkingDirectory);

internal sealed record CounterToolStatus(
    bool Available,
    string Tool,
    string? ExecutablePath,
    IReadOnlyList<string> PrefixArguments,
    string? Version,
    IReadOnlyList<string> Warnings);

internal sealed record RuntimeCounterCaptureResult(
    bool CountersAvailable,
    string Status,
    RuntimeCounterSummary? Summary,
    Dictionary<string, string> Artifacts,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);

internal sealed class RuntimeCounterSession
{
    private readonly Process? process;
    private readonly DateTimeOffset startTimeUtc;
    private readonly string rawPath;
    private readonly Task<string>? stdoutTask;
    private readonly Task<string>? stderrTask;
    private readonly ArtifactPaths paths;
    private readonly Dictionary<string, string> artifacts;
    private readonly List<string> warnings;
    private readonly List<string> errors;
    private readonly bool canCollect;
    private readonly string initialStatus;

    private RuntimeCounterSession(
        Process? process,
        DateTimeOffset startTimeUtc,
        string rawPath,
        Task<string>? stdoutTask,
        Task<string>? stderrTask,
        ArtifactPaths paths,
        Dictionary<string, string> artifacts,
        List<string> warnings,
        List<string> errors,
        bool canCollect,
        string initialStatus)
    {
        this.process = process;
        this.startTimeUtc = startTimeUtc;
        this.rawPath = rawPath;
        this.stdoutTask = stdoutTask;
        this.stderrTask = stderrTask;
        this.paths = paths;
        this.artifacts = artifacts;
        this.warnings = warnings;
        this.errors = errors;
        this.canCollect = canCollect;
        this.initialStatus = initialStatus;
    }

    public static RuntimeCounterSession Disabled(ArtifactPaths paths)
    {
        return new RuntimeCounterSession(
            null,
            DateTimeOffset.UtcNow,
            paths.CountersRawJson,
            null,
            null,
            paths,
            BuildArtifacts(paths, "json"),
            [],
            [],
            canCollect: false,
            CounterCaptureStatuses.Disabled);
    }

    public static async Task<RuntimeCounterSession> StartAsync(
        RuntimeCounterCaptureOptions options,
        DiagnosticTarget target,
        ArtifactPaths paths,
        int collectionSeconds)
    {
        EnsureCounterFiles(paths);
        var artifacts = BuildArtifacts(paths, options.Format);
        if (!options.Enabled)
        {
            return Disabled(paths);
        }

        if (target.ResolvedProcessId is null)
        {
            var warnings = target.Warnings
                .Concat(["counter-capture-skipped: diagnostic target process was not resolved."])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var summary = new RuntimeCounterSummary
            {
                ParseWarnings = warnings
            };
            await File.WriteAllTextAsync(paths.CountersSummaryJson, ResultJson.Serialize(summary));
            return new RuntimeCounterSession(null, DateTimeOffset.UtcNow, artifacts["raw"], null, null, paths, artifacts, warnings, [], false, CounterCaptureStatuses.TargetUnresolved);
        }

        var tool = await DetectToolAsync(options.Tool, options.WorkingDirectory);
        if (!tool.Available || string.IsNullOrWhiteSpace(tool.ExecutablePath))
        {
            var warnings = tool.Warnings.Concat(["counter-capture-skipped: dotnet-counters is unavailable."]).ToList();
            var summary = new RuntimeCounterSummary
            {
                ParseWarnings = warnings
            };
            await File.WriteAllTextAsync(paths.CountersSummaryJson, ResultJson.Serialize(summary));
            await File.WriteAllTextAsync(paths.CountersStderr, string.Join(Environment.NewLine, warnings));
            return new RuntimeCounterSession(null, DateTimeOffset.UtcNow, artifacts["raw"], null, null, paths, artifacts, warnings, [], false, CounterCaptureStatuses.ToolUnavailable);
        }

        var format = NormalizeFormat(options.Format);
        var rawPath = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)
            ? paths.CountersRawCsv
            : paths.CountersRawJson;
        artifacts["raw"] = rawPath;

        var startInfo = new ProcessStartInfo(tool.ExecutablePath)
        {
            WorkingDirectory = options.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in tool.PrefixArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var argument in BuildCollectArguments(target.ResolvedProcessId.Value, options.RefreshIntervalSeconds, format, rawPath, collectionSeconds))
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start dotnet-counters.");
            return new RuntimeCounterSession(
                process,
                DateTimeOffset.UtcNow,
                rawPath,
                process.StandardOutput.ReadToEndAsync(),
                process.StandardError.ReadToEndAsync(),
                paths,
                artifacts,
                tool.Warnings.ToList(),
                [],
                canCollect: true,
                CounterCaptureStatuses.NotRun);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            var errors = new List<string> { $"counter-capture-failed: {ex.Message}" };
            await File.WriteAllTextAsync(paths.CountersStderr, string.Join(Environment.NewLine, errors));
            return new RuntimeCounterSession(null, DateTimeOffset.UtcNow, rawPath, null, null, paths, artifacts, tool.Warnings.ToList(), errors, false, CounterCaptureStatuses.Failed);
        }
    }

    public async Task<RuntimeCounterCaptureResult> StopAsync()
    {
        if (!canCollect || process is null)
        {
            return await CompleteAsync(initialStatus, DateTimeOffset.UtcNow, null);
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            warnings.Add("counter-capture-process-did-not-exit-after-load; collector was stopped.");
            try
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            catch (InvalidOperationException)
            {
            }
        }

        var endTime = DateTimeOffset.UtcNow;
        var stdout = stdoutTask is null ? "" : await stdoutTask;
        var stderr = stderrTask is null ? "" : await stderrTask;
        await File.WriteAllTextAsync(paths.CountersStdout, stdout);
        await File.WriteAllTextAsync(paths.CountersStderr, stderr);

        if (process.ExitCode != 0)
        {
            errors.Add($"counter-capture-failed: dotnet-counters exited with code {process.ExitCode.ToString(CultureInfo.InvariantCulture)}.");
        }

        var status = errors.Count == 0 && File.Exists(rawPath) && new FileInfo(rawPath).Length > 0
            ? CounterCaptureStatuses.Succeeded
            : CounterCaptureStatuses.Failed;
        if (status == CounterCaptureStatuses.Failed && !File.Exists(rawPath))
        {
            warnings.Add("counter-raw-output-missing.");
        }

        return await CompleteAsync(status, endTime, rawPath);
    }

    public static async Task<CounterToolStatus> DetectToolAsync(string tool, string? workingDirectory = null)
    {
        var command = ResolveToolCommand(tool);
        if (command is null)
        {
            var localTool = await TryDetectLocalToolAsync(tool, workingDirectory);
            if (localTool is not null)
            {
                return localTool;
            }

            return new CounterToolStatus(false, tool, null, [], null, [$"{tool} was not found on PATH, as an explicit file path, or as a repo-local dotnet tool."]);
        }

        return await DetectCommandAsync(tool, command, workingDirectory);
    }

    private async Task<RuntimeCounterCaptureResult> CompleteAsync(string status, DateTimeOffset endTimeUtc, string? rawCounterPath)
    {
        var summary = RuntimeCounterParser.Parse(rawCounterPath, startTimeUtc, endTimeUtc);
        summary = summary with
        {
            ParseWarnings = summary.ParseWarnings
                .Concat(warnings)
                .Concat(errors)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
        await File.WriteAllTextAsync(paths.CountersSummaryJson, ResultJson.Serialize(summary));

        return new RuntimeCounterCaptureResult(
            string.Equals(status, CounterCaptureStatuses.Succeeded, StringComparison.OrdinalIgnoreCase),
            status,
            summary,
            artifacts,
            warnings,
            errors);
    }

    private static IReadOnlyList<string> BuildCollectArguments(
        int processId,
        int refreshIntervalSeconds,
        string format,
        string outputPath,
        int collectionSeconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(1, collectionSeconds));
        return
        [
            "collect",
            "--process-id",
            processId.ToString(CultureInfo.InvariantCulture),
            "--refresh-interval",
            Math.Max(1, refreshIntervalSeconds).ToString(CultureInfo.InvariantCulture),
            "--counters",
            "System.Runtime",
            "--format",
            format,
            "--output",
            outputPath,
            "--duration",
            duration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
        ];
    }

    private static Dictionary<string, string> BuildArtifacts(ArtifactPaths paths, string format)
    {
        return new Dictionary<string, string>
        {
            ["stdout"] = paths.CountersStdout,
            ["stderr"] = paths.CountersStderr,
            ["raw"] = string.Equals(NormalizeFormat(format), "csv", StringComparison.OrdinalIgnoreCase) ? paths.CountersRawCsv : paths.CountersRawJson,
            ["summary"] = paths.CountersSummaryJson,
            ["diagnosticTarget"] = paths.DiagnosticTargetJson
        };
    }

    private static void EnsureCounterFiles(ArtifactPaths paths)
    {
        File.WriteAllText(paths.CountersStdout, "");
        File.WriteAllText(paths.CountersStderr, "");
        if (!File.Exists(paths.CountersRawJson))
        {
            File.WriteAllText(paths.CountersRawJson, "");
        }

        if (!File.Exists(paths.CountersRawCsv))
        {
            File.WriteAllText(paths.CountersRawCsv, "");
        }
    }

    private static string NormalizeFormat(string format)
    {
        return string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) ? "csv" : "json";
    }

    private static async Task<CounterToolStatus?> TryDetectLocalToolAsync(string tool, string? workingDirectory)
    {
        if (!string.Equals(tool, "dotnet-counters", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var dotnet = LoadToolInvoker.ResolveExecutable("dotnet");
        if (dotnet is null)
        {
            return null;
        }

        var command = new CounterToolCommand(dotnet, ["tool", "run", tool, "--"]);
        var status = await DetectCommandAsync(tool, command, workingDirectory);
        return status.Available
            ? status with
            {
                Warnings = status.Warnings
                    .Concat(["dotnet-counters resolved from repo-local dotnet tool manifest."])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            }
            : null;
    }

    private static async Task<CounterToolStatus> DetectCommandAsync(
        string tool,
        CounterToolCommand command,
        string? workingDirectory)
    {
        try
        {
            var startInfo = new ProcessStartInfo(command.ExecutablePath)
            {
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var argument in command.PrefixArguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            startInfo.ArgumentList.Add("--version");
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start {tool}.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = string.Join(Environment.NewLine, new[] { await stdoutTask, await stderrTask }.Where(line => !string.IsNullOrWhiteSpace(line))).Trim();
            return process.ExitCode == 0
                ? new CounterToolStatus(true, tool, command.ExecutablePath, command.PrefixArguments, output, [])
                : new CounterToolStatus(false, tool, command.ExecutablePath, command.PrefixArguments, output, [$"{tool} --version exited with code {process.ExitCode.ToString(CultureInfo.InvariantCulture)}."]);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new CounterToolStatus(false, tool, command.ExecutablePath, command.PrefixArguments, null, [$"{tool} availability check failed: {ex.Message}"]);
        }
    }

    private static CounterToolCommand? ResolveToolCommand(string tool)
    {
        if (Path.IsPathFullyQualified(tool) && File.Exists(tool))
        {
            return new CounterToolCommand(tool, []);
        }

        var executable = LoadToolInvoker.ResolveExecutable(tool);
        return executable is null ? null : new CounterToolCommand(executable, []);
    }

    private sealed record CounterToolCommand(string ExecutablePath, IReadOnlyList<string> PrefixArguments);
}

internal static class RuntimeCounterParser
{
    public static RuntimeCounterSummary Parse(string? path, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || new FileInfo(path).Length == 0)
        {
            return new RuntimeCounterSummary
            {
                CollectionStartUtc = startUtc,
                CollectionEndUtc = endUtc,
                ParseWarnings = ["counter-raw-output-missing-or-empty"]
            };
        }

        var samples = Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? ParseCsv(path, warnings)
            : ParseJson(path, warnings);
        return Summarize(samples, startUtc, endUtc, warnings);
    }

    private static RuntimeCounterSummary Summarize(
        IReadOnlyList<CounterValue> samples,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        List<string> warnings)
    {
        if (samples.Count == 0)
        {
            warnings.Add("counter-parser-found-no-numeric-samples");
        }

        var byName = samples
            .GroupBy(sample => NormalizeName(sample.Name))
            .ToDictionary(group => group.Key, group => group.Select(sample => sample.Value).ToArray(), StringComparer.OrdinalIgnoreCase);

        CounterValue[] FindSamples(params string[] tokens)
        {
            return samples
                .Where(sample => tokens.All(token => sample.SearchText.Contains(token, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }

        double[] FindValues(params string[] tokens)
        {
            return FindSamples(tokens)
                .Select(sample => sample.Value)
                .ToArray();
        }

        double[] CpuPercentValues()
        {
            var cpuSamples = FindSamples("process cpu time");
            if (cpuSamples.Length == 0)
            {
                return FindSamples("cpu usage").Select(sample => sample.Value).ToArray();
            }

            var timestamped = cpuSamples
                .Where(sample => sample.TimestampUtc.HasValue)
                .GroupBy(sample => sample.TimestampUtc!.Value)
                .Select(group => group.Sum(sample => sample.Value) * 100.0d)
                .ToArray();

            return timestamped.Length > 0
                ? timestamped
                : cpuSamples.Select(sample => sample.Value * 100.0d).ToArray();
        }

        static double? Mean(double[] values) => values.Length == 0 ? null : values.Average();
        static double? Max(double[] values) => values.Length == 0 ? null : values.Max();
        static double? Delta(double[] values) => values.Length < 2 ? null : values[^1] - values[0];
        static double? Sum(double[] values) => values.Length == 0 ? null : values.Sum();
        static double? RateSumOrDelta(CounterValue[] values)
        {
            if (values.Length == 0)
            {
                return null;
            }

            return values.Any(value => string.Equals(value.CounterType, "Rate", StringComparison.OrdinalIgnoreCase))
                ? values.Sum(value => value.Value)
                : Delta(values.Select(value => value.Value).ToArray());
        }

        var cpu = CpuPercentValues();
        var allocRate = FindSamples("total allocated").Select(sample => sample.Value).ToArray();
        if (allocRate.Length == 0)
        {
            allocRate = FindSamples("alloc rate").Select(sample => sample.Value).ToArray();
        }

        var allocated = FindSamples("total allocated").Select(sample => sample.Value).ToArray();
        var gen0Samples = FindSamples("gc collections", "gen0");
        if (gen0Samples.Length == 0)
        {
            gen0Samples = FindSamples("gen 0");
        }

        var gen1Samples = FindSamples("gc collections", "gen1");
        if (gen1Samples.Length == 0)
        {
            gen1Samples = FindSamples("gen 1");
        }

        var gen2Samples = FindSamples("gc collections", "gen2");
        if (gen2Samples.Length == 0)
        {
            gen2Samples = FindSamples("gen 2");
        }

        var heap = FindSamples("heap size").Select(sample => sample.Value).ToArray();
        var pause = FindValues("pause");
        var poolThreads = FindSamples("thread pool thread count").Select(sample => sample.Value).ToArray();
        var queue = FindSamples("thread pool queue length").Concat(FindSamples("threadpool queue length")).Select(sample => sample.Value).ToArray();
        var exceptionSamples = FindSamples("exception");
        var exception = exceptionSamples.Select(sample => sample.Value).ToArray();

        return new RuntimeCounterSummary
        {
            Samples = samples.Count,
            CollectionStartUtc = startUtc,
            CollectionEndUtc = endUtc,
            CpuMean = Mean(cpu),
            CpuMax = Max(cpu),
            AllocatedBytesDelta = Delta(allocated),
            AllocationRateMean = Mean(allocRate),
            Gen0CollectionsDelta = RateSumOrDelta(gen0Samples),
            Gen1CollectionsDelta = RateSumOrDelta(gen1Samples),
            Gen2CollectionsDelta = RateSumOrDelta(gen2Samples),
            GcHeapSizeMean = Mean(heap),
            GcHeapSizeMax = Max(heap),
            GcPauseTimeDelta = Sum(pause),
            ThreadPoolThreadCountMean = Mean(poolThreads),
            ThreadPoolQueueLengthMax = Max(queue),
            ExceptionCountDelta = RateSumOrDelta(exceptionSamples),
            ExceptionRateMean = Mean(exception),
            ParseWarnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static IReadOnlyList<CounterValue> ParseJson(string path, List<string> warnings)
    {
        var text = File.ReadAllText(path);
        var values = new List<CounterValue>();
        try
        {
            using var document = JsonDocument.Parse(text);
            VisitJson(document.RootElement, values);
        }
        catch (JsonException)
        {
            foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                try
                {
                    using var document = JsonDocument.Parse(line);
                    VisitJson(document.RootElement, values);
                }
                catch (JsonException)
                {
                    warnings.Add("counter-json-line-parse-failed");
                }
            }
        }

        return values;
    }

    private static void VisitJson(JsonElement element, List<CounterValue> values)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                VisitJson(child, values);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        string? name = null;
        string? tags = null;
        string? counterType = null;
        DateTimeOffset? timestamp = null;
        double? value = null;
        foreach (var property in element.EnumerateObject())
        {
            if (name is null && IsNameProperty(property.Name) && property.Value.ValueKind == JsonValueKind.String)
            {
                name = property.Value.GetString();
            }

            if (tags is null && string.Equals(property.Name, "tags", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
            {
                tags = property.Value.GetString();
            }

            if (counterType is null && string.Equals(property.Name, "counterType", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
            {
                counterType = property.Value.GetString();
            }

            if (timestamp is null &&
                string.Equals(property.Name, "timestamp", StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(property.Value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedTimestamp))
            {
                timestamp = parsedTimestamp.ToUniversalTime();
            }

            if (value is null && IsValueProperty(property.Name))
            {
                value = ReadDouble(property.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(name) && value.HasValue)
        {
            values.Add(new CounterValue(name, tags, counterType, timestamp, value.Value));
        }

        foreach (var property in element.EnumerateObject())
        {
            VisitJson(property.Value, values);
        }
    }

    private static IReadOnlyList<CounterValue> ParseCsv(string path, List<string> warnings)
    {
        var lines = File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        if (lines.Length < 2)
        {
            return [];
        }

        var headers = SplitCsvLine(lines[0]);
        var nameIndex = Array.FindIndex(headers, IsNameProperty);
        var valueIndex = Array.FindIndex(headers, IsValueProperty);
        if (nameIndex < 0 || valueIndex < 0)
        {
            warnings.Add("counter-csv-required-columns-missing");
            return [];
        }

        var values = new List<CounterValue>();
        foreach (var line in lines.Skip(1))
        {
            var columns = SplitCsvLine(line);
            if (columns.Length <= Math.Max(nameIndex, valueIndex))
            {
                continue;
            }

            if (double.TryParse(columns[valueIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                values.Add(new CounterValue(columns[nameIndex], null, null, null, value));
            }
        }

        return values;
    }

    private static string[] SplitCsvLine(string line)
    {
        return line.Split(',').Select(column => column.Trim().Trim('"')).ToArray();
    }

    private static bool IsNameProperty(string name)
    {
        return name.Contains("name", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("counter", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValueProperty(string name)
    {
        if (name.Contains("counter name", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return name.Contains("mean", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("value", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("increment", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("rate", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("count", StringComparison.OrdinalIgnoreCase);
    }

    private static double? ReadDouble(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var value))
        {
            return value;
        }

        return element.ValueKind == JsonValueKind.String &&
            double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string NormalizeName(string value)
    {
        return value.Replace("-", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("_", " ", StringComparison.OrdinalIgnoreCase)
            .Replace(".", " ", StringComparison.OrdinalIgnoreCase)
            .Trim()
            .ToLowerInvariant();
    }

    private sealed record CounterValue(string Name, string? Tags, string? CounterType, DateTimeOffset? TimestampUtc, double Value)
    {
        public string SearchText => NormalizeName(string.Join(" ", Name, Tags ?? ""));
    }
}
