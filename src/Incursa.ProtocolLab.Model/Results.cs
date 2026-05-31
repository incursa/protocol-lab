// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Incursa.ProtocolLab.Model;

public enum ValidationStatus
{
    Passed,
    Failed,
    Unsupported,
    NotApplicable,
    Inconclusive,
    InfrastructureFailure
}

public sealed record ValidationObservation
{
    public required string Category { get; init; }
    public required string Description { get; init; }
    public string? Detail { get; init; }
    public string? ExpectedValue { get; init; }
    public string? ActualValue { get; init; }
}

public sealed record ValidationProofArtifact
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required string Category { get; init; }
    public string? Description { get; init; }
}

public sealed record ProtocolProofResult
{
    public ValidationStatus Status { get; init; } = ValidationStatus.NotApplicable;
    public string RequestedProtocol { get; init; } = "";
    public string? ProvenProtocol { get; init; }
    public string Method { get; init; } = "";
    public string? ProofClient { get; init; }
    public string? RequestUrl { get; init; }
    public string? RequestedVersion { get; init; }
    public string? VersionPolicy { get; init; }
    public string? ResponseVersion { get; init; }
    public int? StatusCode { get; init; }
    public string? ContentType { get; init; }
    public string? CommandLine { get; init; }
    public string? JsonPath { get; init; }
    public string? StdoutPath { get; init; }
    public string? StderrPath { get; init; }
    public string? CertificateMode { get; init; }
    public string? HttpsBaseUrl { get; init; }
    public bool FallbackDetected { get; init; }
    public string? LoadToolH3CapabilityStatus { get; init; }
    public Dictionary<string, string> ArtifactPaths { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed record ScenarioValidationResult
{
    public required string ScenarioId { get; init; }
    public required string TargetId { get; init; }
    public required string AdapterId { get; init; }
    public required string Protocol { get; init; }
    public required ValidationStatus Status { get; init; }
    public required string Summary { get; init; }

    public IReadOnlyList<ValidationObservation> Observations { get; init; } = [];
    public IReadOnlyList<ValidationProofArtifact> ProofArtifacts { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public ProtocolProofResult? ProtocolProof { get; init; }

    [JsonIgnore]
    public bool AllowsBenchmark => Status == ValidationStatus.Passed;
}

public sealed record BenchmarkResult
{
    public required string RunId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string ImplementationId { get; init; }
    public required string ImplementationName { get; init; }
    public required string ScenarioId { get; init; }
    public required string ScenarioName { get; init; }
    public required string Family { get; init; }
    public required string Protocol { get; init; }
    public string? RequestedProtocol { get; init; }
    public string? ProvenProtocol { get; init; }
    public ProtocolProofResult? ProtocolProof { get; init; }
    public required string Role { get; init; }
    public required string ExecutionProfile { get; init; }
    public string? LoadProfileId { get; init; }
    public string? LoadProfileTitle { get; init; }
    public string? LoadProfilePurpose { get; init; }
    public string? LoadTool { get; init; }
    public string? LoadToolMode { get; init; }
    public string? LoadToolCategory { get; init; }
    public string? LoadToolVersion { get; init; }
    public string? LoadToolCommandLine { get; init; }
    public string? DockerImage { get; init; }
    public string? DockerCommandLine { get; init; }
    public string? LoadToolDockerInspectPath { get; init; }
    public string? LoadToolWorkingDirectory { get; init; }
    public string? LoadToolParserId { get; init; }
    public string? RequestedLoadToolUrl { get; init; }
    public string? EffectiveLoadToolUrl { get; init; }
    public string? LoadToolConnectTarget { get; init; }
    public string? HostRewriteMode { get; init; }
    public string? LoadToolSni { get; init; }
    public string? LoadToolContainerNetwork { get; init; }
    public string? CertificateMode { get; init; }
    public int? LoadToolExitCode { get; init; }
    public string? LoadToolH3CapabilityStatus { get; init; }
    public IReadOnlyList<string> LoadToolH3CapabilityWarnings { get; init; } = [];
    public string? LoadToolDockerImageId { get; init; }
    public string? LoadToolDockerImageDigest { get; init; }
    public string? LoadToolContainerId { get; init; }
    public string? LoadToolContainerName { get; init; }
    public DockerResourceLimits? LoadToolDockerResourceLimitsRequested { get; init; }
    public DockerResourceLimits? LoadToolDockerResourceLimitsEffective { get; init; }
    public bool LoadToolDockerMetricsAvailable { get; init; }
    public DockerContainerMetricsSummary? LoadToolDockerMetricsSummary { get; init; }
    public string? LoadToolSaturationStatus { get; init; }
    public IReadOnlyList<string> LoadToolSaturationWarnings { get; init; } = [];
    public Dictionary<string, string> LoadToolDockerMetricsArtifacts { get; init; } = [];
    public string? TargetExecutionMode { get; init; }
    public string? TargetDockerImage { get; init; }
    public string? TargetContainerName { get; init; }
    public string? TargetDockerNetwork { get; init; }
    public string? TargetDockerNetworkName { get; init; }
    public string? TargetDockerNetworkId { get; init; }
    public string? TargetDockerNetworkMode { get; init; }
    public IReadOnlyList<string> TargetNetworkAliases { get; init; } = [];
    public Dictionary<string, string> TargetPublishedPorts { get; init; } = [];
    public Dictionary<string, string> TargetInternalPorts { get; init; } = [];
    public string? TargetEffectiveBaseUrl { get; init; }
    public string? TargetDockerCommandLine { get; init; }
    public string? TargetDockerInspectPath { get; init; }
    public string? TargetDockerNetworkInspectPath { get; init; }
    public DockerResourceLimits? TargetDockerResourceLimitsRequested { get; init; }
    public DockerResourceLimits? TargetDockerResourceLimitsEffective { get; init; }
    public string? TargetCapabilityProofId { get; init; }
    public string? TargetCapabilityProofStatus { get; init; }
    public string? TargetCapabilityProofCommandLine { get; init; }
    public string? TargetCapabilityProofExpectedOutput { get; init; }
    public string? TargetCapabilityProofOutputPath { get; init; }
    public IReadOnlyList<string> TargetCapabilityProofWarnings { get; init; } = [];
    public bool TargetDockerMetricsAvailable { get; init; }
    public TargetDockerMetricsSummary? TargetDockerMetricsSummary { get; init; }
    public string? TargetContract { get; init; }
    public string? AdapterControlPlaneBaseUrl { get; init; }
    public string? AdapterSessionId { get; init; }
    public string? AdapterScenarioId { get; init; }
    public string? AdapterScenarioVersion { get; init; }
    public IReadOnlyList<string> AdapterEndpointTypes { get; init; } = [];
    public string? TargetSaturationStatus { get; init; }
    public IReadOnlyList<string> TargetSaturationWarnings { get; init; } = [];
    public Dictionary<string, string> TargetDockerMetricsArtifacts { get; init; } = [];
    public IReadOnlyList<string> ResourceLimitWarnings { get; init; } = [];
    public DockerCleanupSummary? DockerCleanup { get; init; }
    public string? NetworkCleanupStatus { get; init; }
    public IReadOnlyList<string> NetworkWarnings { get; init; } = [];
    public int? TargetProcessId { get; init; }
    public string? TargetCommandLine { get; init; }
    public DateTimeOffset? TargetStartTimeUtc { get; init; }
    public DateTimeOffset? TargetReadyTimeUtc { get; init; }
    public int? TargetExitCode { get; init; }
    public string BenchmarkExecutionStatus { get; init; } = LoadToolExecutionStatuses.Skipped;
    public string? BenchmarkFailureReason { get; init; }
    public required int DurationSeconds { get; init; }
    public required int WarmupSeconds { get; init; }
    public required int Repetition { get; init; }
    public required int Connections { get; init; }
    public required int StreamsPerConnection { get; init; }
    public RequestedLoadShape? RequestedLoadShape { get; init; }
    public EffectiveLoadShape? EffectiveLoadShape { get; init; }
    public LoadShapeSemantics? LoadShapeSemantics { get; init; }
    public List<string> LoadShapeWarnings { get; init; } = [];
    public required string NetworkProfile { get; init; }
    public required ScenarioValidationResult ValidationResult { get; init; }
    public BenchmarkEvidenceAssessment? Evidence { get; init; }
    public TargetProcessMetrics? TargetProcessMetrics { get; init; }
    public DiagnosticTarget? DiagnosticTarget { get; init; }
    public bool CountersAvailable { get; init; }
    public string? CountersCaptureStatus { get; init; }
    public RuntimeCounterSummary? CountersSummary { get; init; }
    public Dictionary<string, string> CounterArtifacts { get; init; } = [];
    public string? QlogDirectory { get; init; }
    public int? QlogFileCount { get; init; }
    public bool ParsedMetricsAvailable { get; init; }
    public HttpMetrics Metrics { get; init; } = new();
    public ServerMetrics ServerMetrics { get; init; } = new();
    public ProtocolMetrics ProtocolMetrics { get; init; } = new();
    public Dictionary<string, string> Artifacts { get; init; } = [];
    public List<string> Errors { get; init; } = [];
    public List<string> Warnings { get; init; } = [];

    public static BenchmarkResult FromCell(
        string runId,
        RunCell cell,
        ScenarioValidationResult validationResult,
        string? loadTool,
        bool parsedMetricsAvailable,
        Dictionary<string, string> artifacts,
        HttpMetrics? metrics = null,
        IEnumerable<string>? warnings = null,
        IEnumerable<string>? errors = null,
        string? loadToolMode = null,
        string? loadToolCategory = null,
        string? loadToolVersion = null,
        string benchmarkExecutionStatus = LoadToolExecutionStatuses.Skipped,
        string? benchmarkFailureReason = null,
        string? loadToolCommandLine = null,
        string? dockerImage = null,
        string? dockerCommandLine = null,
        string? loadToolWorkingDirectory = null,
        string? loadToolParserId = null,
        string? requestedLoadToolUrl = null,
        string? effectiveLoadToolUrl = null,
        string? loadToolConnectTarget = null,
        string? hostRewriteMode = null,
        string? loadToolSni = null,
        string? loadToolContainerNetwork = null,
        string? certificateMode = null,
        RequestedLoadShape? requestedLoadShape = null,
        EffectiveLoadShape? effectiveLoadShape = null,
        LoadShapeSemantics? loadShapeSemantics = null,
        IEnumerable<string>? loadShapeWarnings = null,
        TargetExecutionResult? targetExecution = null,
        ProtocolProofResult? protocolProof = null)
    {
        return new BenchmarkResult
        {
            RunId = runId,
            Timestamp = DateTimeOffset.UtcNow,
            ImplementationId = cell.Implementation.Id,
            ImplementationName = cell.Implementation.Name,
            ScenarioId = cell.Scenario.Id,
            ScenarioName = cell.Scenario.Name,
            Family = cell.Scenario.Family,
            Protocol = ProtocolIds.Normalize(cell.Protocol),
            RequestedProtocol = ProtocolIds.Normalize(cell.Protocol),
            ProvenProtocol = protocolProof?.ProvenProtocol ?? validationResult.ProtocolProof?.ProvenProtocol,
            ProtocolProof = protocolProof ?? validationResult.ProtocolProof,
            Role = cell.Scenario.ImplementationRole,
            ExecutionProfile = ExecutionProfiles.ToId(cell.ExecutionProfile),
            LoadProfileId = cell.LoadProfileId,
            LoadTool = loadTool,
            LoadToolMode = loadToolMode,
            LoadToolCategory = loadToolCategory,
            LoadToolVersion = loadToolVersion,
            LoadToolCommandLine = loadToolCommandLine,
            DockerImage = dockerImage,
            DockerCommandLine = dockerCommandLine,
            LoadToolWorkingDirectory = loadToolWorkingDirectory,
            LoadToolParserId = loadToolParserId,
            RequestedLoadToolUrl = requestedLoadToolUrl,
            EffectiveLoadToolUrl = effectiveLoadToolUrl,
            LoadToolConnectTarget = loadToolConnectTarget,
            HostRewriteMode = hostRewriteMode,
            LoadToolSni = loadToolSni,
            LoadToolContainerNetwork = loadToolContainerNetwork,
            CertificateMode = certificateMode,
            TargetExecutionMode = targetExecution?.TargetExecutionMode,
            TargetContract = targetExecution?.TargetContract,
            TargetDockerImage = targetExecution?.TargetDockerImage,
            TargetContainerName = targetExecution?.TargetContainerName,
            TargetDockerNetwork = targetExecution?.TargetDockerNetwork,
            TargetDockerNetworkName = targetExecution?.TargetDockerNetworkName,
            TargetDockerNetworkId = targetExecution?.TargetDockerNetworkId,
            TargetDockerNetworkMode = targetExecution?.TargetDockerNetworkMode,
            TargetNetworkAliases = targetExecution?.TargetNetworkAliases ?? [],
            TargetPublishedPorts = targetExecution?.TargetPublishedPorts ?? [],
            TargetInternalPorts = targetExecution?.TargetInternalPorts ?? [],
            TargetEffectiveBaseUrl = targetExecution?.TargetEffectiveBaseUrl,
            TargetDockerCommandLine = targetExecution?.TargetDockerCommandLine,
            TargetDockerInspectPath = targetExecution?.TargetDockerInspectPath,
            TargetDockerNetworkInspectPath = targetExecution?.TargetDockerNetworkInspectPath,
            TargetDockerResourceLimitsRequested = targetExecution?.TargetDockerResourceLimitsRequested,
            TargetDockerResourceLimitsEffective = targetExecution?.TargetDockerResourceLimitsEffective,
            TargetCapabilityProofId = targetExecution?.TargetCapabilityProofId,
            TargetCapabilityProofStatus = targetExecution?.TargetCapabilityProofStatus,
            TargetCapabilityProofCommandLine = targetExecution?.TargetCapabilityProofCommandLine,
            TargetCapabilityProofExpectedOutput = targetExecution?.TargetCapabilityProofExpectedOutput,
            TargetCapabilityProofOutputPath = targetExecution?.TargetCapabilityProofOutputPath,
            TargetCapabilityProofWarnings = targetExecution?.TargetCapabilityProofWarnings ?? [],
            AdapterControlPlaneBaseUrl = targetExecution?.AdapterControlPlaneBaseUrl,
            AdapterSessionId = targetExecution?.AdapterSessionId,
            AdapterScenarioId = targetExecution?.AdapterScenarioId,
            AdapterScenarioVersion = targetExecution?.AdapterScenarioVersion,
            AdapterEndpointTypes = targetExecution?.AdapterEndpointTypes ?? [],
            ResourceLimitWarnings = targetExecution?.ResourceLimitWarnings ?? [],
            DockerCleanup = targetExecution?.CleanupSummary,
            NetworkCleanupStatus = targetExecution?.NetworkCleanupStatus,
            NetworkWarnings = targetExecution?.NetworkWarnings ?? [],
            TargetProcessId = targetExecution?.ProcessId,
            TargetCommandLine = targetExecution?.CommandLine,
            TargetStartTimeUtc = targetExecution?.StartTimeUtc,
            TargetReadyTimeUtc = targetExecution?.ReadyTimeUtc,
            TargetExitCode = targetExecution?.ExitCode,
            BenchmarkExecutionStatus = benchmarkExecutionStatus,
            BenchmarkFailureReason = benchmarkFailureReason,
            DurationSeconds = cell.DurationSeconds,
            WarmupSeconds = cell.WarmupSeconds,
            Repetition = cell.Repetition,
            Connections = cell.Connections,
            StreamsPerConnection = cell.StreamsPerConnection,
            RequestedLoadShape = requestedLoadShape,
            EffectiveLoadShape = effectiveLoadShape,
            LoadShapeSemantics = loadShapeSemantics,
            LoadShapeWarnings = loadShapeWarnings?.ToList() ?? [],
            NetworkProfile = cell.NetworkProfile,
            ValidationResult = validationResult,
            Evidence = null,
            ParsedMetricsAvailable = parsedMetricsAvailable,
            Metrics = metrics ?? new HttpMetrics(),
            Artifacts = artifacts,
            Warnings = warnings?.ToList() ?? [],
            Errors = errors?.ToList() ?? []
        };
    }
}

public sealed record HttpMetrics
{
    public double? RequestsPerSecond { get; init; }
    public long? TotalRequests { get; init; }
    public long? SuccessfulRequests { get; init; }
    public long? FailedRequests { get; init; }
    public long? TimeoutRequests { get; init; }
    public Dictionary<string, long> StatusCodeCounts { get; init; } = [];
    public long? BytesReceived { get; init; }
    public long? BytesSent { get; init; }
    public double? ThroughputBytesPerSecond { get; init; }
    public double? LatencyMinMs { get; init; }
    public double? LatencyMaxMs { get; init; }
    public double? LatencyMeanMs { get; init; }
    public double? LatencyP50Ms { get; init; }
    public double? LatencyP75Ms { get; init; }
    public double? LatencyP90Ms { get; init; }
    public double? LatencyP95Ms { get; init; }
    public double? LatencyP99Ms { get; init; }
    public double? LatencyP999Ms { get; init; }
    public double? ConnectTimeMeanMs { get; init; }
    public double? TimeToFirstByteMeanMs { get; init; }
}

public sealed record ServerMetrics
{
    public double? CpuPercentMean { get; init; }
    public double? CpuPercentMax { get; init; }
    public long? WorkingSetBytes { get; init; }
    public long? PrivateBytes { get; init; }
    public long? GcCollectionsGen0 { get; init; }
    public long? GcCollectionsGen1 { get; init; }
    public long? GcCollectionsGen2 { get; init; }
    public long? AllocatedBytesPerSecond { get; init; }
    public int? ThreadCount { get; init; }
    public int? HandleCount { get; init; }
}

public sealed record ProtocolMetrics
{
    [JsonExtensionData]
    public Dictionary<string, object> Values { get; init; } = [];
}
