// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public sealed class ImplementationManifest
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Image { get; init; } = "";
    public string TargetKind { get; init; } = "";
    public string TargetContract { get; init; } = "";
    public string Executable { get; init; } = "";
    public string Project { get; init; } = "";
    public string WorkingDirectory { get; init; } = "";
    public string Dockerfile { get; init; } = "";
    public string BuildContext { get; init; } = "";
    public string ContainerName { get; init; } = "";
    public string DockerNetwork { get; init; } = "";
    public string DockerNetworkMode { get; init; } = "";
    public string DockerBaseUrl { get; init; } = "";
    public Dictionary<string, string> DockerProtocolBaseUrls { get; init; } = [];
    public string BaseUrl { get; init; } = "";
    public Dictionary<string, string> ProtocolBaseUrls { get; init; } = [];
    public string AdapterControlPlaneBaseUrl { get; init; } = "";
    public string CertificateMode { get; init; } = "";
    public List<string> Roles { get; init; } = [];
    public List<string> SupportedProtocols { get; init; } = [];
    public List<string> SupportedWorkloadFamilies { get; init; } = [];
    public List<PortMapping> Ports { get; init; } = [];
    public Dictionary<string, string> Environment { get; init; } = [];
    public Dictionary<string, string> DockerEnvironment { get; init; } = [];
    public DockerResourceLimits? TargetDockerResourceLimits { get; init; }
    public TargetCapabilityProof? TargetCapabilityProof { get; init; }
    public List<string> CommandArguments { get; init; } = [];
    public List<string> DockerCommandArguments { get; init; } = [];
    public ReadinessCheck ReadinessCheck { get; init; } = new();
    public string ShutdownBehavior { get; init; } = "";
    public List<string> Capabilities { get; init; } = [];
    public List<string> ArtifactExports { get; init; } = [];
    public bool QlogSupport { get; init; }
    public bool SslKeyLogSupport { get; init; }
    public string Notes { get; init; } = "";
}

public sealed class PortMapping
{
    public string Name { get; init; } = "";
    public int ContainerPort { get; init; }
    public int? HostPort { get; init; }
    public string Protocol { get; init; } = "tcp";
}

public sealed class ReadinessCheck
{
    public string Type { get; init; } = "";
    public string Url { get; init; } = "";
    public int TimeoutSeconds { get; init; } = 30;
    public int StartupDelayMilliseconds { get; init; }
}

public sealed class TargetCapabilityProof
{
    public string Id { get; init; } = "";
    public bool Required { get; init; }
    public List<string> DockerExecArguments { get; init; } = [];
    public string ExpectedOutputContains { get; init; } = "";
}
