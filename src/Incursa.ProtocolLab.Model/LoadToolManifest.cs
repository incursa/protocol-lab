// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

// ── Load Tool Contract v1 Enums ──────────────────────────────────────────────

public enum LoadToolKind
{
    Managed,
    Process,
    Docker
}

public enum LoadToolPurpose
{
    Validation,
    Benchmark,
    Profile,
    Diagnostic
}

public enum LoadToolParserKind
{
    None,
    ManagedHttp,
    H2Load,
    Wrk,
    Bombardier,
    RawQuic,
    Custom
}

// ── Load Tool Contract v1 Records ────────────────────────────────────────────

public sealed record LoadToolSupport
{
    public List<string> Protocols { get; init; } = [];
    public List<string> TrafficShapes { get; init; } = [];
    public List<string> Roles { get; init; } = [];
}

public sealed record LoadToolMetrics
{
    public List<string> Primary { get; init; } = [];
    public List<string> Secondary { get; init; } = [];
}

public sealed record LoadToolDockerExecution
{
    public string Image { get; init; } = "";
    public List<string> CommandTemplate { get; init; } = [];
    public string Command { get; init; } = "";
    public List<string> Arguments { get; init; } = [];
    public Dictionary<string, string> Environment { get; init; } = [];
    public bool AutoPull { get; init; }
    public string HostRewrite { get; init; } = "";
    public string Sni { get; init; } = "";
}

public sealed record LoadToolProcessExecution
{
    public string Executable { get; init; } = "";
    public List<string> DefaultArguments { get; init; } = [];
    public List<string> VersionCommand { get; init; } = [];
    public string AvailabilityCheck { get; init; } = "path";
}

public sealed record LoadToolExecution
{
    public LoadToolDockerExecution? Docker { get; init; }
    public LoadToolProcessExecution? Process { get; init; }
}

public sealed record LoadToolParser
{
    public string Type { get; init; } = "";
    public string? Id { get; init; }
    public bool PreservesRawOutput { get; init; } = true;
}

public sealed record LoadToolArtifacts
{
    public List<string> Required { get; init; } = [];
    public List<string> Optional { get; init; } = [];
}

public sealed record LoadToolDefinition
{
    public required string SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required LoadToolKind Kind { get; init; }

    public required LoadToolSupport Supports { get; init; }
    public required LoadToolMetrics Metrics { get; init; }
    public required LoadToolParser Parser { get; init; }

    public LoadToolExecution? Execution { get; init; }
    public LoadToolArtifacts Artifacts { get; init; } = new();
    public IReadOnlyList<LoadToolPurpose> Purposes { get; init; } = [LoadToolPurpose.Validation, LoadToolPurpose.Benchmark];
    public IReadOnlyList<string> Limitations { get; init; } = [];
    public string? Description { get; init; }

    public LoadToolManifest ToManifest()
    {
        var kind = Kind.ToString().ToLowerInvariant();
        var parserId = !string.IsNullOrWhiteSpace(Parser.Id) ? Parser.Id : Parser.Type;

        if (Execution?.Docker is not null)
        {
            return new LoadToolManifest
            {
                SchemaVersion = SchemaVersion,
                Id = Id,
                Name = Title,
                Title = Title,
                Description = Description ?? "",
                Kind = LoadToolKinds.Docker,
                SupportedProtocols = [..Supports.Protocols],
                SupportedScenarioFamilies = [],
                SupportedTrafficShapes = [..Supports.TrafficShapes],
                SupportedRoles = [..Supports.Roles],
                PrimaryMetrics = [..Metrics.Primary],
                SecondaryMetrics = [..Metrics.Secondary],
                ParserType = Parser.Type,
                PreservesRawOutput = Parser.PreservesRawOutput,
                RequiredArtifacts = [..Artifacts.Required],
                OptionalArtifacts = [..Artifacts.Optional],
                Purposes = [..Purposes.Select(p => p.ToString().ToLowerInvariant())],
                Limitations = [..Limitations],
                Notes = "",
                DockerImage = Execution.Docker.Image,
                DockerCommand = Execution.Docker.Command,
                DockerArguments = [..Execution.Docker.Arguments],
                DockerEnvironment = new Dictionary<string, string>(Execution.Docker.Environment),
                DockerAutoPull = Execution.Docker.AutoPull,
                DockerHostRewrite = Execution.Docker.HostRewrite,
                Sni = Execution.Docker.Sni,
                DefaultArguments = [..Execution.Docker.CommandTemplate],
                OutputParserId = parserId
            };
        }

        if (Execution?.Process is not null)
        {
            return new LoadToolManifest
            {
                SchemaVersion = SchemaVersion,
                Id = Id,
                Name = Title,
                Title = Title,
                Description = Description ?? "",
                Kind = LoadToolKinds.Process,
                SupportedProtocols = [..Supports.Protocols],
                SupportedScenarioFamilies = [],
                SupportedTrafficShapes = [..Supports.TrafficShapes],
                SupportedRoles = [..Supports.Roles],
                PrimaryMetrics = [..Metrics.Primary],
                SecondaryMetrics = [..Metrics.Secondary],
                ParserType = Parser.Type,
                PreservesRawOutput = Parser.PreservesRawOutput,
                RequiredArtifacts = [..Artifacts.Required],
                OptionalArtifacts = [..Artifacts.Optional],
                Purposes = [..Purposes.Select(p => p.ToString().ToLowerInvariant())],
                Limitations = [..Limitations],
                Notes = "",
                Executable = Execution.Process.Executable,
                DefaultArguments = [..Execution.Process.DefaultArguments],
                VersionCommand = [..Execution.Process.VersionCommand],
                AvailabilityCheck = Execution.Process.AvailabilityCheck,
                OutputParserId = parserId
            };
        }

        return new LoadToolManifest
        {
            SchemaVersion = SchemaVersion,
            Id = Id,
            Name = Title,
            Title = Title,
            Description = Description ?? "",
            Kind = kind,
            SupportedProtocols = [..Supports.Protocols],
            SupportedScenarioFamilies = [],
            SupportedTrafficShapes = [..Supports.TrafficShapes],
            SupportedRoles = [..Supports.Roles],
            PrimaryMetrics = [..Metrics.Primary],
            SecondaryMetrics = [..Metrics.Secondary],
            ParserType = Parser.Type,
            PreservesRawOutput = Parser.PreservesRawOutput,
            RequiredArtifacts = [..Artifacts.Required],
            OptionalArtifacts = [..Artifacts.Optional],
            Purposes = [..Purposes.Select(p => p.ToString().ToLowerInvariant())],
            Limitations = [..Limitations],
            Notes = "",
            OutputParserId = parserId
        };
    }
}

// ── Existing Load Tool Manifest (extended with v1 fields) ────────────────────

public sealed class LoadToolManifest
{
    // ── v1 schema fields ──
    public string SchemaVersion { get; init; } = "";
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Kind { get; init; } = TargetKinds.Process;
    public string Category { get; init; } = LoadToolCategories.ExternalReference;

    // ── supports ──
    public LoadToolSupport? Supports { get; init; }
    public List<string> SupportedProtocols { get; init; } = [];
    public List<string> SupportedScenarioFamilies { get; init; } = [];
    public List<string> SupportedTrafficShapes { get; init; } = [];
    public List<string> SupportedRoles { get; init; } = [];

    // ── metrics ──
    public LoadToolMetrics? Metrics { get; init; }
    public List<string> PrimaryMetrics { get; init; } = [];
    public List<string> SecondaryMetrics { get; init; } = [];

    // ── execution (process) ──
    public string Executable { get; init; } = "";
    public List<string> DefaultArguments { get; init; } = [];
    public List<string> VersionCommand { get; init; } = [];
    public string AvailabilityCheck { get; init; } = "path";

    // ── execution (docker) ──
    public string DockerImage { get; init; } = "";
    public string DockerCommand { get; init; } = "";
    public List<string> DockerArguments { get; init; } = [];
    public Dictionary<string, string> DockerEnvironment { get; init; } = [];
    public DockerResourceLimits? LoadToolDockerResourceLimits { get; init; }
    public bool DockerAutoPull { get; init; }
    public string DockerHostRewrite { get; init; } = "";
    public string Sni { get; init; } = "";

    // ── parser ──
    public LoadToolParser? Parser { get; init; }
    public string OutputParserId { get; init; } = "";
    public string ParserType { get; init; } = "";
    public bool PreservesRawOutput { get; init; } = true;

    // ── artifacts ──
    public LoadToolArtifacts? Artifacts { get; init; }
    public List<string> RequiredArtifacts { get; init; } = [];
    public List<string> OptionalArtifacts { get; init; } = [];

    // ── purposes & limitations ──
    public List<string> Purposes { get; init; } = ["validation", "benchmark"];
    public List<string> Limitations { get; init; } = [];

    // ── legacy ──
    public string Notes { get; init; } = "";

    // ── v1 convenience accessors ──

    public LoadToolDefinition ToDefinition()
    {
        var kind = Kind?.ToLowerInvariant() switch
        {
            "managed" => LoadToolKind.Managed,
            "docker" => LoadToolKind.Docker,
            _ => LoadToolKind.Process
        };

        LoadToolExecution? execution = null;
        if (string.Equals(Kind, LoadToolKinds.Docker, StringComparison.OrdinalIgnoreCase))
        {
            execution = new LoadToolExecution
            {
                Docker = new LoadToolDockerExecution
                {
                    Image = DockerImage,
                    Command = DockerCommand,
                    Arguments = [..DockerArguments],
                    Environment = new Dictionary<string, string>(DockerEnvironment),
                    AutoPull = DockerAutoPull,
                    HostRewrite = DockerHostRewrite,
                    Sni = Sni,
                    CommandTemplate = [..DefaultArguments]
                }
            };
        }
        else if (string.Equals(Kind, LoadToolKinds.Process, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Kind, TargetKinds.Process, StringComparison.OrdinalIgnoreCase))
        {
            execution = new LoadToolExecution
            {
                Process = new LoadToolProcessExecution
                {
                    Executable = Executable,
                    DefaultArguments = [..DefaultArguments],
                    VersionCommand = [..VersionCommand],
                    AvailabilityCheck = AvailabilityCheck
                }
            };
        }

        var parserType = GetEffectiveParserType();
        var purposedList = new List<LoadToolPurpose>();
        foreach (var p in Purposes)
        {
            if (Enum.TryParse<LoadToolPurpose>(p, ignoreCase: true, out var parsed))
            {
                purposedList.Add(parsed);
            }
        }

        return new LoadToolDefinition
        {
            SchemaVersion = !string.IsNullOrWhiteSpace(SchemaVersion) ? SchemaVersion : "protocol-lab.load-tool.v1",
            Id = Id,
            Title = !string.IsNullOrWhiteSpace(Title) ? Title : Name,
            Kind = kind,
            Supports = new LoadToolSupport
            {
                Protocols = [..GetEffectiveProtocols()],
                TrafficShapes = [..GetEffectiveTrafficShapes()],
                Roles = [..GetEffectiveRoles()]
            },
            Metrics = new LoadToolMetrics
            {
                Primary = [..GetEffectivePrimaryMetrics()],
                Secondary = [..GetEffectiveSecondaryMetrics()]
            },
            Parser = new LoadToolParser
            {
                Type = parserType,
                Id = !string.IsNullOrWhiteSpace(OutputParserId) ? OutputParserId : null,
                PreservesRawOutput = PreservesRawOutput
            },
            Execution = execution,
            Artifacts = new LoadToolArtifacts
            {
                Required = [..RequiredArtifacts],
                Optional = [..OptionalArtifacts]
            },
            Purposes = purposedList.Count > 0 ? purposedList : [LoadToolPurpose.Validation, LoadToolPurpose.Benchmark],
            Limitations = [..Limitations],
            Description = Description
        };
    }

    public bool SupportsTrafficShape(string trafficShape)
    {
        var shapes = GetEffectiveTrafficShapes();
        if (shapes.Count == 0)
        {
            return true;
        }

        return shapes.Contains(trafficShape, StringComparer.OrdinalIgnoreCase) ||
               shapes.Contains(
                   NormalizeTrafficShape(trafficShape),
                   StringComparer.OrdinalIgnoreCase);
    }

    public bool SupportsProtocol(string protocol)
    {
        return GetEffectiveProtocols().Contains(protocol, StringComparer.OrdinalIgnoreCase);
    }

    public bool SupportsRole(string role)
    {
        var roles = GetEffectiveRoles();
        if (roles.Count == 0)
        {
            return true;
        }

        return roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetEffectiveProtocols()
    {
        if (Supports?.Protocols is { Count: > 0 })
        {
            return Supports.Protocols;
        }

        return SupportedProtocols;
    }

    public IReadOnlyList<string> GetEffectiveTrafficShapes()
    {
        if (Supports?.TrafficShapes is { Count: > 0 })
        {
            return Supports.TrafficShapes;
        }

        return SupportedTrafficShapes;
    }

    public IReadOnlyList<string> GetEffectiveRoles()
    {
        if (Supports?.Roles is { Count: > 0 })
        {
            return Supports.Roles;
        }

        return SupportedRoles;
    }

    public IReadOnlyList<string> GetEffectivePrimaryMetrics()
    {
        if (Metrics?.Primary is { Count: > 0 })
        {
            return Metrics.Primary;
        }

        return PrimaryMetrics;
    }

    public IReadOnlyList<string> GetEffectiveSecondaryMetrics()
    {
        if (Metrics?.Secondary is { Count: > 0 })
        {
            return Metrics.Secondary;
        }

        return SecondaryMetrics;
    }

    public string GetEffectiveParserType()
    {
        if (Parser is not null && !string.IsNullOrWhiteSpace(Parser.Type))
        {
            return Parser.Type;
        }

        if (!string.IsNullOrWhiteSpace(ParserType))
        {
            return ParserType;
        }

        return OutputParserId;
    }

    public IReadOnlyList<string> GetEffectiveRequiredArtifacts()
    {
        if (Artifacts?.Required is { Count: > 0 })
        {
            return Artifacts.Required;
        }

        return RequiredArtifacts;
    }

    public IReadOnlyList<string> GetEffectiveOptionalArtifacts()
    {
        if (Artifacts?.Optional is { Count: > 0 })
        {
            return Artifacts.Optional;
        }

        return OptionalArtifacts;
    }

    private static string NormalizeTrafficShape(string shape)
    {
        return shape switch
        {
            "request-response" or "requestResponse" => "request-response",
            "streaming-download" or "streamingDownload" => "streaming-download",
            "streaming-upload" or "streamingUpload" => "streaming-upload",
            "bidirectional-stream" or "bidirectionalStream" => "bidirectional-stream",
            "handshake-only" or "handshakeOnly" => "handshake-only",
            "connection-lifecycle" or "connectionLifecycle" => "connection-lifecycle",
            _ => shape
        };
    }
}

public static class LoadToolKinds
{
    public const string Process = "process";
    public const string Docker = "docker";
    public const string Managed = "managed";
    public const string External = "external";
}

public static class LoadToolCategories
{
    public const string ExternalReference = "external-reference";
    public const string ManagedLab = "managed-lab";
    public const string Experimental = "experimental";
}

public static class LoadToolExecutionStatuses
{
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
    public const string Unavailable = "load-tool-unavailable";
    public const string Unsupported = "unsupported";
}

public sealed record LoadToolExecutionResult
{
    public required string Status { get; init; }
    public string? ToolId { get; init; }
    public string? ToolName { get; init; }
    public string? Mode { get; init; }
    public string? Category { get; init; }
    public string? ExecutablePath { get; init; }
    public string? DockerImage { get; init; }
    public string? DockerCommandLine { get; init; }
    public string? RequestedUrl { get; init; }
    public string? EffectiveUrl { get; init; }
    public string? ConnectTarget { get; init; }
    public string? HostRewriteMode { get; init; }
    public string? Sni { get; init; }
    public string? ContainerNetwork { get; init; }
    public string? CertificateMode { get; init; }
    public string? Version { get; init; }
    public string? H3CapabilityStatus { get; init; }
    public IReadOnlyList<string> H3CapabilityWarnings { get; init; } = [];
    public string? DockerImageId { get; init; }
    public string? DockerImageDigest { get; init; }
    public int? ContainerExitCode { get; init; }
    public string? ContainerId { get; init; }
    public string? ContainerName { get; init; }
    public string? DockerInspectPath { get; init; }
    public DockerResourceLimits? LoadToolDockerResourceLimitsRequested { get; init; }
    public DockerResourceLimits? LoadToolDockerResourceLimitsEffective { get; init; }
    public IReadOnlyList<string> ResourceLimitWarnings { get; init; } = [];
    public bool LoadToolDockerMetricsAvailable { get; init; }
    public DockerContainerMetricsSummary? LoadToolDockerMetricsSummary { get; init; }
    public string? LoadToolSaturationStatus { get; init; }
    public IReadOnlyList<string> LoadToolSaturationWarnings { get; init; } = [];
    public Dictionary<string, string> LoadToolDockerMetricsArtifacts { get; init; } = [];
    public DockerCleanupSummary? CleanupSummary { get; init; }
    public string? CommandLine { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? ParserId { get; init; }
    public int? ExitCode { get; init; }
    public DateTimeOffset? StartTimeUtc { get; init; }
    public DateTimeOffset? EndTimeUtc { get; init; }
    public string? StdoutPath { get; init; }
    public string? StderrPath { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
