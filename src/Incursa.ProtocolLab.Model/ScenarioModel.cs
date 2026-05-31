// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public enum ScenarioStatus
{
    Draft,
    Stable,
    Experimental,
    Deprecated,
    Placeholder
}

public enum ScenarioKind
{
    Workload,
    ProtocolValidation,
    InteropValidation,
    Diagnostic,
    Profile
}

public enum ScenarioLayer
{
    Application,
    Protocol,
    Transport
}

public enum TrafficShape
{
    RequestResponse,
    Upload,
    Download,
    StreamingDownload,
    StreamingUpload,
    BidirectionalStream,
    Datagram,
    HandshakeOnly,
    ConnectionLifecycle
}

public enum CompatibilityStatus
{
    Supported,
    Unsupported,
    MissingCapability,
    MissingLoadTool,
    IncompatibleProtocol,
    IncompatibleRole,
    IncompatibleExecutionBackend,
    InvalidScenarioParameters,
    ExperimentalScenarioNotEnabled,
    PlaceholderScenarioNotRunnable,
    IncompatibleTrafficShape,
    IncompatibleLoadProfile,
    ExperimentalProfileNotEnabled,
    InvalidLoadProfileParameters
}

public sealed class ScenarioRequires
{
    public List<string> Capabilities { get; init; } = [];
    public List<string> Protocols { get; init; } = [];
    public List<string> Roles { get; init; } = [];
}

public sealed class ScenarioArtifacts
{
    public List<string> Required { get; init; } = [];
    public List<string> Optional { get; init; } = [];
}

public sealed class ScenarioBenchmarkCompat
{
    public List<string> CompatibleLoadShapes { get; init; } = [];
    public List<string> PrimaryMetrics { get; init; } = [];
}

public sealed class ScenarioComparability
{
    public List<string> Requires { get; init; } = [];
    public string Notes { get; init; } = "";
}
