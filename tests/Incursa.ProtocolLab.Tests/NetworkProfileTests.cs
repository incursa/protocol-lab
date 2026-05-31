// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Tests;

public sealed class NetworkProfileTests
{
    [Fact]
    public void Loads_network_profiles_without_adding_scenarios()
    {
        var scenarios = ScenarioCatalog.Load(Path.Combine(TestPaths.RepoRoot, "scenarios"));
        var profiles = NetworkProfileCatalog.Load(Path.Combine(TestPaths.RepoRoot, "scenarios", "network", "profiles"));

        Assert.Equal(25, scenarios.Count);
        Assert.Equal(6, profiles.Count);
        Assert.Contains(profiles, profile => profile.Id == "clean");
        Assert.Contains(profiles, profile => profile.Id == "rtt-25ms" && profile.RttMilliseconds == 25);
        Assert.Contains(profiles, profile => profile.Id == "loss-0.1pct" && profile.LossPercent == 0.1);
    }

    [Fact]
    public void Supports_only_none_provider_initially()
    {
        var clean = new NetworkProfileDefinition
        {
            Id = "clean",
            Provider = NetworkImpairmentProviders.None
        };
        var dockerTc = new NetworkProfileDefinition
        {
            Id = "rtt-25ms",
            Provider = NetworkImpairmentProviders.DockerTc
        };
        var ns3 = new NetworkProfileDefinition
        {
            Id = "ns3-future",
            Provider = NetworkImpairmentProviders.Ns3Simulator
        };

        Assert.True(NetworkProfileSupportEvaluator.Evaluate(clean).IsSupported);

        var dockerSupport = NetworkProfileSupportEvaluator.Evaluate(dockerTc);
        Assert.False(dockerSupport.IsSupported);
        Assert.Contains("docker-tc", dockerSupport.Reason);

        var ns3Support = NetworkProfileSupportEvaluator.Evaluate(ns3);
        Assert.False(ns3Support.IsSupported);
        Assert.Contains("ns3-simulator", ns3Support.Reason);
    }
}
