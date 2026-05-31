// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;

namespace Incursa.ProtocolLab.Adapter.Contracts;

public enum AdapterHealthStatus
{
    Ready,
    Degraded,
    NotReady,
    Unavailable,
    Unsupported
}

public enum AdapterSessionState
{
    Created,
    Preparing,
    Prepared,
    Starting,
    Running,
    Ready,
    Stopping,
    Stopped,
    Failed,
    Unsupported,
    Disposed
}

public enum AdapterReadinessStatus
{
    Unknown,
    NotReady,
    Ready,
    Unsupported,
    Failed
}

public enum AdapterOperationResultCategory
{
    Succeeded,
    Pending,
    Unsupported,
    Rejected,
    Failed
}

public enum AdapterResourceAvailability
{
    Available,
    Partial,
    Unavailable,
    Unsupported
}

public enum AdapterCapabilityStatus
{
    Supported,
    Conditional,
    Partial,
    Experimental,
    Unsupported
}

public sealed record AdapterIdentity
{
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public string? Version { get; init; }

    public string? Revision { get; init; }

    public string? Vendor { get; init; }

    public string? Image { get; init; }

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterVersionCompatibility
{
    public string ContractVersion { get; init; } = "";

    public IReadOnlyList<string> CompatibleContractVersions { get; init; } = [];

    public JsonElement? RunnerCompatibility { get; init; }

    public string? ImplementationVersion { get; init; }

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterCapability
{
    public string Id { get; init; } = "";

    public AdapterCapabilityStatus Status { get; init; }

    public string? Version { get; init; }

    public string? Mode { get; init; }

    public string? Description { get; init; }

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterScenarioSelector
{
    public string SelectorType { get; init; } = "";

    public string Expression { get; init; } = "";

    public string? Description { get; init; }

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterEndpointType
{
    public string Type { get; init; } = "";

    public string? Description { get; init; }

    public IReadOnlyList<string> Protocols { get; init; } = [];

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterArtifactType
{
    public string Type { get; init; } = "";

    public string? Description { get; init; }

    public IReadOnlyList<string> ProducedByStates { get; init; } = [];

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterMetricsAvailability
{
    public bool Available { get; init; }

    public bool? SessionMetricsAvailable { get; init; }

    public bool? EndpointMetricsAvailable { get; init; }

    public bool? ProcessMetricsAvailable { get; init; }

    public bool? ContainerMetricsAvailable { get; init; }

    public IReadOnlyList<string> AvailableKinds { get; init; } = [];

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterHealthResponse
{
    public AdapterIdentity AdapterIdentity { get; init; } = new();

    public AdapterHealthStatus Status { get; init; } = AdapterHealthStatus.Ready;

    public AdapterVersionCompatibility VersionCompatibility { get; init; } = new();

    public string? Message { get; init; }

    public DateTimeOffset? ObservedAt { get; init; }

    public IReadOnlyList<AdapterCapability> Capabilities { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterManifestResponse
{
    public AdapterIdentity AdapterIdentity { get; init; } = new();

    public AdapterIdentity ImplementationIdentity { get; init; } = new();

    public AdapterVersionCompatibility VersionCompatibility { get; init; } = new();

    public IReadOnlyList<string> SupportedRoles { get; init; } = [];

    public IReadOnlyList<AdapterCapability> ClaimedCapabilities { get; init; } = [];

    public IReadOnlyList<AdapterScenarioSelector> SupportedScenarioSelectors { get; init; } = [];

    public IReadOnlyList<AdapterEndpointType> SupportedEndpointTypes { get; init; } = [];

    public IReadOnlyList<AdapterArtifactType> SupportedArtifactTypes { get; init; } = [];

    public AdapterMetricsAvailability MetricsAvailability { get; init; } = new();

    public IReadOnlyList<string> DefaultResponseContentTypes { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterSessionCreateRequest
{
    public string? RequestedSessionId { get; init; }

    public string? RunId { get; init; }

    public string? CellId { get; init; }

    public string? SessionLabel { get; init; }

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterEndpointBinding
{
    public string BindingId { get; init; } = "";

    public string Purpose { get; init; } = "";

    public string EndpointType { get; init; } = "";

    public bool Required { get; init; } = true;

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterArtifactExpectation
{
    public string ArtifactType { get; init; } = "";

    public bool Required { get; init; } = true;

    public string? DestinationHint { get; init; }

    public string? ContentType { get; init; }

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterPrepareRequest
{
    public string ScenarioId { get; init; } = "";

    public string ScenarioVersion { get; init; } = "";

    public string Role { get; init; } = "";

    public JsonElement ScenarioDocument { get; init; }

    public IReadOnlyList<AdapterEndpointBinding> RequestedEndpointBindings { get; init; } = [];

    public string RunId { get; init; } = "";

    public string CellId { get; init; } = "";

    public IReadOnlyList<AdapterArtifactExpectation> ArtifactOutputExpectations { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterSessionSummary
{
    public string SessionId { get; init; } = "";

    public AdapterSessionState State { get; init; } = AdapterSessionState.Created;

    public string? ScenarioId { get; init; }

    public string? ScenarioVersion { get; init; }

    public string? Role { get; init; }

    public string? RunId { get; init; }

    public string? CellId { get; init; }

    public DateTimeOffset? CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterOperationResult
{
    public AdapterOperationResultCategory Category { get; init; } = AdapterOperationResultCategory.Succeeded;

    public string? Message { get; init; }

    public string? Code { get; init; }

    public bool? Retryable { get; init; }

    public Dictionary<string, JsonElement> Details { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterSessionResource
{
    public AdapterSessionSummary Session { get; init; } = new();

    public AdapterOperationResult? Operation { get; init; }

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterReadinessSnapshot
{
    public AdapterReadinessStatus Status { get; init; } = AdapterReadinessStatus.Unknown;

    public string? Message { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public DateTimeOffset? ObservedAt { get; init; }

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterHealthSnapshot
{
    public AdapterHealthStatus Status { get; init; } = AdapterHealthStatus.Ready;

    public string? Message { get; init; }

    public DateTimeOffset? ObservedAt { get; init; }

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterStatusResponse
{
    public AdapterSessionSummary Session { get; init; } = new();

    public AdapterReadinessSnapshot Readiness { get; init; } = new();

    public AdapterHealthSnapshot Health { get; init; } = new();

    public AdapterOperationResult? Operation { get; init; }

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterTlsNotes
{
    public string? CertificateMode { get; init; }

    public string? CertificateNotes { get; init; }

    public string? Sni { get; init; }

    public string? VerificationNotes { get; init; }

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterEndpoint
{
    public string EndpointId { get; init; } = "";

    public string Purpose { get; init; } = "";

    public string Scheme { get; init; } = "";

    public string Protocol { get; init; } = "";

    public string Host { get; init; } = "";

    public int Port { get; init; }

    public string? Path { get; init; }

    public string? Authority { get; init; }

    public string? SocketAddress { get; init; }

    public string? NetworkMode { get; init; }

    public string? BindMode { get; init; }

    public AdapterTlsNotes? Tls { get; init; }

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterMetric
{
    public string MetricId { get; init; } = "";

    public string Scope { get; init; } = "";

    public string? Unit { get; init; }

    public JsonElement Value { get; init; }

    public DateTimeOffset? CapturedAt { get; init; }

    public string? Notes { get; init; }

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterArtifact
{
    public string ArtifactId { get; init; } = "";

    public string ArtifactType { get; init; } = "";

    public AdapterResourceAvailability Status { get; init; } = AdapterResourceAvailability.Available;

    public string? Path { get; init; }

    public string? Uri { get; init; }

    public string? ContentType { get; init; }

    public bool? Final { get; init; }

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterEndpointsResponse
{
    public AdapterSessionSummary Session { get; init; } = new();

    public IReadOnlyList<AdapterEndpoint> Endpoints { get; init; } = [];

    public AdapterOperationResult? Operation { get; init; }

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterMetricsResponse
{
    public AdapterSessionSummary Session { get; init; } = new();

    public AdapterResourceAvailability Availability { get; init; } = AdapterResourceAvailability.Unavailable;

    public DateTimeOffset? CapturedAt { get; init; }

    public IReadOnlyList<AdapterMetric> Metrics { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];

    public AdapterOperationResult? Operation { get; init; }

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterArtifactsResponse
{
    public AdapterSessionSummary Session { get; init; } = new();

    public AdapterResourceAvailability Availability { get; init; } = AdapterResourceAvailability.Unavailable;

    public IReadOnlyList<AdapterArtifact> Artifacts { get; init; } = [];

    public AdapterOperationResult? Operation { get; init; }

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterProblemDetails
{
    public string Type { get; init; } = "about:blank";

    public string Title { get; init; } = "";

    public int Status { get; init; }

    public string? Detail { get; init; }

    public string? Instance { get; init; }

    public string? Code { get; init; }

    public AdapterHealthStatus? AdapterStatus { get; init; }

    public string? Operation { get; init; }

    public string? SessionId { get; init; }

    public string? UnsupportedReason { get; init; }

    public bool? Retryable { get; init; }

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}
