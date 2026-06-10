// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;

namespace Incursa.ProtocolLab.Adapter.Contracts;

public enum TestExecutorHealthStatus
{
    Ready,
    Degraded,
    NotReady,
    Unavailable,
    Unsupported
}

public enum TestExecutorSessionState
{
    Created,
    Preparing,
    Prepared,
    Starting,
    Running,
    Stopping,
    Stopped,
    Failed,
    Unsupported,
    Disposed
}

public enum TestExecutorOperationResultCategory
{
    Succeeded,
    Pending,
    Unsupported,
    Rejected,
    Failed
}

public enum TestExecutorResourceAvailability
{
    Available,
    Partial,
    Unavailable,
    Unsupported
}

public enum TestExecutorCapabilityStatus
{
    Supported,
    Conditional,
    Partial,
    Experimental,
    Unsupported
}

public sealed record TestExecutorIdentity
{
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public string? Version { get; init; }

    public string? Revision { get; init; }

    public string? Vendor { get; init; }

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorVersionCompatibility
{
    public string ContractVersion { get; init; } = "";

    public IReadOnlyList<string> CompatibleContractVersions { get; init; } = [];

    public JsonElement? RunnerCompatibility { get; init; }

    public string? ExecutorVersion { get; init; }

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorCapability
{
    public string Id { get; init; } = "";

    public TestExecutorCapabilityStatus Status { get; init; }

    public string? Version { get; init; }

    public string? Mode { get; init; }

    public string? Description { get; init; }

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorSelector
{
    public string SelectorType { get; init; } = "";

    public string Expression { get; init; } = "";

    public string? Description { get; init; }

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorEndpointBinding
{
    public string BindingId { get; init; } = "";

    public string Purpose { get; init; } = "";

    public string EndpointType { get; init; } = "";

    public IReadOnlyList<string> Protocols { get; init; } = [];

    public bool Required { get; init; } = true;

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorArtifactType
{
    public string Type { get; init; } = "";

    public string? Description { get; init; }

    public IReadOnlyList<string> ProducedByStates { get; init; } = [];

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorMetricsAvailability
{
    public bool Available { get; init; }

    public IReadOnlyList<string> AvailableKinds { get; init; } = [];

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorHealthResponse
{
    public TestExecutorIdentity ExecutorIdentity { get; init; } = new();

    public TestExecutorHealthStatus Status { get; init; } = TestExecutorHealthStatus.Ready;

    public TestExecutorVersionCompatibility VersionCompatibility { get; init; } = new();

    public string? Message { get; init; }

    public DateTimeOffset? ObservedAt { get; init; }

    public IReadOnlyList<TestExecutorCapability> Capabilities { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorManifestResponse
{
    public TestExecutorIdentity ExecutorIdentity { get; init; } = new();

    public TestExecutorVersionCompatibility VersionCompatibility { get; init; } = new();

    public IReadOnlyList<TestExecutorCapability> ClaimedCapabilities { get; init; } = [];

    public IReadOnlyList<TestExecutorSelector> SupportedTestSelectors { get; init; } = [];

    public IReadOnlyList<TestExecutorSelector> SupportedScenarioSelectors { get; init; } = [];

    public IReadOnlyList<string> SupportedProtocolFamilies { get; init; } = [];

    public IReadOnlyList<string> SupportedExecutionModes { get; init; } = [];

    public IReadOnlyList<TestExecutorEndpointBinding> RequiredTargetEndpointBindings { get; init; } = [];

    public IReadOnlyList<TestExecutorArtifactType> SupportedArtifactTypes { get; init; } = [];

    public TestExecutorMetricsAvailability MetricsAvailability { get; init; } = new();

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorSessionCreateRequest
{
    public string? RequestedSessionId { get; init; }

    public string? RunId { get; init; }

    public string? CellId { get; init; }

    public string? SessionLabel { get; init; }

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorTargetEndpoint
{
    public string BindingId { get; init; } = "";

    public string EndpointId { get; init; } = "";

    public string Purpose { get; init; } = "";

    public string Scheme { get; init; } = "";

    public string Protocol { get; init; } = "";

    public string Host { get; init; } = "";

    public int Port { get; init; }

    public string? Path { get; init; }

    public string? Authority { get; init; }

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorArtifactExpectation
{
    public string ArtifactType { get; init; } = "";

    public bool Required { get; init; } = true;

    public string? DestinationHint { get; init; }

    public string? ContentType { get; init; }

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorPrepareRequest
{
    public string TestId { get; init; } = "";

    public string ScenarioId { get; init; } = "";

    public string ScenarioVersion { get; init; } = "";

    public string Protocol { get; init; } = "";

    public JsonElement TestDocument { get; init; }

    public JsonElement ScenarioDocument { get; init; }

    public IReadOnlyList<TestExecutorTargetEndpoint> TargetEndpoints { get; init; } = [];

    public string RunId { get; init; } = "";

    public string CellId { get; init; } = "";

    public IReadOnlyList<TestExecutorArtifactExpectation> ArtifactOutputExpectations { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorSessionSummary
{
    public string SessionId { get; init; } = "";

    public TestExecutorSessionState State { get; init; } = TestExecutorSessionState.Created;

    public string? TestId { get; init; }

    public string? ScenarioId { get; init; }

    public string? Protocol { get; init; }

    public string? RunId { get; init; }

    public string? CellId { get; init; }

    public DateTimeOffset? CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorOperationResult
{
    public TestExecutorOperationResultCategory Category { get; init; } = TestExecutorOperationResultCategory.Succeeded;

    public string? Message { get; init; }

    public string? Code { get; init; }

    public bool? Retryable { get; init; }

    public Dictionary<string, JsonElement> Details { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorSessionResource
{
    public TestExecutorSessionSummary Session { get; init; } = new();

    public TestExecutorOperationResult? Operation { get; init; }

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorStatusResponse
{
    public TestExecutorSessionSummary Session { get; init; } = new();

    public TestExecutorOperationResult? Operation { get; init; }

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorMetric
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

public sealed record TestExecutorArtifact
{
    public string ArtifactId { get; init; } = "";

    public string ArtifactType { get; init; } = "";

    public TestExecutorResourceAvailability Status { get; init; } = TestExecutorResourceAvailability.Available;

    public string? Path { get; init; }

    public string? Uri { get; init; }

    public string? ContentType { get; init; }

    public bool? Final { get; init; }

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorMetricsResponse
{
    public TestExecutorSessionSummary Session { get; init; } = new();

    public TestExecutorResourceAvailability Availability { get; init; } = TestExecutorResourceAvailability.Unavailable;

    public DateTimeOffset? CapturedAt { get; init; }

    public IReadOnlyList<TestExecutorMetric> Metrics { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];

    public TestExecutorOperationResult? Operation { get; init; }

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorArtifactsResponse
{
    public TestExecutorSessionSummary Session { get; init; } = new();

    public TestExecutorResourceAvailability Availability { get; init; } = TestExecutorResourceAvailability.Unavailable;

    public IReadOnlyList<TestExecutorArtifact> Artifacts { get; init; } = [];

    public TestExecutorOperationResult? Operation { get; init; }

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorProblemDetails
{
    public string Type { get; init; } = "about:blank";

    public string Title { get; init; } = "";

    public int Status { get; init; }

    public string? Detail { get; init; }

    public string? Instance { get; init; }

    public string? Code { get; init; }

    public TestExecutorHealthStatus? ExecutorStatus { get; init; }

    public string? Operation { get; init; }

    public string? SessionId { get; init; }

    public string? UnsupportedReason { get; init; }

    public bool? Retryable { get; init; }

    public Dictionary<string, JsonElement> Metadata { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}
