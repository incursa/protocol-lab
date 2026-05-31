// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Globalization;
using System.Text;

namespace Incursa.ProtocolLab.Model;

public sealed record RunSummary(string RunId, IReadOnlyList<BenchmarkResult> Results);

public static class MarkdownSummaryWriter
{
    public static string Write(RunSummary summary)
    {
        return Write(RunReportBuilder.Build(summary.RunId, DateTimeOffset.UtcNow, metadata: null, summary.Results));
    }

    public static string Write(RunReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Incursa Protocol Lab Run {report.RunId}");
        builder.AppendLine();
        builder.AppendLine($"Generated at: {report.GeneratedAt:O}");
        builder.AppendLine($"Claim level: {FormatClaimLevel(report.ClaimLevel)}");

        if (report.Metadata is not null)
        {
            builder.AppendLine();
            builder.AppendLine("## Run Metadata");
            AppendMetadata(builder, report.Metadata);
        }

        builder.AppendLine();
        builder.AppendLine("## Totals");
        AppendTotals(builder, report.Totals);

        builder.AppendLine();
        builder.AppendLine("## Aggregate Results");

        if (report.Aggregates.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("_No benchmark cells were aggregated._");
            return builder.ToString();
        }

        builder.AppendLine();
        builder.AppendLine("> Best and worst follow metric direction. Throughput uses higher-is-better; latency uses lower-is-better.");
        builder.AppendLine();
        builder.AppendLine("| implementation | scenario | protocol | protocol proof | target mode | load tool | category | mode | evidence | comparability | network | load shape | reps | validation | benchmark status | parsed | requests/s (med/best/worst) | p50 ms | p95 ms | p99 ms | latency mean ms | throughput B/s | qlog | target metrics | warnings | errors | failure reason |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");

        foreach (var aggregate in report.Aggregates)
        {
            builder.Append("| ");
            builder.Append(FormatLabel(aggregate.ImplementationId, aggregate.ImplementationName));
            builder.Append(" | ");
            builder.Append(FormatLabel(aggregate.ScenarioId, aggregate.ScenarioName));
            builder.Append(" | ");
            builder.Append(Escape(aggregate.Protocol));
            builder.Append(" | ");
            builder.Append(Escape(FormatExecutionStatuses(aggregate.ProtocolProofStatuses)));
            builder.Append(" | ");
            builder.Append(Escape(aggregate.TargetExecutionMode ?? "process"));
            builder.Append(" | ");
            builder.Append(Escape(aggregate.LoadTool ?? "n/a"));
            builder.Append(" | ");
            builder.Append(Escape(aggregate.LoadToolCategory ?? "n/a"));
            builder.Append(" | ");
            builder.Append(Escape(aggregate.LoadToolMode ?? "n/a"));
            builder.Append(" | ");
            builder.Append(Escape(aggregate.Evidence?.EvidenceClass ?? "n/a"));
            builder.Append(" | ");
            builder.Append(Escape(aggregate.Evidence?.ComparabilityStatus ?? "n/a"));
            builder.Append(" | ");
            builder.Append(Escape(aggregate.NetworkProfile));
            builder.Append(" | ");
            builder.Append(Escape(FormatLoadShape(aggregate)));
            builder.Append(" | ");
            builder.Append(aggregate.Repetitions);
            builder.Append(" | ");
            builder.Append(Escape(FormatValidation(aggregate.Validation)));
            builder.Append(" | ");
            builder.Append(Escape(FormatExecutionStatuses(aggregate.BenchmarkExecutionStatuses)));
            builder.Append(" | ");
            builder.Append(Escape(FormatParsedMetrics(aggregate.ParsedMetricsCount, aggregate.Repetitions)));
            builder.Append(" | ");
            builder.Append(Escape(FormatMetricTriple(aggregate.RequestsPerSecond)));
            builder.Append(" | ");
            builder.Append(Escape(FormatMetricTriple(aggregate.LatencyP50Ms)));
            builder.Append(" | ");
            builder.Append(Escape(FormatMetricTriple(aggregate.LatencyP95Ms)));
            builder.Append(" | ");
            builder.Append(Escape(FormatMetricTriple(aggregate.LatencyP99Ms)));
            builder.Append(" | ");
            builder.Append(Escape(FormatMetricTriple(aggregate.LatencyMeanMs)));
            builder.Append(" | ");
            builder.Append(Escape(FormatMetricTriple(aggregate.ThroughputBytesPerSecond)));
            builder.Append(" | ");
            builder.Append(Escape(FormatQlog(aggregate)));
            builder.Append(" | ");
            builder.Append(Escape(FormatTargetMetrics(aggregate)));
            builder.Append(" | ");
            builder.Append(Escape(FormatWarnings(aggregate)));
            builder.Append(" | ");
            builder.Append(aggregate.ErrorCount);
            builder.Append(" | ");
            builder.Append(Escape(FormatFailureReasons(aggregate.FailureReasons)));
            builder.AppendLine(" |");
        }

        builder.AppendLine();
        builder.AppendLine("## Target Metadata");
        if (report.Aggregates.Count == 0)
        {
            builder.AppendLine("_No benchmark cells were aggregated._");
        }
        else
        {
            foreach (var aggregate in report.Aggregates)
            {
                builder.AppendLine($"- {Escape(aggregate.ImplementationId)}/{Escape(aggregate.ScenarioId)}/{Escape(aggregate.Protocol)}: {FormatTargetMetadata(aggregate)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Load Generator Diagnostics");
        if (report.Aggregates.Count == 0)
        {
            builder.AppendLine("_No benchmark cells were aggregated._");
        }
        else
        {
            builder.AppendLine();
            builder.AppendLine("| implementation | scenario | load tool | samples | saturation | cpu mean | cpu max | memory max bytes | memory max % | net rx delta | net tx delta | warnings |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var aggregate in report.Aggregates)
            {
                builder.Append("| ");
                builder.Append(Escape(aggregate.ImplementationId));
                builder.Append(" | ");
                builder.Append(Escape(aggregate.ScenarioId));
                builder.Append(" | ");
                builder.Append(Escape($"{aggregate.LoadTool ?? "n/a"} / {aggregate.LoadToolMode ?? "n/a"}"));
                builder.Append(" | ");
                builder.Append(Escape($"captured {aggregate.LoadToolDockerMetricsCapturedCount}/{aggregate.Repetitions}, samples med {FormatNullableInt(aggregate.LoadToolDockerMetricsSampleCountMedian)}"));
                builder.Append(" | ");
                builder.Append(Escape(FormatExecutionStatuses(aggregate.LoadToolSaturationStatuses)));
                builder.Append(" | ");
                builder.Append(Escape(FormatMetricTriple(aggregate.LoadToolCpuMeanPercent)));
                builder.Append(" | ");
                builder.Append(Escape(FormatMetricTriple(aggregate.LoadToolCpuMaxPercent)));
                builder.Append(" | ");
                builder.Append(Escape(FormatMetricTriple(aggregate.LoadToolMemoryMaxBytes)));
                builder.Append(" | ");
                builder.Append(Escape(FormatMetricTriple(aggregate.LoadToolMemoryMaxPercent)));
                builder.Append(" | ");
                builder.Append(Escape(FormatMetricTriple(aggregate.LoadToolNetworkRxBytesDelta)));
                builder.Append(" | ");
                builder.Append(Escape(FormatMetricTriple(aggregate.LoadToolNetworkTxBytesDelta)));
                builder.Append(" | ");
                builder.Append(Escape(aggregate.SaturationWarnings.Count == 0 ? "n/a" : string.Join("; ", aggregate.SaturationWarnings)));
                builder.AppendLine(" |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Target Container Diagnostics");
        if (report.Aggregates.Count == 0)
        {
            builder.AppendLine("_No benchmark cells were aggregated._");
        }
        else
        {
            builder.AppendLine();
            builder.AppendLine("| implementation | scenario | target mode | samples | saturation | cpu mean | cpu max | memory max bytes | memory max % | net rx delta | net tx delta | warnings |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var aggregate in report.Aggregates)
            {
                builder.Append("| ");
                builder.Append(Escape(aggregate.ImplementationId));
                builder.Append(" | ");
                builder.Append(Escape(aggregate.ScenarioId));
                builder.Append(" | ");
                builder.Append(Escape(aggregate.TargetExecutionMode ?? "process"));
                builder.Append(" | ");
                builder.Append(Escape($"captured {aggregate.TargetDockerMetricsCapturedCount}/{aggregate.Repetitions}, samples med {FormatNullableInt(aggregate.TargetDockerMetricsSampleCountMedian)}"));
                builder.Append(" | ");
                builder.Append(Escape(FormatExecutionStatuses(aggregate.TargetSaturationStatuses)));
                builder.Append(" | ");
                builder.Append(Escape(FormatMetricTriple(aggregate.TargetCpuMeanPercent)));
                builder.Append(" | ");
                builder.Append(Escape(FormatMetricTriple(aggregate.TargetCpuMaxPercent)));
                builder.Append(" | ");
                builder.Append(Escape(FormatMetricTriple(aggregate.TargetMemoryMaxBytes)));
                builder.Append(" | ");
                builder.Append(Escape(FormatMetricTriple(aggregate.TargetMemoryMaxPercent)));
                builder.Append(" | ");
                builder.Append(Escape(FormatMetricTriple(aggregate.TargetNetworkRxBytesDelta)));
                builder.Append(" | ");
                builder.Append(Escape(FormatMetricTriple(aggregate.TargetNetworkTxBytesDelta)));
                builder.Append(" | ");
                builder.Append(Escape(aggregate.SaturationWarnings.Count == 0 ? "n/a" : string.Join("; ", aggregate.SaturationWarnings)));
                builder.AppendLine(" |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Runtime Diagnostics");
        if (report.Aggregates.Count == 0)
        {
            builder.AppendLine("_No benchmark cells were aggregated._");
        }
        else
        {
            builder.AppendLine();
            builder.AppendLine("| implementation | scenario | diagnostic pid | confidence | counters | cpu mean | cpu max | allocation rate mean | gc delta gen0/gen1/gen2 | thread pool queue max | exception rate mean |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var aggregate in report.Aggregates)
            {
                builder.Append("| ");
                builder.Append(Escape(aggregate.ImplementationId));
                builder.Append(" | ");
                builder.Append(Escape(aggregate.ScenarioId));
                builder.Append(" | ");
                builder.Append(Escape(aggregate.DiagnosticProcessId?.ToString(CultureInfo.InvariantCulture) ?? "n/a"));
                builder.Append(" | ");
                builder.Append(Escape($"{aggregate.DiagnosticConfidence ?? "n/a"} / {aggregate.DiagnosticResolutionStrategy ?? "n/a"}"));
                builder.Append(" | ");
                builder.Append(Escape(FormatExecutionStatuses(aggregate.CountersCaptureStatuses)));
                builder.Append(" | ");
                builder.Append(Escape(FormatMetricTriple(aggregate.CounterCpuMean)));
                builder.Append(" | ");
                builder.Append(Escape(FormatMetricTriple(aggregate.CounterCpuMax)));
                builder.Append(" | ");
                builder.Append(Escape(FormatMetricTriple(aggregate.CounterAllocationRateMean)));
                builder.Append(" | ");
                builder.Append(Escape($"{FormatMetricTriple(aggregate.CounterGen0CollectionsDelta)} / {FormatMetricTriple(aggregate.CounterGen1CollectionsDelta)} / {FormatMetricTriple(aggregate.CounterGen2CollectionsDelta)}"));
                builder.Append(" | ");
                builder.Append(Escape(FormatMetricTriple(aggregate.CounterThreadPoolQueueLengthMax)));
                builder.Append(" | ");
                builder.Append(Escape(FormatMetricTriple(aggregate.CounterExceptionRateMean)));
                builder.AppendLine(" |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Interpretation");
        builder.AppendLine("- `local-lab` rows are local shared-host managed-lab measurements.");
        builder.AppendLine("- `external-reference-local` rows are local shared-host external-reference h2load measurements.");
        builder.AppendLine("- These results are not publishable benchmark evidence.");
        builder.AppendLine("- Use them for regression and profiling direction only.");

        return builder.ToString();
    }

    private static void AppendMetadata(StringBuilder builder, RunMetadata metadata)
    {
        builder.AppendLine($"- host: {Escape(metadata.HostName)}");
        builder.AppendLine($"- operating system: {Escape(metadata.OperatingSystem)}");
        builder.AppendLine($"- operating system architecture: {Escape(metadata.OperatingSystemArchitecture)}");
        builder.AppendLine($"- runtime: {Escape(metadata.FrameworkDescription)}");
        builder.AppendLine($"- process architecture: {Escape(metadata.ProcessArchitecture)}");
        builder.AppendLine($"- execution profile: {Escape(ExecutionProfiles.ToId(metadata.ExecutionProfile))}");
        builder.AppendLine($"- processor count: {metadata.ProcessorCount}");
        builder.AppendLine($"- is 64-bit process: {(metadata.Is64BitProcess ? "yes" : "no")}");
        builder.AppendLine($"- working set bytes: {metadata.WorkingSetBytes.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine($"- total available memory bytes: {FormatNullableLong(metadata.TotalAvailableMemoryBytes)}");
        builder.AppendLine($"- docker version: {Escape(FirstLine(metadata.DockerVersion) ?? "n/a")}");
        builder.AppendLine($"- docker backend: {Escape(metadata.DockerBackend ?? "n/a")}");
        builder.AppendLine($"- network mode: {Escape(metadata.NetworkMode ?? "n/a")}");
        builder.AppendLine($"- runner process id: {metadata.ProcessId?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}");
        builder.AppendLine($"- metadata timestamp utc: {metadata.TimestampUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "n/a"}");
        builder.AppendLine($"- git commit: {Escape(metadata.GitCommit ?? "n/a")}");
        builder.AppendLine($"- working tree status: {Escape(FirstLine(metadata.WorkingTreeStatus) ?? "clean or unavailable")}");
        if (metadata.Warnings is { Count: > 0 })
        {
            builder.AppendLine($"- metadata warnings: {Escape(string.Join("; ", metadata.Warnings))}");
        }
    }

    private static void AppendTotals(StringBuilder builder, RunTotals totals)
    {
        builder.AppendLine($"- results: {totals.ResultCount}");
        builder.AppendLine($"- aggregates: {totals.AggregateCount}");
        builder.AppendLine($"- validation: passed {totals.Validation.Passed}, failed {totals.Validation.Failed}, unsupported {totals.Validation.Unsupported}, not-applicable {totals.Validation.NotApplicable}");
        builder.AppendLine($"- benchmark attempts: {totals.BenchmarkAttemptCount}");
        builder.AppendLine($"- parsed metrics: {totals.ParsedMetricsCount}");
        builder.AppendLine($"- warnings: {totals.WarningCount}");
        builder.AppendLine($"- errors: {totals.ErrorCount}");
        builder.AppendLine($"- saturation warnings: {totals.SaturationWarningCount}");
        builder.AppendLine($"- comparability: local {totals.ComparableLocalCount}, warnings {totals.ComparableWithWarningsCount}, not-comparable {totals.NotComparableCount}, invalid {totals.InvalidCount}");
        builder.AppendLine($"- target process metrics: captured {totals.TargetProcessMetricsCapturedCount}, missing {totals.TargetProcessMetricsMissingCount}");
        builder.AppendLine($"- target Docker metrics: captured {totals.TargetDockerMetricsCapturedCount}, missing {totals.TargetDockerMetricsMissingCount}");
        builder.AppendLine($"- runtime counters: captured {totals.CountersCapturedCount}, missing {totals.CountersMissingCount}");
        builder.AppendLine($"- load-generator Docker metrics: captured {totals.LoadToolDockerMetricsCapturedCount}, missing {totals.LoadToolDockerMetricsMissingCount}");
        builder.AppendLine($"- docker cleanup failures: {totals.DockerCleanupFailureCount}");
    }

    private static string FormatLabel(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || string.Equals(id, name, StringComparison.Ordinal))
        {
            return Escape(id);
        }

        return $"{Escape(id)} ({Escape(name)})";
    }

    private static string FormatLoadShape(RunAggregate aggregate)
    {
        var effective = aggregate.EffectiveConcurrency.HasValue || aggregate.EffectiveStreamsPerConnection.HasValue
            ? $" effective concurrency {aggregate.EffectiveConcurrency?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}, streams {aggregate.EffectiveStreamsPerConnection?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}"
            : "";
        var sample = aggregate.Repetitions == 1 ? " single-run sample" : "";
        return $"requested c{aggregate.Connections}-s{aggregate.StreamsPerConnection} @ {aggregate.DurationSeconds}s/{aggregate.WarmupSeconds}s;{effective}{sample}";
    }

    private static string FormatValidation(ValidationCounts validation)
    {
        return $"passed {validation.Passed}, failed {validation.Failed}, unsupported {validation.Unsupported}, not-applicable {validation.NotApplicable}";
    }

    private static string FormatParsedMetrics(int parsedMetricsCount, int repetitions)
    {
        return $"{parsedMetricsCount}/{repetitions}";
    }

    private static string FormatExecutionStatuses(IReadOnlyDictionary<string, int> statuses)
    {
        if (statuses.Count == 0)
        {
            return "n/a";
        }

        return string.Join(", ", statuses
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static pair => $"{pair.Key} {pair.Value}"));
    }

    private static string FormatMetricTriple(MetricTriple triple)
    {
        if (triple.Median is null || triple.Best is null || triple.Worst is null)
        {
            return "n/a";
        }

        return $"{FormatNumber(triple.Median.Value)} / {FormatNumber(triple.Best.Value)} / {FormatNumber(triple.Worst.Value)}";
    }

    private static string FormatFailureReasons(IReadOnlyList<string> reasons)
    {
        return reasons.Count == 0
            ? "n/a"
            : string.Join("; ", reasons);
    }

    private static string FormatWarnings(RunAggregate aggregate)
    {
        return aggregate.Warnings.Count == 0
            ? aggregate.WarningCount.ToString(CultureInfo.InvariantCulture)
            : string.Join("; ", aggregate.Warnings);
    }

    private static string FormatQlog(RunAggregate aggregate)
    {
        if (string.IsNullOrWhiteSpace(aggregate.QlogDirectory))
        {
            return "n/a";
        }

        return $"{aggregate.QlogAvailableCount}/{aggregate.QlogAvailableCount + aggregate.QlogMissingCount} files, median {FormatNullableInt(aggregate.QlogFileCountMedian)}";
    }

    private static string FormatTargetMetrics(RunAggregate aggregate)
    {
        if (aggregate.TargetProcessMetricsCapturedCount == 0 && aggregate.TargetProcessMetricsMissingCount == 0)
        {
            return aggregate.TargetDockerMetricsCapturedCount == 0 && aggregate.TargetDockerMetricsMissingCount == 0
                ? "n/a"
                : $"docker captured {aggregate.TargetDockerMetricsCapturedCount}/{aggregate.TargetDockerMetricsCapturedCount + aggregate.TargetDockerMetricsMissingCount}, samples med {FormatNullableInt(aggregate.TargetDockerMetricsSampleCountMedian)}";
        }

        var process = $"process captured {aggregate.TargetProcessMetricsCapturedCount}/{aggregate.TargetProcessMetricsCapturedCount + aggregate.TargetProcessMetricsMissingCount}, samples med {FormatNullableInt(aggregate.TargetProcessMetricsSampleCountMedian)}";
        if (aggregate.TargetDockerMetricsCapturedCount == 0 && aggregate.TargetDockerMetricsMissingCount == 0)
        {
            return process;
        }

        return $"{process}; docker captured {aggregate.TargetDockerMetricsCapturedCount}/{aggregate.TargetDockerMetricsCapturedCount + aggregate.TargetDockerMetricsMissingCount}, samples med {FormatNullableInt(aggregate.TargetDockerMetricsSampleCountMedian)}";
    }

    private static string FormatTargetMetadata(RunAggregate aggregate)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(aggregate.TargetExecutionMode))
        {
            parts.Add($"mode {Escape(aggregate.TargetExecutionMode)}");
        }

        if (!string.IsNullOrWhiteSpace(aggregate.TargetContract))
        {
            parts.Add($"contract {Escape(aggregate.TargetContract)}");
        }

        if (!string.IsNullOrWhiteSpace(aggregate.TargetDockerImage))
        {
            parts.Add($"image {Escape(aggregate.TargetDockerImage)}");
        }

        if (!string.IsNullOrWhiteSpace(aggregate.TargetContainerName))
        {
            parts.Add($"container {Escape(aggregate.TargetContainerName)}");
        }

        if (!string.IsNullOrWhiteSpace(aggregate.TargetDockerNetworkMode))
        {
            parts.Add($"network mode {Escape(aggregate.TargetDockerNetworkMode)}");
        }

        if (!string.IsNullOrWhiteSpace(aggregate.TargetDockerNetworkName))
        {
            parts.Add($"network {Escape(aggregate.TargetDockerNetworkName)}");
        }

        if (aggregate.TargetNetworkAliases.Count > 0)
        {
            parts.Add($"aliases {Escape(string.Join(", ", aggregate.TargetNetworkAliases))}");
        }

        if (!string.IsNullOrWhiteSpace(aggregate.NetworkCleanupStatus))
        {
            parts.Add($"network cleanup {Escape(aggregate.NetworkCleanupStatus)}");
        }

        if (aggregate.TargetDockerResourceLimitsRequested?.HasAnyLimit == true)
        {
            parts.Add($"target limits {Escape(FormatDockerLimits(aggregate.TargetDockerResourceLimitsRequested))}");
        }

        if (aggregate.LoadToolDockerResourceLimitsRequested?.HasAnyLimit == true)
        {
            parts.Add($"load-tool limits {Escape(FormatDockerLimits(aggregate.LoadToolDockerResourceLimitsRequested))}");
        }

        if (aggregate.DockerCleanup is not null)
        {
            parts.Add($"cleanup {Escape(FormatDockerCleanup(aggregate.DockerCleanup))}");
        }

        if (aggregate.TargetPublishedPorts.Count > 0)
        {
            parts.Add($"ports {Escape(string.Join(", ", aggregate.TargetPublishedPorts.Select(pair => $"{pair.Key} {pair.Value}")))}");
        }

        if (!string.IsNullOrWhiteSpace(aggregate.AdapterControlPlaneBaseUrl))
        {
            parts.Add($"adapter control plane {Escape(aggregate.AdapterControlPlaneBaseUrl)}");
        }

        if (!string.IsNullOrWhiteSpace(aggregate.AdapterSessionId))
        {
            parts.Add($"adapter session {Escape(aggregate.AdapterSessionId)}");
        }

        if (aggregate.AdapterEndpointTypes.Count > 0)
        {
            parts.Add($"adapter endpoints {Escape(string.Join(", ", aggregate.AdapterEndpointTypes))}");
        }

        if (aggregate.TargetProcessId.HasValue)
        {
            parts.Add($"pid {aggregate.TargetProcessId.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (aggregate.TargetStartTimeUtc.HasValue)
        {
            parts.Add($"start {aggregate.TargetStartTimeUtc.Value.ToString("O", CultureInfo.InvariantCulture)}");
        }

        if (aggregate.TargetReadyTimeUtc.HasValue)
        {
            parts.Add($"ready {aggregate.TargetReadyTimeUtc.Value.ToString("O", CultureInfo.InvariantCulture)}");
        }

        if (aggregate.TargetExitCode.HasValue)
        {
            parts.Add($"exit {aggregate.TargetExitCode.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (!string.IsNullOrWhiteSpace(aggregate.TargetCommandLine))
        {
            parts.Add($"command {Escape(aggregate.TargetCommandLine)}");
        }

        if (!string.IsNullOrWhiteSpace(aggregate.QlogDirectory))
        {
            parts.Add($"qlog {Escape(aggregate.QlogDirectory)}");
        }

        return parts.Count == 0 ? "n/a" : string.Join("; ", parts);
    }

    private static string FormatDockerLimits(DockerResourceLimits limits)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(limits.Cpus))
        {
            parts.Add($"cpus={limits.Cpus}");
        }

        if (!string.IsNullOrWhiteSpace(limits.CpusetCpus))
        {
            parts.Add($"cpuset={limits.CpusetCpus}");
        }

        if (!string.IsNullOrWhiteSpace(limits.Memory))
        {
            parts.Add($"memory={limits.Memory}");
        }

        if (!string.IsNullOrWhiteSpace(limits.MemorySwap))
        {
            parts.Add($"memory-swap={limits.MemorySwap}");
        }

        if (limits.PidsLimit.HasValue)
        {
            parts.Add($"pids={limits.PidsLimit.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        return parts.Count == 0 ? "none" : string.Join(", ", parts);
    }

    private static string FormatDockerCleanup(DockerCleanupSummary cleanup)
    {
        var parts = new List<string>();
        if (cleanup.TargetContainerCleanupAttempted)
        {
            parts.Add($"target={FormatNullableBool(cleanup.TargetContainerCleanupSucceeded)}");
        }

        if (cleanup.LoadToolContainerCleanupAttempted)
        {
            parts.Add($"load-tool={FormatNullableBool(cleanup.LoadToolContainerCleanupSucceeded)}");
        }

        if (cleanup.LoadToolMetricsSamplerCleanupAttempted)
        {
            parts.Add($"metrics-sampler={FormatNullableBool(cleanup.LoadToolMetricsSamplerCleanupSucceeded)}");
        }

        if (cleanup.TargetMetricsSamplerCleanupAttempted)
        {
            parts.Add($"target-metrics-sampler={FormatNullableBool(cleanup.TargetMetricsSamplerCleanupSucceeded)}");
        }

        if (cleanup.NetworkCleanupAttempted)
        {
            parts.Add($"network={FormatNullableBool(cleanup.NetworkCleanupSucceeded)}");
        }

        if (cleanup.LeftoverContainers.Count > 0)
        {
            parts.Add($"leftover containers {cleanup.LeftoverContainers.Count}");
        }

        if (cleanup.LeftoverNetworks.Count > 0)
        {
            parts.Add($"leftover networks {cleanup.LeftoverNetworks.Count}");
        }

        return parts.Count == 0 ? "n/a" : string.Join(", ", parts);
    }

    private static string FormatNullableBool(bool? value)
    {
        return value.HasValue ? (value.Value ? "ok" : "failed") : "n/a";
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatNullableLong(long? value)
    {
        return value.HasValue
            ? value.Value.ToString(CultureInfo.InvariantCulture)
            : "n/a";
    }

    private static string FormatNullableInt(int? value)
    {
        return value.HasValue
            ? value.Value.ToString(CultureInfo.InvariantCulture)
            : "n/a";
    }

    private static string? FirstLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var lines = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length > 0 ? lines[0] : value.Trim();
    }

    private static string FormatClaimLevel(ReportClaimLevel claimLevel)
    {
        return claimLevel switch
        {
            ReportClaimLevel.DiagnosticOnly => "diagnostic-only",
            ReportClaimLevel.Validation => "validation",
            ReportClaimLevel.Regression => "regression",
            ReportClaimLevel.Benchmark => "benchmark",
            ReportClaimLevel.Verified => "verified",
            _ => claimLevel.ToString().ToLowerInvariant()
        };
    }

    private static string Escape(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }
}
