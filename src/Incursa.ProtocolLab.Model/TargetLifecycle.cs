// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public static class TargetKinds
{
    public const string Process = "process";
    public const string Docker = "docker";
    public const string External = "external";
}

public static class TargetLifecycleSteps
{
    public const string Start = "start";
    public const string WaitReady = "waitReady";
    public const string CollectArtifacts = "collectArtifacts";
    public const string Stop = "stop";
}

public static class ReadinessCheckTypes
{
    public const string Http = "http";
    public const string Tcp = "tcp";
    public const string ProcessStarted = "processStarted";
    public const string None = "none";
}

public static class TargetExecutionStatuses
{
    public const string Started = "started";
    public const string Ready = "ready";
    public const string Failed = "failed";
    public const string Unsupported = "unsupported";
}

public static class TargetNetworkModes
{
    public const string PublishedPort = "published-port";
    public const string SharedDockerNetwork = "shared-docker-network";
}

public sealed record TargetExecutionResult
{
    public required string Status { get; init; }
    public string? TargetExecutionMode { get; init; }
    public string? TargetContract { get; init; }
    public bool Started { get; init; }
    public bool Ready { get; init; }
    public bool Failed { get; init; }
    public bool Unsupported { get; init; }
    public string? AdapterControlPlaneBaseUrl { get; init; }
    public string? AdapterSessionId { get; init; }
    public string? AdapterScenarioId { get; init; }
    public string? AdapterScenarioVersion { get; init; }
    public IReadOnlyList<string> AdapterEndpointTypes { get; init; } = [];
    public DateTimeOffset? StartTimeUtc { get; init; }
    public DateTimeOffset? ReadyTimeUtc { get; init; }
    public int? ProcessId { get; init; }
    public int? ExitCode { get; init; }
    public string? CommandLine { get; init; }
    public string? ExecutablePath { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? StdoutPath { get; init; }
    public string? StderrPath { get; init; }
    public string? LogsPath { get; init; }
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
    public string? LoadToolEffectiveUrl { get; init; }
    public string? HostRewriteMode { get; init; }
    public string? TargetDockerCommandLine { get; init; }
    public string? TargetDockerInspectPath { get; init; }
    public string? TargetDockerNetworkInspectPath { get; init; }
    public DockerResourceLimits? TargetDockerResourceLimitsRequested { get; init; }
    public DockerResourceLimits? TargetDockerResourceLimitsEffective { get; init; }
    public IReadOnlyList<string> ResourceLimitWarnings { get; init; } = [];
    public string? TargetCapabilityProofId { get; init; }
    public string? TargetCapabilityProofStatus { get; init; }
    public string? TargetCapabilityProofCommandLine { get; init; }
    public string? TargetCapabilityProofExpectedOutput { get; init; }
    public string? TargetCapabilityProofOutputPath { get; init; }
    public IReadOnlyList<string> TargetCapabilityProofWarnings { get; init; } = [];
    public DockerCleanupSummary? CleanupSummary { get; init; }
    public bool TargetDockerNetworkGenerated { get; init; }
    public string? NetworkCleanupStatus { get; init; }
    public IReadOnlyList<string> NetworkWarnings { get; init; } = [];
    public List<string> Errors { get; init; } = [];
    public List<string> Warnings { get; init; } = [];

    public static TargetExecutionResult ExternalReady(string baseUrl)
    {
        var now = DateTimeOffset.UtcNow;
        return new TargetExecutionResult
        {
            Status = TargetExecutionStatuses.Ready,
            TargetExecutionMode = TargetKinds.External,
            TargetContract = null,
            Started = false,
            Ready = true,
            StartTimeUtc = now,
            ReadyTimeUtc = now,
            Warnings = [$"Using external pre-started target at {baseUrl}."]
        };
    }

    public static TargetExecutionResult UnsupportedResult(string reason)
    {
        return new TargetExecutionResult
        {
            Status = TargetExecutionStatuses.Unsupported,
            Unsupported = true,
            TargetContract = null,
            Errors = [reason]
        };
    }
}
