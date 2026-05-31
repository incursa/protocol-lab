// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public static class NetworkImpairmentProviders
{
    public const string None = "none";
    public const string DockerTc = "docker-tc";
    public const string Ns3Simulator = "ns3-simulator";
}

public sealed class NetworkProfileDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Provider { get; init; } = NetworkImpairmentProviders.None;
    public int? RttMilliseconds { get; init; }
    public double? LossPercent { get; init; }
    public int? BandwidthMegabitsPerSecond { get; init; }
    public string Notes { get; init; } = "";
}

public sealed record NetworkProfileSupport(bool IsSupported, string Reason)
{
    public static NetworkProfileSupport Supported { get; } = new(true, "supported");
}

public static class NetworkProfileSupportEvaluator
{
    public static NetworkProfileSupport Evaluate(NetworkProfileDefinition profile)
    {
        if (string.Equals(profile.Provider, NetworkImpairmentProviders.None, StringComparison.OrdinalIgnoreCase))
        {
            return NetworkProfileSupport.Supported;
        }

        return new NetworkProfileSupport(
            false,
            $"network provider '{profile.Provider}' is modeled but not implemented");
    }
}
