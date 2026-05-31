// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public enum LoadProfileStatus
{
    Stable,
    Experimental,
    Deprecated
}

public enum LoadProfilePurpose
{
    Smoke,
    Regression,
    Comparison,
    Stress,
    Soak,
    PublishableBenchmark
}

public enum LoadProfileEvidenceTier
{
    Smoke,
    LocalRegression,
    LocalComparison,
    Publishable
}

public sealed record HttpLoadProfile
{
    public int? Connections { get; init; }
    public int? Concurrency { get; init; }
    public int? RequestTimeoutSeconds { get; init; }
}

public sealed record Http2LoadProfile
{
    public int? Connections { get; init; }
    public int? Concurrency { get; init; }
    public int? StreamsPerConnection { get; init; }
    public int? RequestTimeoutSeconds { get; init; }
}

public sealed record Http3LoadProfile
{
    public int? Connections { get; init; }
    public int? Concurrency { get; init; }
    public int? StreamsPerConnection { get; init; }
    public int? RequestTimeoutSeconds { get; init; }
}

public sealed record QuicLoadProfile
{
    public int? Connections { get; init; }
    public int? StreamsPerConnection { get; init; }
    public int? StreamBytes { get; init; }
    public int? ConnectionTimeoutSeconds { get; init; }
    public int? IdleTimeoutSeconds { get; init; }
}

public sealed record LoadProfileTraffic
{
    public long? TotalRequests { get; init; }
    public int? RequestRateLimit { get; init; }
}

public sealed record LoadProfileEvidence
{
    public string MinimumTier { get; init; } = "local-comparison";
    public bool Publishable { get; init; }
}

public sealed record LoadProfileDefinition
{
    public required string SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }

    public string Status { get; init; } = "stable";
    public string Purpose { get; init; } = "comparison";

    public int? DurationSeconds { get; init; }
    public int? WarmupSeconds { get; init; }
    public int? CooldownSeconds { get; init; }
    public int Repetitions { get; init; } = 1;

    public LoadProfileTraffic? Traffic { get; init; }
    public HttpLoadProfile? Http { get; init; }
    public Http2LoadProfile? Http2 { get; init; }
    public Http3LoadProfile? Http3 { get; init; }
    public QuicLoadProfile? Quic { get; init; }

    public LoadProfileEvidence Evidence { get; init; } = new();
    public LoadToolArtifacts Artifacts { get; init; } = new();

    public bool IsStable()
    {
        return string.Equals(Status, "stable", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsExperimental()
    {
        return string.Equals(Status, "experimental", StringComparison.OrdinalIgnoreCase);
    }

    public LoadProfilePurpose GetPurpose()
    {
        return Purpose?.ToLowerInvariant() switch
        {
            "smoke" => LoadProfilePurpose.Smoke,
            "regression" => LoadProfilePurpose.Regression,
            "comparison" => LoadProfilePurpose.Comparison,
            "stress" => LoadProfilePurpose.Stress,
            "soak" => LoadProfilePurpose.Soak,
            "publishable-benchmark" => LoadProfilePurpose.PublishableBenchmark,
            _ => LoadProfilePurpose.Comparison
        };
    }

    public int? GetConnections(string protocol)
    {
        return protocol?.ToLowerInvariant() switch
        {
            "h1" or "http1" => Http?.Connections,
            "h2" or "http2" => Http2?.Connections ?? Http?.Connections,
            "h3" or "http3" => Http3?.Connections ?? Http?.Connections,
            "quic" => Quic?.Connections,
            _ => null
        };
    }

    public int? GetStreamsPerConnection(string protocol)
    {
        return protocol?.ToLowerInvariant() switch
        {
            "h2" or "http2" => Http2?.StreamsPerConnection,
            "h3" or "http3" => Http3?.StreamsPerConnection,
            "quic" => Quic?.StreamsPerConnection,
            _ => null
        };
    }

    public int? GetConcurrency(string protocol)
    {
        return protocol?.ToLowerInvariant() switch
        {
            "h1" or "http1" => Http?.Concurrency,
            "h2" or "http2" => Http2?.Concurrency,
            "h3" or "http3" => Http3?.Concurrency,
            _ => Http?.Concurrency
        };
    }
}
