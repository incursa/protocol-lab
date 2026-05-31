// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public sealed record RunMetadata(
    string HostName,
    string OperatingSystem,
    string FrameworkDescription,
    string ProcessArchitecture,
    string OperatingSystemArchitecture,
    int ProcessorCount,
    bool Is64BitProcess,
    long WorkingSetBytes,
    long? TotalAvailableMemoryBytes,
    string? DockerVersion,
    string? NetworkMode,
    string? DockerBackend = null,
    int? ProcessId = null,
    DateTimeOffset? TimestampUtc = null,
    string? GitCommit = null,
    string? WorkingTreeStatus = null,
    IReadOnlyList<string>? Warnings = null);

public sealed record ValidationCounts(
    int Passed,
    int Failed,
    int Unsupported,
    int NotApplicable,
    int Inconclusive,
    int InfrastructureFailure);

public sealed record MetricTriple(
    double? Median,
    double? Best,
    double? Worst);

public sealed record RunAggregate
{
    public required string ImplementationId { get; init; }
    public required string ImplementationName { get; init; }
    public required string ScenarioId { get; init; }
    public required string ScenarioName { get; init; }
    public required string Family { get; init; }
    public required string Protocol { get; init; }
    public required string Role { get; init; }
    public string? LoadProfileId { get; init; }
    public string? LoadProfileTitle { get; init; }
    public string? LoadProfilePurpose { get; init; }
    public string? LoadTool { get; init; }
    public string? LoadToolMode { get; init; }
    public string? LoadToolCategory { get; init; }
    public string? TargetExecutionMode { get; init; }
    public string? TargetContract { get; init; }
    public string? TargetDockerImage { get; init; }
    public string? TargetContainerName { get; init; }
    public string? TargetDockerNetwork { get; init; }
    public string? TargetDockerNetworkName { get; init; }
    public string? TargetDockerNetworkId { get; init; }
    public string? TargetDockerNetworkMode { get; init; }
    public IReadOnlyList<string> TargetNetworkAliases { get; init; } = [];
    public IReadOnlyDictionary<string, string> TargetPublishedPorts { get; init; } = new Dictionary<string, string>();
    public string? AdapterControlPlaneBaseUrl { get; init; }
    public string? AdapterSessionId { get; init; }
    public IReadOnlyList<string> AdapterEndpointTypes { get; init; } = [];
    public DockerResourceLimits? TargetDockerResourceLimitsRequested { get; init; }
    public DockerResourceLimits? TargetDockerResourceLimitsEffective { get; init; }
    public int TargetDockerMetricsCapturedCount { get; init; }
    public int TargetDockerMetricsMissingCount { get; init; }
    public int TargetDockerMetricsSampleCountMedian { get; init; }
    public required IReadOnlyDictionary<string, int> TargetSaturationStatuses { get; init; }
    public required MetricTriple TargetCpuMeanPercent { get; init; }
    public required MetricTriple TargetCpuMaxPercent { get; init; }
    public required MetricTriple TargetMemoryMaxBytes { get; init; }
    public required MetricTriple TargetMemoryMaxPercent { get; init; }
    public required MetricTriple TargetNetworkRxBytesDelta { get; init; }
    public required MetricTriple TargetNetworkTxBytesDelta { get; init; }
    public DockerResourceLimits? LoadToolDockerResourceLimitsRequested { get; init; }
    public DockerResourceLimits? LoadToolDockerResourceLimitsEffective { get; init; }
    public int LoadToolDockerMetricsCapturedCount { get; init; }
    public int LoadToolDockerMetricsMissingCount { get; init; }
    public int LoadToolDockerMetricsSampleCountMedian { get; init; }
    public required IReadOnlyDictionary<string, int> LoadToolSaturationStatuses { get; init; }
    public required MetricTriple LoadToolCpuMeanPercent { get; init; }
    public required MetricTriple LoadToolCpuMaxPercent { get; init; }
    public required MetricTriple LoadToolMemoryMaxBytes { get; init; }
    public required MetricTriple LoadToolMemoryMaxPercent { get; init; }
    public required MetricTriple LoadToolNetworkRxBytesDelta { get; init; }
    public required MetricTriple LoadToolNetworkTxBytesDelta { get; init; }
    public DockerCleanupSummary? DockerCleanup { get; init; }
    public string? NetworkCleanupStatus { get; init; }
    public required int Connections { get; init; }
    public required int StreamsPerConnection { get; init; }
    public int? EffectiveConcurrency { get; init; }
    public int? EffectiveStreamsPerConnection { get; init; }
    public required int DurationSeconds { get; init; }
    public required int WarmupSeconds { get; init; }
    public required string NetworkProfile { get; init; }
    public required int Repetitions { get; init; }
    public BenchmarkEvidenceAssessment? Evidence { get; init; }
    public required ValidationCounts Validation { get; init; }
    public required IReadOnlyDictionary<string, int> ProtocolProofStatuses { get; init; }
    public required IReadOnlyDictionary<string, int> BenchmarkExecutionStatuses { get; init; }
    public required IReadOnlyDictionary<string, int> LoadToolH3CapabilityStatuses { get; init; }
    public IReadOnlyList<string> FailureReasons { get; init; } = [];
    public long FailedRequests { get; init; }
    public long TimeoutRequests { get; init; }
    public required int ParsedMetricsCount { get; init; }
    public required int WarningCount { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public required int ErrorCount { get; init; }
    public required int SaturationWarningCount { get; init; }
    public int TargetProcessMetricsCapturedCount { get; init; }
    public int TargetProcessMetricsMissingCount { get; init; }
    public int TargetProcessMetricsSampleCountMedian { get; init; }
    public IReadOnlyList<string> TargetProcessMetricsWarnings { get; init; } = [];
    public int? TargetProcessId { get; init; }
    public string? TargetCommandLine { get; init; }
    public DateTimeOffset? TargetStartTimeUtc { get; init; }
    public DateTimeOffset? TargetReadyTimeUtc { get; init; }
    public int? TargetExitCode { get; init; }
    public int? DiagnosticProcessId { get; init; }
    public string? DiagnosticConfidence { get; init; }
    public string? DiagnosticResolutionStrategy { get; init; }
    public required IReadOnlyDictionary<string, int> CountersCaptureStatuses { get; init; }
    public int CountersCapturedCount { get; init; }
    public int CountersMissingCount { get; init; }
    public required MetricTriple CounterCpuMean { get; init; }
    public required MetricTriple CounterCpuMax { get; init; }
    public required MetricTriple CounterAllocationRateMean { get; init; }
    public required MetricTriple CounterGen0CollectionsDelta { get; init; }
    public required MetricTriple CounterGen1CollectionsDelta { get; init; }
    public required MetricTriple CounterGen2CollectionsDelta { get; init; }
    public required MetricTriple CounterThreadPoolQueueLengthMax { get; init; }
    public required MetricTriple CounterExceptionRateMean { get; init; }
    public string? QlogDirectory { get; init; }
    public int QlogAvailableCount { get; init; }
    public int QlogMissingCount { get; init; }
    public int? QlogFileCountMedian { get; init; }
    public required MetricTriple RequestsPerSecond { get; init; }
    public required MetricTriple LatencyP50Ms { get; init; }
    public required MetricTriple LatencyP95Ms { get; init; }
    public required MetricTriple LatencyP99Ms { get; init; }
    public required MetricTriple LatencyMeanMs { get; init; }
    public required MetricTriple ThroughputBytesPerSecond { get; init; }
    public IReadOnlyList<string> SaturationWarnings { get; init; } = [];
}

public sealed record RunTotals(
    int ResultCount,
    int AggregateCount,
    ValidationCounts Validation,
    int BenchmarkAttemptCount,
    int ParsedMetricsCount,
    int WarningCount,
    int ErrorCount,
    int SaturationWarningCount,
    int ComparableLocalCount,
    int ComparableWithWarningsCount,
    int NotComparableCount,
    int InvalidCount,
    long FailedRequests,
    long TimeoutRequests,
    int TargetProcessMetricsCapturedCount,
    int TargetProcessMetricsMissingCount,
    int TargetDockerMetricsCapturedCount,
    int TargetDockerMetricsMissingCount,
    int CountersCapturedCount,
    int CountersMissingCount,
    int LoadToolDockerMetricsCapturedCount,
    int LoadToolDockerMetricsMissingCount,
    int DockerCleanupFailureCount);

public sealed record RunDescriptor(
    string RunId,
    DateTimeOffset GeneratedAt,
    RunMetadata? Metadata,
    RunTotals Totals);

public sealed record RunReport(
    string RunId,
    DateTimeOffset GeneratedAt,
    RunMetadata? Metadata,
    RunTotals Totals,
    IReadOnlyList<RunAggregate> Aggregates);

public static class RunReportBuilder
{
    public static RunReport Build(
        string runId,
        DateTimeOffset generatedAt,
        RunMetadata? metadata,
        IReadOnlyList<BenchmarkResult> results)
    {
        var aggregates = results
            .GroupBy(static result => new RunGroupKey(
                result.ImplementationId,
                result.ScenarioId,
                result.Protocol,
                result.LoadTool,
                result.LoadToolMode,
                result.LoadToolCategory,
                result.LoadProfileId,
                result.TargetExecutionMode,
                result.TargetContract,
                result.Connections,
                result.StreamsPerConnection,
                result.DurationSeconds,
                result.WarmupSeconds,
                result.NetworkProfile))
            .Select(BuildAggregate)
            .OrderBy(static aggregate => aggregate.ImplementationId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static aggregate => aggregate.ScenarioId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static aggregate => aggregate.Protocol, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static aggregate => aggregate.TargetExecutionMode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static aggregate => aggregate.Connections)
            .ThenBy(static aggregate => aggregate.StreamsPerConnection)
            .ThenBy(static aggregate => aggregate.DurationSeconds)
            .ThenBy(static aggregate => aggregate.WarmupSeconds)
            .ThenBy(static aggregate => aggregate.NetworkProfile, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var comparableLocalCount = aggregates.Count(static aggregate =>
            string.Equals(aggregate.Evidence?.ComparabilityStatus, BenchmarkComparabilityStatuses.ComparableLocal, StringComparison.OrdinalIgnoreCase));
        var comparableWithWarningsCount = aggregates.Count(static aggregate =>
            string.Equals(aggregate.Evidence?.ComparabilityStatus, BenchmarkComparabilityStatuses.ComparableWithWarnings, StringComparison.OrdinalIgnoreCase));
        var notComparableCount = aggregates.Count(static aggregate =>
            string.Equals(aggregate.Evidence?.ComparabilityStatus, BenchmarkComparabilityStatuses.NotComparable, StringComparison.OrdinalIgnoreCase));
        var invalidCount = aggregates.Count(static aggregate =>
            string.Equals(aggregate.Evidence?.ComparabilityStatus, BenchmarkComparabilityStatuses.Invalid, StringComparison.OrdinalIgnoreCase));
        var failedRequests = results.Sum(static result => result.Metrics.FailedRequests.GetValueOrDefault());
        var timeoutRequests = results.Sum(static result => result.Metrics.TimeoutRequests.GetValueOrDefault());
        var targetProcessMetricsCapturedCount = results.Count(static result => result.TargetProcessMetrics is not null);
        var targetProcessMetricsMissingCount = results.Count(static result => result.TargetProcessMetrics is null);
        var targetDockerMetricsCapturedCount = results.Count(static result => result.TargetDockerMetricsAvailable);
        var targetDockerMetricsMissingCount = results.Count(static result => string.Equals(result.TargetExecutionMode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase) && !result.TargetDockerMetricsAvailable);
        var warningCount = results
            .SelectMany(static result => result.Warnings
                .Concat(result.LoadShapeWarnings)
                .Concat(result.LoadToolH3CapabilityWarnings)
                .Concat(result.TargetProcessMetrics?.Warnings ?? [])
                .Concat(result.DiagnosticTarget?.Warnings ?? [])
                .Concat(result.CountersSummary?.ParseWarnings ?? [])
                .Concat(result.TargetDockerMetricsSummary?.ParseWarnings ?? [])
                .Concat(result.TargetSaturationWarnings)
                .Concat(result.LoadToolDockerMetricsSummary?.ParseWarnings ?? [])
                .Concat(result.LoadToolSaturationWarnings)
                .Concat(result.Evidence?.ComparabilityWarnings ?? []))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var totals = new RunTotals(
            results.Count,
            aggregates.Length,
            BuildValidationCounts(results),
            results.Count(result =>
                result.BenchmarkExecutionStatus == LoadToolExecutionStatuses.Succeeded ||
                result.BenchmarkExecutionStatus == LoadToolExecutionStatuses.Failed),
            results.Count(result => result.ParsedMetricsAvailable),
            warningCount,
            results.Sum(result => result.Errors.Count),
            aggregates.Sum(aggregate => aggregate.SaturationWarningCount),
            comparableLocalCount,
            comparableWithWarningsCount,
            notComparableCount,
            invalidCount,
            failedRequests,
            timeoutRequests,
            targetProcessMetricsCapturedCount,
            targetProcessMetricsMissingCount,
            targetDockerMetricsCapturedCount,
            targetDockerMetricsMissingCount,
            results.Count(static result => result.CountersAvailable),
            results.Count(static result => !result.CountersAvailable),
            results.Count(static result => result.LoadToolDockerMetricsAvailable),
            results.Count(static result => string.Equals(result.LoadToolMode, LoadToolKinds.Docker, StringComparison.OrdinalIgnoreCase) && !result.LoadToolDockerMetricsAvailable),
            results.Count(static result => result.DockerCleanup?.Errors.Count > 0 ||
                result.DockerCleanup?.TargetContainerCleanupSucceeded == false ||
                result.DockerCleanup?.TargetMetricsSamplerCleanupSucceeded == false ||
                result.DockerCleanup?.LoadToolContainerCleanupSucceeded == false ||
                result.DockerCleanup?.LoadToolMetricsSamplerCleanupSucceeded == false ||
                result.DockerCleanup?.NetworkCleanupSucceeded == false));

        return new RunReport(runId, generatedAt, metadata, totals, aggregates);
    }

    public static RunDescriptor CreateDescriptor(RunReport report)
    {
        return new RunDescriptor(report.RunId, report.GeneratedAt, report.Metadata, report.Totals);
    }

    private static RunAggregate BuildAggregate(IGrouping<RunGroupKey, BenchmarkResult> group)
    {
        var results = group
            .OrderBy(static result => result.Repetition)
            .ToArray();
        var first = results[0];
        var validation = BuildValidationCounts(results);
        var parsedMetricsCount = results.Count(result => result.ParsedMetricsAvailable);
        var evidence = BenchmarkEvidenceEvaluator.AssessAggregate(results);
        var warnings = results
            .SelectMany(result => result.Warnings
                .Concat(result.LoadShapeWarnings)
                .Concat(result.LoadToolH3CapabilityWarnings)
                .Concat(result.TargetProcessMetrics?.Warnings ?? [])
                .Concat(result.DiagnosticTarget?.Warnings ?? [])
                .Concat(result.CountersSummary?.ParseWarnings ?? [])
                .Concat(result.TargetDockerMetricsSummary?.ParseWarnings ?? [])
                .Concat(result.TargetSaturationWarnings)
                .Concat(result.LoadToolDockerMetricsSummary?.ParseWarnings ?? [])
                .Concat(result.LoadToolSaturationWarnings)
                .Concat(result.Evidence?.ComparabilityWarnings ?? []))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var errors = results.Sum(result => result.Errors.Count);
        var failedRequests = results.Sum(result => result.Metrics.FailedRequests.GetValueOrDefault());
        var timeoutRequests = results.Sum(result => result.Metrics.TimeoutRequests.GetValueOrDefault());
        var qlogAvailableCount = results.Count(result => (result.QlogFileCount ?? 0) > 0);
        var qlogMissingCount = results.Count(result => (result.QlogFileCount ?? 0) <= 0);
        var qlogFileCountTriple = BuildIntegerMetricTriple(results.Select(result => result.QlogFileCount));
        var qlogFileCountMedian = qlogFileCountTriple.Median is null
            ? null
            : (int?)Math.Round(qlogFileCountTriple.Median.Value, MidpointRounding.AwayFromZero);
        var targetProcessMetricsCapturedCount = results.Count(result => result.TargetProcessMetrics is not null);
        var targetProcessMetricsMissingCount = results.Count(result => result.TargetProcessMetrics is null);
        var targetProcessSampleCountTriple = BuildIntegerMetricTriple(results.Select(result => result.TargetProcessMetrics?.Samples.Count));
        var targetProcessMetricsSampleCountMedian = targetProcessSampleCountTriple.Median is null
            ? 0
            : (int)Math.Round(targetProcessSampleCountTriple.Median.Value, MidpointRounding.AwayFromZero);
        var loadToolH3CapabilityStatuses = results
                .GroupBy(static result => result.LoadToolH3CapabilityStatus ?? "n/a")
                .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var countersCaptureStatuses = results
            .GroupBy(static result => result.CountersCaptureStatus ?? CounterCaptureStatuses.Disabled)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var targetProcessWarnings = results
            .SelectMany(result => result.TargetProcessMetrics?.Warnings ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var saturationWarnings = results
            .SelectMany(result => result.Warnings.Concat(result.LoadShapeWarnings).Concat(result.TargetSaturationWarnings).Concat(result.LoadToolSaturationWarnings).Concat(result.Evidence?.ComparabilityWarnings ?? []).Concat(result.Errors))
            .Where(IsSaturationWarning)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var targetDockerMetricsCapturedCount = results.Count(static result => result.TargetDockerMetricsAvailable);
        var targetDockerMetricsMissingCount = results.Count(static result => string.Equals(result.TargetExecutionMode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase) && !result.TargetDockerMetricsAvailable);
        var targetDockerSampleCountTriple = BuildIntegerMetricTriple(results.Select(static result => result.TargetDockerMetricsSummary?.Samples.Count));
        var targetDockerMetricsSampleCountMedian = targetDockerSampleCountTriple.Median is null
            ? 0
            : (int)Math.Round(targetDockerSampleCountTriple.Median.Value, MidpointRounding.AwayFromZero);
        var loadToolMetricsCapturedCount = results.Count(static result => result.LoadToolDockerMetricsAvailable);
        var loadToolMetricsMissingCount = results.Count(static result => string.Equals(result.LoadToolMode, LoadToolKinds.Docker, StringComparison.OrdinalIgnoreCase) && !result.LoadToolDockerMetricsAvailable);
        var loadToolSampleCountTriple = BuildIntegerMetricTriple(results.Select(static result => result.LoadToolDockerMetricsSummary?.Samples.Count));
        var loadToolMetricsSampleCountMedian = loadToolSampleCountTriple.Median is null
            ? 0
            : (int)Math.Round(loadToolSampleCountTriple.Median.Value, MidpointRounding.AwayFromZero);

        return new RunAggregate
        {
            ImplementationId = first.ImplementationId,
            ImplementationName = first.ImplementationName,
            ScenarioId = first.ScenarioId,
            ScenarioName = first.ScenarioName,
            Family = first.Family,
            Protocol = first.Protocol,
            Role = first.Role,
            LoadProfileId = first.LoadProfileId,
            LoadProfileTitle = first.LoadProfileTitle,
            LoadProfilePurpose = first.LoadProfilePurpose,
            LoadTool = first.LoadTool,
            LoadToolMode = first.LoadToolMode,
            LoadToolCategory = first.LoadToolCategory,
            TargetExecutionMode = first.TargetExecutionMode,
            TargetContract = first.TargetContract,
            TargetDockerImage = first.TargetDockerImage,
            TargetContainerName = first.TargetContainerName,
            TargetDockerNetwork = first.TargetDockerNetwork,
            TargetDockerNetworkName = first.TargetDockerNetworkName,
            TargetDockerNetworkId = first.TargetDockerNetworkId,
            TargetDockerNetworkMode = first.TargetDockerNetworkMode,
            TargetNetworkAliases = first.TargetNetworkAliases,
            TargetPublishedPorts = first.TargetPublishedPorts,
            AdapterControlPlaneBaseUrl = first.AdapterControlPlaneBaseUrl,
            AdapterSessionId = first.AdapterSessionId,
            AdapterEndpointTypes = first.AdapterEndpointTypes,
            TargetDockerResourceLimitsRequested = first.TargetDockerResourceLimitsRequested,
            TargetDockerResourceLimitsEffective = first.TargetDockerResourceLimitsEffective,
            TargetDockerMetricsCapturedCount = targetDockerMetricsCapturedCount,
            TargetDockerMetricsMissingCount = targetDockerMetricsMissingCount,
            TargetDockerMetricsSampleCountMedian = targetDockerMetricsSampleCountMedian,
            TargetSaturationStatuses = results
                .GroupBy(static result => result.TargetSaturationStatus ?? "n/a")
                .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase),
            TargetCpuMeanPercent = BuildMetricTriple(results.Select(static result => result.TargetDockerMetricsSummary?.CpuMeanPercent), higherIsBetter: false),
            TargetCpuMaxPercent = BuildMetricTriple(results.Select(static result => result.TargetDockerMetricsSummary?.CpuMaxPercent), higherIsBetter: false),
            TargetMemoryMaxBytes = BuildLongMetricTriple(results.Select(static result => result.TargetDockerMetricsSummary?.MemoryMaxBytes), higherIsBetter: false),
            TargetMemoryMaxPercent = BuildMetricTriple(results.Select(static result => result.TargetDockerMetricsSummary?.MemoryMaxPercent), higherIsBetter: false),
            TargetNetworkRxBytesDelta = BuildLongMetricTriple(results.Select(static result => result.TargetDockerMetricsSummary?.NetworkRxBytesDelta), higherIsBetter: false),
            TargetNetworkTxBytesDelta = BuildLongMetricTriple(results.Select(static result => result.TargetDockerMetricsSummary?.NetworkTxBytesDelta), higherIsBetter: false),
            LoadToolDockerResourceLimitsRequested = first.LoadToolDockerResourceLimitsRequested,
            LoadToolDockerResourceLimitsEffective = first.LoadToolDockerResourceLimitsEffective,
            LoadToolDockerMetricsCapturedCount = loadToolMetricsCapturedCount,
            LoadToolDockerMetricsMissingCount = loadToolMetricsMissingCount,
            LoadToolDockerMetricsSampleCountMedian = loadToolMetricsSampleCountMedian,
            LoadToolSaturationStatuses = results
                .GroupBy(static result => result.LoadToolSaturationStatus ?? "n/a")
                .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase),
            LoadToolCpuMeanPercent = BuildMetricTriple(results.Select(static result => result.LoadToolDockerMetricsSummary?.CpuMeanPercent), higherIsBetter: false),
            LoadToolCpuMaxPercent = BuildMetricTriple(results.Select(static result => result.LoadToolDockerMetricsSummary?.CpuMaxPercent), higherIsBetter: false),
            LoadToolMemoryMaxBytes = BuildLongMetricTriple(results.Select(static result => result.LoadToolDockerMetricsSummary?.MemoryMaxBytes), higherIsBetter: false),
            LoadToolMemoryMaxPercent = BuildMetricTriple(results.Select(static result => result.LoadToolDockerMetricsSummary?.MemoryMaxPercent), higherIsBetter: false),
            LoadToolNetworkRxBytesDelta = BuildLongMetricTriple(results.Select(static result => result.LoadToolDockerMetricsSummary?.NetworkRxBytesDelta), higherIsBetter: false),
            LoadToolNetworkTxBytesDelta = BuildLongMetricTriple(results.Select(static result => result.LoadToolDockerMetricsSummary?.NetworkTxBytesDelta), higherIsBetter: false),
            DockerCleanup = first.DockerCleanup,
            NetworkCleanupStatus = first.NetworkCleanupStatus,
            Connections = first.Connections,
            StreamsPerConnection = first.StreamsPerConnection,
            EffectiveConcurrency = first.EffectiveLoadShape?.Concurrency,
            EffectiveStreamsPerConnection = first.EffectiveLoadShape?.StreamsPerConnection,
            DurationSeconds = first.DurationSeconds,
            WarmupSeconds = first.WarmupSeconds,
            NetworkProfile = first.NetworkProfile,
            Repetitions = results.Length,
            Evidence = evidence,
            Validation = validation,
            ProtocolProofStatuses = results
                .GroupBy(static result => result.ProtocolProof?.Status.ToString().ToLowerInvariant() ?? "not-required")
                .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase),
            BenchmarkExecutionStatuses = results
                .GroupBy(static result => result.BenchmarkExecutionStatus)
                .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase),
            LoadToolH3CapabilityStatuses = loadToolH3CapabilityStatuses,
            FailureReasons = results
                .Select(static result => result.BenchmarkFailureReason)
                .Where(static reason => !string.IsNullOrWhiteSpace(reason))
                .Select(static reason => reason!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            FailedRequests = failedRequests,
            TimeoutRequests = timeoutRequests,
            ParsedMetricsCount = parsedMetricsCount,
            WarningCount = warnings.Length,
            Warnings = results
                .SelectMany(static result => result.Warnings
                    .Concat(result.LoadShapeWarnings)
                    .Concat(result.LoadToolH3CapabilityWarnings)
                    .Concat(result.TargetProcessMetrics?.Warnings ?? [])
                    .Concat(result.DiagnosticTarget?.Warnings ?? [])
                    .Concat(result.CountersSummary?.ParseWarnings ?? [])
                    .Concat(result.TargetDockerMetricsSummary?.ParseWarnings ?? [])
                    .Concat(result.TargetSaturationWarnings)
                    .Concat(result.LoadToolDockerMetricsSummary?.ParseWarnings ?? [])
                    .Concat(result.LoadToolSaturationWarnings)
                    .Concat(result.Evidence?.ComparabilityWarnings ?? []))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ErrorCount = errors,
            SaturationWarningCount = saturationWarnings.Length,
            SaturationWarnings = saturationWarnings,
            TargetProcessMetricsCapturedCount = targetProcessMetricsCapturedCount,
            TargetProcessMetricsMissingCount = targetProcessMetricsMissingCount,
            TargetProcessMetricsSampleCountMedian = targetProcessMetricsSampleCountMedian,
            TargetProcessMetricsWarnings = targetProcessWarnings,
            TargetProcessId = first.TargetProcessId,
            TargetCommandLine = first.TargetCommandLine,
            TargetStartTimeUtc = first.TargetStartTimeUtc,
            TargetReadyTimeUtc = first.TargetReadyTimeUtc,
            TargetExitCode = first.TargetExitCode,
            DiagnosticProcessId = first.DiagnosticTarget?.ResolvedProcessId,
            DiagnosticConfidence = first.DiagnosticTarget?.Confidence,
            DiagnosticResolutionStrategy = first.DiagnosticTarget?.ResolutionStrategy,
            CountersCaptureStatuses = countersCaptureStatuses,
            CountersCapturedCount = results.Count(static result => result.CountersAvailable),
            CountersMissingCount = results.Count(static result => !result.CountersAvailable),
            CounterCpuMean = BuildMetricTriple(results.Select(result => result.CountersSummary?.CpuMean), higherIsBetter: false),
            CounterCpuMax = BuildMetricTriple(results.Select(result => result.CountersSummary?.CpuMax), higherIsBetter: false),
            CounterAllocationRateMean = BuildMetricTriple(results.Select(result => result.CountersSummary?.AllocationRateMean), higherIsBetter: false),
            CounterGen0CollectionsDelta = BuildMetricTriple(results.Select(result => result.CountersSummary?.Gen0CollectionsDelta), higherIsBetter: false),
            CounterGen1CollectionsDelta = BuildMetricTriple(results.Select(result => result.CountersSummary?.Gen1CollectionsDelta), higherIsBetter: false),
            CounterGen2CollectionsDelta = BuildMetricTriple(results.Select(result => result.CountersSummary?.Gen2CollectionsDelta), higherIsBetter: false),
            CounterThreadPoolQueueLengthMax = BuildMetricTriple(results.Select(result => result.CountersSummary?.ThreadPoolQueueLengthMax), higherIsBetter: false),
            CounterExceptionRateMean = BuildMetricTriple(results.Select(result => result.CountersSummary?.ExceptionRateMean), higherIsBetter: false),
            QlogDirectory = first.QlogDirectory,
            QlogAvailableCount = qlogAvailableCount,
            QlogMissingCount = qlogMissingCount,
            QlogFileCountMedian = qlogFileCountMedian,
            RequestsPerSecond = BuildMetricTriple(results.Select(result => result.Metrics.RequestsPerSecond), higherIsBetter: true),
            LatencyP50Ms = BuildMetricTriple(results.Select(result => result.Metrics.LatencyP50Ms), higherIsBetter: false),
            LatencyP95Ms = BuildMetricTriple(results.Select(result => result.Metrics.LatencyP95Ms), higherIsBetter: false),
            LatencyP99Ms = BuildMetricTriple(results.Select(result => result.Metrics.LatencyP99Ms), higherIsBetter: false),
            LatencyMeanMs = BuildMetricTriple(results.Select(result => result.Metrics.LatencyMeanMs), higherIsBetter: false),
            ThroughputBytesPerSecond = BuildMetricTriple(results.Select(result => result.Metrics.ThroughputBytesPerSecond), higherIsBetter: true)
        };
    }

    private static ValidationCounts BuildValidationCounts(IEnumerable<BenchmarkResult> results)
    {
        var passed = 0;
        var failed = 0;
        var unsupported = 0;
        var notApplicable = 0;
        var inconclusive = 0;
        var infrastructureFailure = 0;

        foreach (var result in results)
        {
            switch (result.ValidationResult.Status)
            {
                case ValidationStatus.Passed:
                    passed++;
                    break;
                case ValidationStatus.Failed:
                    failed++;
                    break;
                case ValidationStatus.Unsupported:
                    unsupported++;
                    break;
                case ValidationStatus.NotApplicable:
                    notApplicable++;
                    break;
                case ValidationStatus.Inconclusive:
                    inconclusive++;
                    break;
                case ValidationStatus.InfrastructureFailure:
                    infrastructureFailure++;
                    break;
            }
        }

        return new ValidationCounts(passed, failed, unsupported, notApplicable, inconclusive, infrastructureFailure);
    }

    private static MetricTriple BuildMetricTriple(IEnumerable<double?> values, bool higherIsBetter)
    {
        var samples = values
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .OrderBy(value => value)
            .ToArray();

        if (samples.Length == 0)
        {
            return new MetricTriple(null, null, null);
        }

        var median = samples.Length % 2 == 1
            ? samples[samples.Length / 2]
            : (samples[(samples.Length / 2) - 1] + samples[samples.Length / 2]) / 2.0;

        var best = higherIsBetter ? samples[^1] : samples[0];
        var worst = higherIsBetter ? samples[0] : samples[^1];

        return new MetricTriple(median, best, worst);
    }

    private static MetricTriple BuildIntegerMetricTriple(IEnumerable<int?> values)
    {
        var samples = values
            .Where(value => value.HasValue)
            .Select(value => (double)value!.Value)
            .OrderBy(value => value)
            .ToArray();

        if (samples.Length == 0)
        {
            return new MetricTriple(null, null, null);
        }

        var median = samples.Length % 2 == 1
            ? samples[samples.Length / 2]
            : (samples[(samples.Length / 2) - 1] + samples[samples.Length / 2]) / 2.0d;

        return new MetricTriple(median, samples[^1], samples[0]);
    }

    private static MetricTriple BuildLongMetricTriple(IEnumerable<long?> values, bool higherIsBetter)
    {
        return BuildMetricTriple(values.Select(static value => value.HasValue ? (double?)value.Value : null), higherIsBetter);
    }

    private static bool IsSaturationWarning(string message)
    {
        return message.Contains("saturat", StringComparison.OrdinalIgnoreCase) ||
            message.Contains(BenchmarkEvidenceReasons.UnstableResult, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record RunGroupKey(
        string ImplementationId,
        string ScenarioId,
        string Protocol,
        string? LoadTool,
        string? LoadToolMode,
        string? LoadToolCategory,
        string? LoadProfileId,
        string? TargetExecutionMode,
        string? TargetContract,
        int Connections,
        int StreamsPerConnection,
        int DurationSeconds,
        int WarmupSeconds,
        string NetworkProfile);
}
