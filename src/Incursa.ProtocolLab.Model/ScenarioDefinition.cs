// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public sealed class ScenarioDefinition
{
    public string SchemaVersion { get; init; } = "";
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Name { get; init; } = "";
    public string Family { get; init; } = "";
    public string Version { get; init; } = "";
    public string Description { get; init; } = "";
    public string Status { get; init; } = "draft";
    public string Kind { get; init; } = "workload";
    public string Layer { get; init; } = "application";
    public string Protocol { get; init; } = "";
    public string ImplementationRole { get; init; } = "server";
    public List<string> Roles { get; init; } = [];
    public List<string> RequiredCapabilities { get; init; } = [];
    public ScenarioRequires Requires { get; init; } = new();
    public string TrafficShape { get; init; } = "";
    public HttpEndpointSpec? Endpoint { get; init; }
    public H3ProtocolSpec? H3Protocol { get; init; }
    public QuicTransportSpec? QuicTransport { get; init; }
    public WebTransportSpec? WebTransport { get; init; }
    public MasqueSpec? Masque { get; init; }
    public Dictionary<string, string> Parameters { get; init; } = [];
    public ValidationRules Validation { get; init; } = new();
    public BenchmarkLoadShape Benchmark { get; init; } = new();
    public ScenarioBenchmarkCompat? BenchmarkCompat { get; init; }
    public ScenarioArtifacts Artifacts { get; init; } = new();
    public ScenarioComparability? Comparability { get; init; }
    public string NetworkProfile { get; init; } = "clean";
    public List<string> RequiredMetrics { get; init; } = [];
    public List<string> ArtifactRequirements { get; init; } = [];
    public List<string> Tags { get; init; } = [];

    public string GetTitle()
    {
        if (!string.IsNullOrWhiteSpace(Title))
        {
            return Title;
        }

        return Name;
    }

    public bool IsPlaceholder()
    {
        return string.Equals(Status, "placeholder", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsExperimental()
    {
        return string.Equals(Status, "experimental", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsStable()
    {
        return string.Equals(Status, "stable", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Status, "draft", StringComparison.OrdinalIgnoreCase) ||
               string.IsNullOrWhiteSpace(Status);
    }

    public IReadOnlyList<string> GetEffectiveRoles()
    {
        if (Roles.Count > 0)
        {
            return Roles;
        }

        if (!string.IsNullOrWhiteSpace(ImplementationRole))
        {
            return [ImplementationRole];
        }

        return [];
    }

    public IReadOnlyList<string> GetEffectiveRequiredCapabilities()
    {
        if (Requires.Capabilities.Count > 0)
        {
            return Requires.Capabilities;
        }

        return RequiredCapabilities;
    }

    public IReadOnlyList<string> GetEffectiveProtocols()
    {
        if (Requires.Protocols.Count > 0)
        {
            return Requires.Protocols;
        }

        if (!string.IsNullOrWhiteSpace(Protocol))
        {
            return [Protocol];
        }

        return [];
    }
}

public sealed class HttpEndpointSpec
{
    public string Method { get; init; } = "GET";
    public string Path { get; init; } = "";
    public Dictionary<string, string> Query { get; init; } = [];
    public Dictionary<string, string> RequestHeaders { get; init; } = [];
    public string? RequestBodyGeneration { get; init; }
    public int ExpectedStatus { get; init; } = 200;
    public Dictionary<string, string> ExpectedHeaders { get; init; } = [];
    public string? ExpectedHeaderPrefix { get; init; }
    public int? ExpectedHeaderCount { get; init; }
    public int? ExpectedHeaderValueSize { get; init; }
    public string ExpectedBodyRule { get; init; } = "";
    public string? ExpectedBody { get; init; }
    public int? ExpectedBodySize { get; init; }
    public Dictionary<string, string> ExpectedJsonProperties { get; init; } = [];
    public List<string> ExpectedJsonKeys { get; init; } = [];
}

public sealed class H3ProtocolSpec
{
    public string Behavior { get; init; } = "";
    public string RequestPath { get; init; } = "";
    public int? RepeatedHeaderCount { get; init; }
    public int? HeaderValueSize { get; init; }
    public string? CancellationPoint { get; init; }
    public int? ResponseBytesBeforeCancel { get; init; }
    public int? ConcurrentRequestStreams { get; init; }
    public List<string> ExpectedFrameTypes { get; init; } = [];
}

public sealed class QuicTransportSpec
{
    public string Behavior { get; init; } = "";
    public int ConnectionCount { get; init; } = 1;
    public string StreamType { get; init; } = "";
    public int StreamCount { get; init; } = 1;
    public int? PayloadSizeBytes { get; init; }
    public string PayloadDirection { get; init; } = "";
    public string OpenPattern { get; init; } = "";
    public long? ExpectedBytes { get; init; }
    public bool DatagramEnabled { get; init; }
    public string? CancellationMode { get; init; }
    public string? ResetBehavior { get; init; }
}

public sealed class WebTransportSpec
{
    public string Behavior { get; init; } = "";
    public string SessionPath { get; init; } = "";
    public string StreamType { get; init; } = "";
    public int StreamCount { get; init; } = 1;
    public int? PayloadSizeBytes { get; init; }
    public string PayloadDirection { get; init; } = "";
}

public sealed class MasqueSpec
{
    public string Behavior { get; init; } = "";
    public string TunnelMode { get; init; } = "";
    public string TargetAuthority { get; init; } = "";
    public string DatagramPolicy { get; init; } = "";
}

public sealed class ValidationRules
{
    public bool Required { get; init; } = true;
    public List<string> Checks { get; init; } = [];
}

public sealed class BenchmarkLoadShape
{
    public int WarmupSeconds { get; init; } = 5;
    public int DurationSeconds { get; init; } = 30;
    public int Repetitions { get; init; } = 1;
    public List<int> Connections { get; init; } = [1];
    public List<int> StreamsPerConnection { get; init; } = [1];
}
