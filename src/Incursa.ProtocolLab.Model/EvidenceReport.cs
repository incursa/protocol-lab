// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public sealed record EvidenceReportIdentity
{
    public string RunId { get; init; } = "";
    public DateTimeOffset Timestamp { get; init; }
    public string? SuiteId { get; init; }
    public string? SuiteTitle { get; init; }
    public string? GitCommit { get; init; }
    public string? HostName { get; init; }
    public string? OperatingSystem { get; init; }
    public string? FrameworkDescription { get; init; }
    public string? ProcessArchitecture { get; init; }
    public string? OperatingSystemArchitecture { get; init; }
    public int ProcessorCount { get; init; }
    public long? TotalAvailableMemoryBytes { get; init; }
    public string? ExecutionMode { get; init; }
    public string? EvidenceTier { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed record EvidenceReportMatrixSummary
{
    public int TotalCells { get; init; }
    public int SupportedCells { get; init; }
    public int UnsupportedCells { get; init; }
    public int MissingCapability { get; init; }
    public int MissingLoadTool { get; init; }
    public int IncompatibleTrafficShape { get; init; }
    public int IncompatibleLoadProfile { get; init; }
    public int ExperimentalDisabled { get; init; }
    public int PlaceholderNotRunnable { get; init; }
    public int OtherUnsupported { get; init; }
    public IReadOnlyList<string> UnsupportedCellDetails { get; init; } = [];
}

public sealed record EvidenceReportValidationSummary
{
    public int Passed { get; init; }
    public int Failed { get; init; }
    public int Unsupported { get; init; }
    public int NotApplicable { get; init; }
    public int Inconclusive { get; init; }
    public int InfrastructureFailure { get; init; }
    public IReadOnlyList<EvidenceReportValidationProof> ProofArtifacts { get; init; } = [];
}

public sealed record EvidenceReportValidationProof
{
    public string CellKey { get; init; } = "";
    public string Status { get; init; } = "";
    public string? ProofDirectory { get; init; }
    public IReadOnlyList<string> Artifacts { get; init; } = [];
}

public sealed record EvidenceReportBenchmarkAcceptance
{
    public int AcceptedBenchmarks { get; init; }
    public int RejectedBenchmarks { get; init; }
    public int NotRunValidationFailed { get; init; }
    public int NotRunUnsupported { get; init; }
    public int NotRunLoadToolFailed { get; init; }
    public int NotRunParserFailed { get; init; }
    public IReadOnlyList<EvidenceReportBenchmarkItem> AcceptedDetails { get; init; } = [];
    public IReadOnlyList<EvidenceReportBenchmarkItem> RejectedDetails { get; init; } = [];
    public IReadOnlyList<EvidenceReportBenchmarkItem> NotRunDetails { get; init; } = [];
}

public sealed record EvidenceReportBenchmarkItem
{
    public string ImplementationId { get; init; } = "";
    public string ScenarioId { get; init; } = "";
    public string Protocol { get; init; } = "";
    public int Connections { get; init; }
    public int StreamsPerConnection { get; init; }
    public int Repetition { get; init; }
    public string BenchmarkStatus { get; init; } = "";
    public string? Reason { get; init; }
    public string? EvidenceTier { get; init; }
}

public sealed record EvidenceReportCellValidation
{
    public string Status { get; init; } = "";
    public string Summary { get; init; } = "";
    public string? ProvenProtocol { get; init; }
    public string? ProtocolProofMethod { get; init; }
    public IReadOnlyList<ValidationObservation> Observations { get; init; } = [];
    public IReadOnlyList<ValidationProofArtifact> ProofArtifacts { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
}

public sealed record EvidenceReportCellBenchmark
{
    public string Status { get; init; } = "";
    public string? FailureReason { get; init; }
    public bool ParsedMetricsAvailable { get; init; }
    public string? EvidenceClass { get; init; }
    public string? ComparabilityStatus { get; init; }
    public IReadOnlyList<string> EvidenceReasons { get; init; } = [];
    public IReadOnlyList<string> ComparabilityWarnings { get; init; } = [];
}

public sealed record EvidenceReportMeasurement
{
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public string Unit { get; init; } = "";
    public string Source { get; init; } = "";
    public double Value { get; init; }
    public string? Statistic { get; init; }
    public bool? HigherIsBetter { get; init; }
}

public sealed record EvidenceReportCellArtifact
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public bool Exists { get; init; }
}

public sealed record EvidenceReportCell
{
    public string CellKey { get; init; } = "";
    public string ImplementationId { get; init; } = "";
    public string ImplementationName { get; init; } = "";
    public string ScenarioId { get; init; } = "";
    public string ScenarioName { get; init; } = "";
    public string Family { get; init; } = "";
    public string Protocol { get; init; } = "";
    public string? RequestedProtocol { get; init; }
    public string? ProvenProtocol { get; init; }
    public string Role { get; init; } = "";
    public string ExecutionProfile { get; init; } = "";
    public string? LoadProfileId { get; init; }
    public string? LoadProfileTitle { get; init; }
    public string? LoadProfilePurpose { get; init; }
    public string? LoadTool { get; init; }
    public string? LoadToolMode { get; init; }
    public string? LoadToolCategory { get; init; }
    public string? TargetExecutionMode { get; init; }
    public string? TargetContract { get; init; }
    public int DurationSeconds { get; init; }
    public int WarmupSeconds { get; init; }
    public int Repetition { get; init; }
    public int Connections { get; init; }
    public int StreamsPerConnection { get; init; }
    public string NetworkProfile { get; init; } = "";
    public EvidenceReportCellValidation Validation { get; init; } = new();
    public EvidenceReportCellBenchmark Benchmark { get; init; } = new();
    public IReadOnlyList<EvidenceReportMeasurement> Measurements { get; init; } = [];
    public IReadOnlyList<EvidenceReportCellArtifact> Artifacts { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
}

public sealed record EvidenceReportComparisonGroup
{
    public string ScenarioId { get; init; } = "";
    public string ScenarioName { get; init; } = "";
    public string Family { get; init; } = "";
    public string Protocol { get; init; } = "";
    public string LoadProfileId { get; init; } = "";
    public string LoadProfileTitle { get; init; } = "";
    public string LoadTool { get; init; } = "";
    public string ExecutionBackend { get; init; } = "";
    public string EvidenceTier { get; init; } = "";
    public int Connections { get; init; }
    public int StreamsPerConnection { get; init; }
    public int DurationSeconds { get; init; }
    public string NetworkProfile { get; init; } = "";
    public IReadOnlyList<EvidenceReportComparisonEntry> Entries { get; init; } = [];
    public IReadOnlyList<string> ComparabilityWarnings { get; init; } = [];
}

public sealed record EvidenceReportComparisonEntry
{
    public string ImplementationId { get; init; } = "";
    public string ImplementationName { get; init; } = "";
    public string? LoadToolMode { get; init; }
    public string? ProvenProtocol { get; init; }
    public double? RequestsPerSecondMedian { get; init; }
    public double? LatencyP50MsMedian { get; init; }
    public double? LatencyP95MsMedian { get; init; }
    public double? LatencyP99MsMedian { get; init; }
    public double? LatencyMeanMsMedian { get; init; }
    public double? ThroughputBytesPerSecondMedian { get; init; }
    public int Repetitions { get; init; }
    public string ValidationStatus { get; init; } = "";
    public string BenchmarkStatus { get; init; } = "";
    public string EvidenceClass { get; init; } = "";
    public string ComparabilityStatus { get; init; } = "";
}

public sealed record EvidenceReportWarning
{
    public string Category { get; init; } = "";
    public string Message { get; init; } = "";
    public string? Context { get; init; }
}

public sealed record EvidenceReportArtifactEntry
{
    public string CellKey { get; init; } = "";
    public string CellDirectory { get; init; } = "";
    public IReadOnlyList<EvidenceReportArtifactFile> Files { get; init; } = [];
}

public sealed record EvidenceReportArtifactFile
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public bool Exists { get; init; }
}

public sealed record EvidenceReport
{
    public string SchemaVersion { get; init; } = "protocol-lab.evidence-report.v1";
    public string RunId { get; init; } = "";
    public DateTimeOffset GeneratedAt { get; init; }
    public EvidenceReportIdentity Identity { get; init; } = new();
    public EvidenceReportMatrixSummary MatrixSummary { get; init; } = new();
    public EvidenceReportValidationSummary ValidationSummary { get; init; } = new();
    public EvidenceReportBenchmarkAcceptance BenchmarkAcceptance { get; init; } = new();
    public IReadOnlyList<EvidenceReportCell> Cells { get; init; } = [];
    public IReadOnlyList<EvidenceReportComparisonGroup> ComparisonGroups { get; init; } = [];
    public IReadOnlyList<EvidenceReportWarning> Warnings { get; init; } = [];
    public IReadOnlyList<EvidenceReportArtifactEntry> ArtifactIndex { get; init; } = [];
}

public static class EvidenceReportBuilder
{
    public static EvidenceReport Build(
        string runId,
        DateTimeOffset generatedAt,
        RunMetadata? metadata,
        string? suiteId,
        string? suiteTitle,
        IReadOnlyList<RunCell> cells,
        IReadOnlyList<RunCellCompatibility> compatibilities,
        IReadOnlyList<BenchmarkResult> results)
    {
        var identity = BuildIdentity(runId, metadata, suiteId, suiteTitle);
        var matrixSummary = BuildMatrixSummary(cells, compatibilities);
        var validationSummary = BuildValidationSummary(results);
        var benchmarkAcceptance = BuildBenchmarkAcceptance(results);
        var reportCells = BuildCells(results);
        var comparisonGroups = BuildComparisonGroups(results);
        var warnings = BuildWarnings(cells, compatibilities, results);
        var artifactIndex = BuildArtifactIndex(results);

        return new EvidenceReport
        {
            RunId = runId,
            GeneratedAt = generatedAt,
            Identity = identity,
            MatrixSummary = matrixSummary,
            ValidationSummary = validationSummary,
            BenchmarkAcceptance = benchmarkAcceptance,
            Cells = reportCells,
            ComparisonGroups = comparisonGroups,
            Warnings = warnings,
            ArtifactIndex = artifactIndex
        };
    }

    private static EvidenceReportIdentity BuildIdentity(
        string runId,
        RunMetadata? metadata,
        string? suiteId,
        string? suiteTitle)
    {
        var identityWarnings = new List<string>();

        var evidenceTier = "local-lab";
        if (metadata is not null)
        {
            identityWarnings.Add("Shared-host local run; not publishable benchmark evidence.");
        }

        return new EvidenceReportIdentity
        {
            RunId = runId,
            Timestamp = metadata?.TimestampUtc ?? DateTimeOffset.UtcNow,
            SuiteId = suiteId,
            SuiteTitle = suiteTitle,
            GitCommit = metadata?.GitCommit,
            HostName = metadata?.HostName ?? "unknown",
            OperatingSystem = metadata?.OperatingSystem ?? "unknown",
            FrameworkDescription = metadata?.FrameworkDescription ?? "unknown",
            ProcessArchitecture = metadata?.ProcessArchitecture ?? "unknown",
            OperatingSystemArchitecture = metadata?.OperatingSystemArchitecture ?? "unknown",
            ProcessorCount = metadata?.ProcessorCount ?? 0,
            TotalAvailableMemoryBytes = metadata?.TotalAvailableMemoryBytes,
            ExecutionMode = "process",
            EvidenceTier = evidenceTier,
            Warnings = identityWarnings
        };
    }

    private static EvidenceReportMatrixSummary BuildMatrixSummary(
        IReadOnlyList<RunCell> cells,
        IReadOnlyList<RunCellCompatibility> compatibilities)
    {
        var supported = 0;
        var missingCapability = 0;
        var missingLoadTool = 0;
        var incompatibleTrafficShape = 0;
        var incompatibleLoadProfile = 0;
        var experimentalDisabled = 0;
        var placeholderNotRunnable = 0;
        var otherUnsupported = 0;
        var unsupportedDetails = new List<string>();

        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            var compatibility = i < compatibilities.Count ? compatibilities[i] : null;
            var cellLabel = $"{cell.Implementation.Id}/{cell.Scenario.Id}/{cell.Protocol} c{cell.Connections}-s{cell.StreamsPerConnection}-r{cell.Repetition}";

            if (compatibility is not null && compatibility.CanRun)
            {
                supported++;
            }
            else if (compatibility is not null)
            {
                unsupportedDetails.Add($"{cellLabel}: {compatibility.Status} ({compatibility.Reason})");

                switch (compatibility.Status.ToLowerInvariant())
                {
                    case "missing-capability":
                        missingCapability++;
                        break;
                    case "missing-load-tool":
                        missingLoadTool++;
                        break;
                    case "incompatible-traffic-shape":
                        incompatibleTrafficShape++;
                        break;
                    case "incompatible-load-profile":
                    case "experimental-profile-not-enabled":
                        incompatibleLoadProfile++;
                        break;
                    case "experimental-not-enabled":
                        experimentalDisabled++;
                        break;
                    case "placeholder-not-runnable":
                        placeholderNotRunnable++;
                        break;
                    default:
                        otherUnsupported++;
                        break;
                }
            }
            else
            {
                otherUnsupported++;
                unsupportedDetails.Add($"{cellLabel}: no compatibility information");
            }
        }

        var totalUnsupported = missingCapability + missingLoadTool + incompatibleTrafficShape +
            incompatibleLoadProfile + experimentalDisabled + placeholderNotRunnable + otherUnsupported;

        return new EvidenceReportMatrixSummary
        {
            TotalCells = cells.Count,
            SupportedCells = supported,
            UnsupportedCells = totalUnsupported,
            MissingCapability = missingCapability,
            MissingLoadTool = missingLoadTool,
            IncompatibleTrafficShape = incompatibleTrafficShape,
            IncompatibleLoadProfile = incompatibleLoadProfile,
            ExperimentalDisabled = experimentalDisabled,
            PlaceholderNotRunnable = placeholderNotRunnable,
            OtherUnsupported = otherUnsupported,
            UnsupportedCellDetails = unsupportedDetails
        };
    }

    private static EvidenceReportValidationSummary BuildValidationSummary(IReadOnlyList<BenchmarkResult> results)
    {
        var passed = 0;
        var failed = 0;
        var unsupported = 0;
        var notApplicable = 0;
        var inconclusive = 0;
        var infrastructureFailure = 0;
        var proofs = new List<EvidenceReportValidationProof>();

        foreach (var result in results)
        {
            var status = result.ValidationResult.Status;
            switch (status)
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

            var cellKey = BuildCellKey(result);
            var proofArtifacts = result.ValidationResult.ProofArtifacts
                .Select(pa => pa.Path)
                .ToArray();

            var resultJsonPath = GetArtifact(result, "resultJson");
            var proofDirectory = resultJsonPath is not null
                ? Path.Combine(Path.GetDirectoryName(resultJsonPath) ?? "", "validation-proof")
                : null;

            proofs.Add(new EvidenceReportValidationProof
            {
                CellKey = cellKey,
                Status = status.ToString(),
                ProofDirectory = proofDirectory,
                Artifacts = proofArtifacts
            });
        }

        return new EvidenceReportValidationSummary
        {
            Passed = passed,
            Failed = failed,
            Unsupported = unsupported,
            NotApplicable = notApplicable,
            Inconclusive = inconclusive,
            InfrastructureFailure = infrastructureFailure,
            ProofArtifacts = proofs
        };
    }

    private static EvidenceReportBenchmarkAcceptance BuildBenchmarkAcceptance(IReadOnlyList<BenchmarkResult> results)
    {
        var acceptedDetails = new List<EvidenceReportBenchmarkItem>();
        var rejectedDetails = new List<EvidenceReportBenchmarkItem>();
        var notRunDetails = new List<EvidenceReportBenchmarkItem>();
        var accepted = 0;
        var rejected = 0;
        var notRunValidationFailed = 0;
        var notRunUnsupported = 0;
        var notRunLoadToolFailed = 0;
        var notRunParserFailed = 0;

        foreach (var result in results)
        {
            var item = CreateBenchmarkItem(result);

            if (result.ValidationResult.Status == ValidationStatus.Unsupported ||
                result.ValidationResult.Status == ValidationStatus.NotApplicable)
            {
                item = item with { Reason = $"Validation: {result.ValidationResult.Status} - cell is unsupported" };
                notRunDetails.Add(item);
                notRunUnsupported++;
                continue;
            }

            if (result.ValidationResult.Status == ValidationStatus.Failed)
            {
                item = item with { Reason = $"Validation failed: {result.ValidationResult.Status}" };
                notRunDetails.Add(item);
                notRunValidationFailed++;
                continue;
            }

            if (result.ValidationResult.Status != ValidationStatus.Passed)
            {
                item = item with { Reason = $"Validation status: {result.ValidationResult.Status}" };
                notRunDetails.Add(item);
                notRunValidationFailed++;
                continue;
            }

            if (result.BenchmarkExecutionStatus == LoadToolExecutionStatuses.Skipped)
            {
                item = item with { Reason = "Benchmark not executed (unsupported)" };
                notRunDetails.Add(item);
                notRunUnsupported++;
                continue;
            }

            if (result.BenchmarkExecutionStatus == LoadToolExecutionStatuses.Failed)
            {
                item = item with { Reason = result.BenchmarkFailureReason ?? "Load tool failed" };
                notRunDetails.Add(item);
                notRunLoadToolFailed++;
                continue;
            }

            if (result.BenchmarkExecutionStatus == LoadToolExecutionStatuses.Succeeded && !result.ParsedMetricsAvailable)
            {
                item = item with { Reason = "Metrics parser failed" };
                notRunDetails.Add(item);
                notRunParserFailed++;
                continue;
            }

            if (result.ParsedMetricsAvailable &&
                result.Evidence?.ComparabilityStatus != BenchmarkComparabilityStatuses.Invalid &&
                result.ValidationResult.Status == ValidationStatus.Passed)
            {
                item = item with { EvidenceTier = result.Evidence?.EvidenceClass ?? "local-smoke" };
                acceptedDetails.Add(item);
                accepted++;
            }
            else
            {
                item = item with
                {
                    Reason = result.Evidence?.ComparabilityStatus == BenchmarkComparabilityStatuses.Invalid
                        ? "Invalid comparability status"
                        : "Did not pass acceptance criteria",
                    EvidenceTier = result.Evidence?.EvidenceClass
                };
                rejectedDetails.Add(item);
                rejected++;
            }
        }

        return new EvidenceReportBenchmarkAcceptance
        {
            AcceptedBenchmarks = accepted,
            RejectedBenchmarks = rejected,
            NotRunValidationFailed = notRunValidationFailed,
            NotRunUnsupported = notRunUnsupported,
            NotRunLoadToolFailed = notRunLoadToolFailed,
            NotRunParserFailed = notRunParserFailed,
            AcceptedDetails = acceptedDetails,
            RejectedDetails = rejectedDetails,
            NotRunDetails = notRunDetails
        };
    }

    private static EvidenceReportBenchmarkItem CreateBenchmarkItem(BenchmarkResult result)
    {
        return new EvidenceReportBenchmarkItem
        {
            ImplementationId = result.ImplementationId,
            ScenarioId = result.ScenarioId,
            Protocol = result.Protocol,
            Connections = result.Connections,
            StreamsPerConnection = result.StreamsPerConnection,
            Repetition = result.Repetition,
            BenchmarkStatus = result.BenchmarkExecutionStatus
        };
    }

    private static IReadOnlyList<EvidenceReportCell> BuildCells(IReadOnlyList<BenchmarkResult> results)
    {
        return results
            .Select(result =>
            {
                var cellKey = BuildCellKey(result);
                return new EvidenceReportCell
                {
                    CellKey = cellKey,
                    ImplementationId = result.ImplementationId,
                    ImplementationName = result.ImplementationName,
                    ScenarioId = result.ScenarioId,
                    ScenarioName = result.ScenarioName,
                    Family = result.Family,
                    Protocol = result.Protocol,
                    RequestedProtocol = result.RequestedProtocol,
                    ProvenProtocol = result.ProvenProtocol,
                    Role = result.Role,
                    ExecutionProfile = result.ExecutionProfile,
                    LoadProfileId = result.LoadProfileId,
                    LoadProfileTitle = result.LoadProfileTitle,
                    LoadProfilePurpose = result.LoadProfilePurpose,
                    LoadTool = result.LoadTool,
                    LoadToolMode = result.LoadToolMode,
                    LoadToolCategory = result.LoadToolCategory,
                    TargetExecutionMode = result.TargetExecutionMode,
                    TargetContract = result.TargetContract,
                    DurationSeconds = result.DurationSeconds,
                    WarmupSeconds = result.WarmupSeconds,
                    Repetition = result.Repetition,
                    Connections = result.Connections,
                    StreamsPerConnection = result.StreamsPerConnection,
                    NetworkProfile = result.NetworkProfile,
                    Validation = new EvidenceReportCellValidation
                    {
                        Status = result.ValidationResult.Status.ToString(),
                        Summary = result.ValidationResult.Summary,
                        ProvenProtocol = result.ProvenProtocol,
                        ProtocolProofMethod = result.ProtocolProof?.Method ?? result.ValidationResult.ProtocolProof?.Method,
                        Observations = result.ValidationResult.Observations,
                        ProofArtifacts = result.ValidationResult.ProofArtifacts,
                        Warnings = result.ValidationResult.Warnings,
                        Errors = result.ValidationResult.Errors
                    },
                    Benchmark = new EvidenceReportCellBenchmark
                    {
                        Status = result.BenchmarkExecutionStatus,
                        FailureReason = result.BenchmarkFailureReason,
                        ParsedMetricsAvailable = result.ParsedMetricsAvailable,
                        EvidenceClass = result.Evidence?.EvidenceClass,
                        ComparabilityStatus = result.Evidence?.ComparabilityStatus,
                        EvidenceReasons = result.Evidence?.EvidenceReasons ?? [],
                        ComparabilityWarnings = result.Evidence?.ComparabilityWarnings ?? []
                    },
                    Measurements = BuildMeasurements(result),
                    Artifacts = result.Artifacts
                        .OrderBy(artifact => artifact.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(artifact => new EvidenceReportCellArtifact
                        {
                            Name = artifact.Key,
                            Path = artifact.Value,
                            Exists = !string.IsNullOrWhiteSpace(artifact.Value)
                        })
                        .ToArray(),
                    Warnings = result.Warnings
                        .Concat(result.LoadShapeWarnings)
                        .Concat(result.TargetSaturationWarnings)
                        .Concat(result.LoadToolSaturationWarnings)
                        .Concat(result.TargetProcessMetrics?.Warnings ?? [])
                        .Concat(result.DiagnosticTarget?.Warnings ?? [])
                        .Concat(result.CountersSummary?.ParseWarnings ?? [])
                        .Concat(result.TargetDockerMetricsSummary?.ParseWarnings ?? [])
                        .Concat(result.LoadToolDockerMetricsSummary?.ParseWarnings ?? [])
                        .ToArray(),
                    Errors = result.Errors
                        .Concat(result.ValidationResult.Errors)
                        .Concat(result.DiagnosticTarget?.Errors ?? [])
                        .ToArray()
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<EvidenceReportMeasurement> BuildMeasurements(BenchmarkResult result)
    {
        var measurements = new List<EvidenceReportMeasurement>();

        AddMeasurement(measurements, "requestsPerSecond", "throughput", "requests/s", "load-tool", result.Metrics.RequestsPerSecond, "sample", true);
        AddMeasurement(measurements, "throughputBytesPerSecond", "throughput", "bytes/s", "load-tool", result.Metrics.ThroughputBytesPerSecond, "sample", true);
        AddMeasurement(measurements, "totalRequests", "quantity", "count", "load-tool", result.Metrics.TotalRequests, "sample", true);
        AddMeasurement(measurements, "successfulRequests", "quantity", "count", "load-tool", result.Metrics.SuccessfulRequests, "sample", true);
        AddMeasurement(measurements, "failedRequests", "quantity", "count", "load-tool", result.Metrics.FailedRequests, "sample", false);
        AddMeasurement(measurements, "timeoutRequests", "quantity", "count", "load-tool", result.Metrics.TimeoutRequests, "sample", false);
        AddMeasurement(measurements, "bytesReceived", "quantity", "bytes", "load-tool", result.Metrics.BytesReceived, "sample", true);
        AddMeasurement(measurements, "bytesSent", "quantity", "bytes", "load-tool", result.Metrics.BytesSent, "sample", true);
        AddMeasurement(measurements, "latencyMinMs", "latency", "ms", "load-tool", result.Metrics.LatencyMinMs, "min", false);
        AddMeasurement(measurements, "latencyMaxMs", "latency", "ms", "load-tool", result.Metrics.LatencyMaxMs, "max", false);
        AddMeasurement(measurements, "latencyMeanMs", "latency", "ms", "load-tool", result.Metrics.LatencyMeanMs, "mean", false);
        AddMeasurement(measurements, "latencyP50Ms", "latency", "ms", "load-tool", result.Metrics.LatencyP50Ms, "p50", false);
        AddMeasurement(measurements, "latencyP75Ms", "latency", "ms", "load-tool", result.Metrics.LatencyP75Ms, "p75", false);
        AddMeasurement(measurements, "latencyP90Ms", "latency", "ms", "load-tool", result.Metrics.LatencyP90Ms, "p90", false);
        AddMeasurement(measurements, "latencyP95Ms", "latency", "ms", "load-tool", result.Metrics.LatencyP95Ms, "p95", false);
        AddMeasurement(measurements, "latencyP99Ms", "latency", "ms", "load-tool", result.Metrics.LatencyP99Ms, "p99", false);
        AddMeasurement(measurements, "latencyP999Ms", "latency", "ms", "load-tool", result.Metrics.LatencyP999Ms, "p999", false);
        AddMeasurement(measurements, "connectTimeMeanMs", "latency", "ms", "load-tool", result.Metrics.ConnectTimeMeanMs, "mean", false);
        AddMeasurement(measurements, "timeToFirstByteMeanMs", "latency", "ms", "load-tool", result.Metrics.TimeToFirstByteMeanMs, "mean", false);
        AddMeasurement(measurements, "targetCpuMeanPercent", "cpu", "percent", "target", result.TargetDockerMetricsSummary?.CpuMeanPercent, "mean", false);
        AddMeasurement(measurements, "targetCpuMaxPercent", "cpu", "percent", "target", result.TargetDockerMetricsSummary?.CpuMaxPercent, "max", false);
        AddMeasurement(measurements, "targetMemoryMaxBytes", "memory", "bytes", "target", result.TargetDockerMetricsSummary?.MemoryMaxBytes, "max", false);
        AddMeasurement(measurements, "targetMemoryMaxPercent", "memory", "percent", "target", result.TargetDockerMetricsSummary?.MemoryMaxPercent, "max", false);
        AddMeasurement(measurements, "loadToolCpuMeanPercent", "cpu", "percent", "load-generator", result.LoadToolDockerMetricsSummary?.CpuMeanPercent, "mean", false);
        AddMeasurement(measurements, "loadToolCpuMaxPercent", "cpu", "percent", "load-generator", result.LoadToolDockerMetricsSummary?.CpuMaxPercent, "max", false);
        AddMeasurement(measurements, "loadToolMemoryMaxBytes", "memory", "bytes", "load-generator", result.LoadToolDockerMetricsSummary?.MemoryMaxBytes, "max", false);
        AddMeasurement(measurements, "counterCpuMean", "cpu", "percent", "runtime-counter", result.CountersSummary?.CpuMean, "mean", false);
        AddMeasurement(measurements, "counterCpuMax", "cpu", "percent", "runtime-counter", result.CountersSummary?.CpuMax, "max", false);
        AddMeasurement(measurements, "allocationRateMean", "memory", "bytes/s", "runtime-counter", result.CountersSummary?.AllocationRateMean, "mean", false);
        AddMeasurement(measurements, "gcHeapSizeMean", "memory", "bytes", "runtime-counter", result.CountersSummary?.GcHeapSizeMean, "mean", false);
        AddMeasurement(measurements, "gcHeapSizeMax", "memory", "bytes", "runtime-counter", result.CountersSummary?.GcHeapSizeMax, "max", false);
        AddMeasurement(measurements, "exceptionRateMean", "diagnostic", "exceptions/s", "runtime-counter", result.CountersSummary?.ExceptionRateMean, "mean", false);

        return measurements;
    }

    private static void AddMeasurement(
        ICollection<EvidenceReportMeasurement> measurements,
        string name,
        string category,
        string unit,
        string source,
        double? value,
        string statistic,
        bool higherIsBetter)
    {
        if (value.HasValue)
        {
            measurements.Add(new EvidenceReportMeasurement
            {
                Name = name,
                Category = category,
                Unit = unit,
                Source = source,
                Value = value.Value,
                Statistic = statistic,
                HigherIsBetter = higherIsBetter
            });
        }
    }

    private static void AddMeasurement(
        ICollection<EvidenceReportMeasurement> measurements,
        string name,
        string category,
        string unit,
        string source,
        long? value,
        string statistic,
        bool higherIsBetter)
    {
        if (value.HasValue)
        {
            AddMeasurement(measurements, name, category, unit, source, (double)value.Value, statistic, higherIsBetter);
        }
    }

    private static IReadOnlyList<EvidenceReportComparisonGroup> BuildComparisonGroups(
        IReadOnlyList<BenchmarkResult> results)
    {
        var acceptedResults = results
            .Where(r => r.ValidationResult.Status == ValidationStatus.Passed &&
                        r.BenchmarkExecutionStatus == LoadToolExecutionStatuses.Succeeded &&
                        r.ParsedMetricsAvailable)
            .ToArray();

        var groups = acceptedResults
            .GroupBy(r => new ComparisonGroupKey(
                r.ScenarioId,
                r.Protocol,
                r.LoadProfileId ?? "",
                r.LoadTool ?? "",
                r.TargetExecutionMode ?? "process",
                r.Connections,
                r.StreamsPerConnection,
                r.DurationSeconds,
                r.NetworkProfile))
            .Where(g => g.Select(r => r.ImplementationId).Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 2)
            .Select(g => BuildComparisonGroup(g.Key, g.ToArray()))
            .OrderBy(g => g.ScenarioId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Protocol, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.LoadProfileTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.LoadTool, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Connections)
            .ToArray();

        return groups;
    }

    private static EvidenceReportComparisonGroup BuildComparisonGroup(
        ComparisonGroupKey key,
        BenchmarkResult[] groupResults)
    {
        var first = groupResults[0];
        var comparabilityWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var entries = groupResults
            .GroupBy(r => (r.ImplementationId, r.ImplementationName))
            .Select(impGroup =>
            {
                var results = impGroup.ToArray();
                var sample = results[0];
                var metricsResults = results
                    .Where(r => r.ParsedMetricsAvailable)
                    .ToArray();

                double? rpsMedian = null;
                double? p50Median = null;
                double? p95Median = null;
                double? p99Median = null;
                double? meanMedian = null;
                double? throughputMedian = null;

                if (metricsResults.Length > 0)
                {
                    var rpsValues = metricsResults.Select(r => r.Metrics.RequestsPerSecond).Where(v => v.HasValue).Select(v => v!.Value).OrderBy(v => v).ToArray();
                    var p50Values = metricsResults.Select(r => r.Metrics.LatencyP50Ms).Where(v => v.HasValue).Select(v => v!.Value).OrderBy(v => v).ToArray();
                    var p95Values = metricsResults.Select(r => r.Metrics.LatencyP95Ms).Where(v => v.HasValue).Select(v => v!.Value).OrderBy(v => v).ToArray();
                    var p99Values = metricsResults.Select(r => r.Metrics.LatencyP99Ms).Where(v => v.HasValue).Select(v => v!.Value).OrderBy(v => v).ToArray();
                    var meanValues = metricsResults.Select(r => r.Metrics.LatencyMeanMs).Where(v => v.HasValue).Select(v => v!.Value).OrderBy(v => v).ToArray();
                    var tpValues = metricsResults.Select(r => r.Metrics.ThroughputBytesPerSecond).Where(v => v.HasValue).Select(v => v!.Value).OrderBy(v => v).ToArray();
                    rpsMedian = ComputeMedian(rpsValues);
                    p50Median = ComputeMedian(p50Values);
                    p95Median = ComputeMedian(p95Values);
                    p99Median = ComputeMedian(p99Values);
                    meanMedian = ComputeMedian(meanValues);
                    throughputMedian = ComputeMedian(tpValues);
                }

                foreach (var warning in results.SelectMany(r => r.Evidence?.ComparabilityWarnings ?? []))
                {
                    comparabilityWarnings.Add(warning);
                }

                return new EvidenceReportComparisonEntry
                {
                    ImplementationId = sample.ImplementationId,
                    ImplementationName = sample.ImplementationName,
                    LoadToolMode = sample.LoadToolMode,
                    ProvenProtocol = sample.ProvenProtocol,
                    RequestsPerSecondMedian = rpsMedian,
                    LatencyP50MsMedian = p50Median,
                    LatencyP95MsMedian = p95Median,
                    LatencyP99MsMedian = p99Median,
                    LatencyMeanMsMedian = meanMedian,
                    ThroughputBytesPerSecondMedian = throughputMedian,
                    Repetitions = results.Length,
                    ValidationStatus = results.All(r => r.ValidationResult.Status == ValidationStatus.Passed)
                        ? "passed"
                        : results.First(r => r.ValidationResult.Status != ValidationStatus.Passed).ValidationResult.Status.ToString(),
                    BenchmarkStatus = results.All(r => r.BenchmarkExecutionStatus == LoadToolExecutionStatuses.Succeeded)
                        ? "succeeded"
                        : "mixed",
                    EvidenceClass = sample.Evidence?.EvidenceClass ?? BenchmarkEvidenceClasses.LocalSmoke,
                    ComparabilityStatus = sample.Evidence?.ComparabilityStatus ?? BenchmarkComparabilityStatuses.NotComparable
                };
            })
            .OrderBy(e => e.ImplementationId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new EvidenceReportComparisonGroup
        {
            ScenarioId = key.ScenarioId,
            ScenarioName = first.ScenarioName,
            Family = first.Family,
            Protocol = key.Protocol,
            LoadProfileId = key.LoadProfileId,
            LoadProfileTitle = first.LoadProfileTitle ?? key.LoadProfileId,
            LoadTool = key.LoadTool,
            ExecutionBackend = key.TargetExecutionMode,
            EvidenceTier = groupResults
                .Select(r => r.Evidence?.EvidenceClass)
                .Where(c => c is not null)
                .Distinct(StringComparer.Ordinal)
                .FirstOrDefault() ?? BenchmarkEvidenceClasses.LocalSmoke,
            Connections = key.Connections,
            StreamsPerConnection = key.StreamsPerConnection,
            DurationSeconds = key.DurationSeconds,
            NetworkProfile = key.NetworkProfile,
            Entries = entries,
            ComparabilityWarnings = comparabilityWarnings.OrderBy(w => w, StringComparer.Ordinal).ToArray()
        };
    }

    private static IReadOnlyList<EvidenceReportWarning> BuildWarnings(
        IReadOnlyList<RunCell> cells,
        IReadOnlyList<RunCellCompatibility> compatibilities,
        IReadOnlyList<BenchmarkResult> results)
    {
        var warnings = new List<EvidenceReportWarning>();

        warnings.Add(new EvidenceReportWarning
        {
            Category = "environment",
            Message = "Shared-host local run; not publishable benchmark evidence."
        });

        var unsupportedCount = 0;
        for (var i = 0; i < cells.Count; i++)
        {
            var compatibility = i < compatibilities.Count ? compatibilities[i] : null;
            if (compatibility is not null && !compatibility.CanRun)
            {
                unsupportedCount++;
            }
        }

        if (unsupportedCount > 0)
        {
            warnings.Add(new EvidenceReportWarning
            {
                Category = "matrix",
                Message = $"{unsupportedCount} cells are unsupported and were not executed.",
                Context = "matrix-compatibility"
            });
        }

        var dockerBridgeResults = results
            .Where(r => string.Equals(r.TargetDockerNetworkMode, TargetNetworkModes.PublishedPort, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (dockerBridgeResults.Length > 0)
        {
            warnings.Add(new EvidenceReportWarning
            {
                Category = "network",
                Message = "Docker bridge networking used; network overhead may affect comparison.",
                Context = "docker-network"
            });
        }

        var distinctLoadTools = results
            .Select(r => r.LoadTool)
            .Where(lt => !string.IsNullOrWhiteSpace(lt))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        if (distinctLoadTools > 1)
        {
            warnings.Add(new EvidenceReportWarning
            {
                Category = "comparability",
                Message = "Different load tools used; metrics are not directly comparable.",
                Context = "load-tool-mix"
            });
        }

        var parserFailedResults = results
            .Where(r => r.BenchmarkExecutionStatus == LoadToolExecutionStatuses.Succeeded && !r.ParsedMetricsAvailable)
            .ToArray();
        if (parserFailedResults.Length > 0)
        {
            warnings.Add(new EvidenceReportWarning
            {
                Category = "parser",
                Message = "Parser fallback used for some results; normalized metrics may be incomplete.",
                Context = "parser-fallback"
            });
        }

        var missingQlogResults = results
            .Where(r => (r.QlogFileCount ?? 0) <= 0 && r.BenchmarkExecutionStatus == LoadToolExecutionStatuses.Succeeded)
            .ToArray();
        if (missingQlogResults.Length > 0)
        {
            warnings.Add(new EvidenceReportWarning
            {
                Category = "validation",
                Message = "Validation proof missing optional qlog artifact.",
                Context = "qlog-missing"
            });
        }

        var diffProfiles = results
            .Select(r => r.LoadProfileId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        if (diffProfiles > 1)
        {
            warnings.Add(new EvidenceReportWarning
            {
                Category = "comparability",
                Message = "Multiple load profiles used; results across profiles are not directly comparable.",
                Context = "load-profile-mix"
            });
        }

        var diffBackends = results
            .Select(r => r.TargetExecutionMode)
            .Where(mode => !string.IsNullOrWhiteSpace(mode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        if (diffBackends > 1)
        {
            warnings.Add(new EvidenceReportWarning
            {
                Category = "comparability",
                Message = "Different execution backends used; results are not directly comparable across backends.",
                Context = "execution-backend-mix"
            });
        }

        var diffEvidenceTiers = results
            .Select(r => r.Evidence?.EvidenceClass)
            .Where(c => c is not null)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (diffEvidenceTiers > 1)
        {
            warnings.Add(new EvidenceReportWarning
            {
                Category = "comparability",
                Message = "Different evidence tiers present; results across tiers are not directly comparable.",
                Context = "evidence-tier-mix"
            });
        }

        return warnings;
    }

    private static IReadOnlyList<EvidenceReportArtifactEntry> BuildArtifactIndex(IReadOnlyList<BenchmarkResult> results)
    {
        var entries = new List<EvidenceReportArtifactEntry>();

        foreach (var result in results)
        {
            var cellKey = BuildCellKey(result);
            var resultJsonPath = GetArtifact(result, "resultJson");
            var directory = resultJsonPath is not null
                ? Path.GetDirectoryName(resultJsonPath) ?? ""
                : "";
            var files = new List<EvidenceReportArtifactFile>();

            void AddFile(string name, string? path)
            {
                files.Add(new EvidenceReportArtifactFile
                {
                    Name = name,
                    Path = path ?? "",
                    Exists = !string.IsNullOrWhiteSpace(path)
                });
            }

            AddFile("result.json", resultJsonPath);
            AddFile("validation.json", GetArtifact(result, "validationJson"));
            AddFile("validation-proof/", directory.Length > 0 ? Path.Combine(directory, "validation-proof") : null);
            AddFile("protocol-proof.json", GetArtifact(result, "protocolProofJson"));
            AddFile("protocol-proof.stdout.txt", GetArtifact(result, "protocolProofStdout"));
            AddFile("protocol-proof.stderr.txt", GetArtifact(result, "protocolProofStderr"));
            AddFile("load.stdout.log", GetArtifact(result, "loadToolStdout"));
            AddFile("load.stderr.log", GetArtifact(result, "loadToolStderr"));
            AddFile("h2load-output.json", GetArtifact(result, "h2loadOutputJson"));
            AddFile("load-tool.version.txt", GetArtifact(result, "loadToolVersion"));
            AddFile("target.stdout.log", GetArtifact(result, "targetStdout"));
            AddFile("target.stderr.log", GetArtifact(result, "targetStderr"));
            AddFile("manifest.json", GetArtifact(result, "manifestSnapshot"));
            AddFile("scenario.json", GetArtifact(result, "scenarioSnapshot"));
            AddFile("docker-inspect.json", GetArtifact(result, "dockerInspectJson"));
            AddFile("target-docker-stats.jsonl", GetArtifact(result, "targetDockerStatsJsonl"));
            AddFile("counters-summary.json", GetArtifact(result, "countersSummary"));
            AddFile("qlog/", GetArtifact(result, "qlog"));
            AddFile("notes.txt", GetArtifact(result, "notes"));

            entries.Add(new EvidenceReportArtifactEntry
            {
                CellKey = cellKey,
                CellDirectory = directory,
                Files = files
            });
        }

        return entries;
    }

    private static string? GetArtifact(BenchmarkResult result, string key)
    {
        return result.Artifacts.TryGetValue(key, out var value) ? value : null;
    }

    private static string BuildCellKey(BenchmarkResult result)
    {
        return $"{result.ImplementationId}/{result.ScenarioId}/{result.Protocol}/c{result.Connections}-s{result.StreamsPerConnection}-r{result.Repetition}";
    }

    private static double? ComputeMedian(double[] values)
    {
        if (values.Length == 0)
        {
            return null;
        }

        return values.Length % 2 == 1
            ? values[values.Length / 2]
            : (values[(values.Length / 2) - 1] + values[values.Length / 2]) / 2.0d;
    }

    private sealed record ComparisonGroupKey(
        string ScenarioId,
        string Protocol,
        string LoadProfileId,
        string LoadTool,
        string TargetExecutionMode,
        int Connections,
        int StreamsPerConnection,
        int DurationSeconds,
        string NetworkProfile);
}
