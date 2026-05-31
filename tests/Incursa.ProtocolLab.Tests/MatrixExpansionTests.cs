// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Tests;

public sealed class MatrixExpansionTests
{
    [Fact]
    public void Expands_selected_scenario_matrix()
    {
        var manifest = new ImplementationManifest { Id = "kestrel-http3", Name = "Kestrel" };
        var scenario = new ScenarioDefinition
        {
            Id = "http.core.plaintext",
            Protocol = "h3",
            NetworkProfile = "clean",
            Benchmark = new BenchmarkLoadShape
            {
                DurationSeconds = 30,
                WarmupSeconds = 5,
                Repetitions = 2,
                Connections = [1, 16],
                StreamsPerConnection = [1, 10]
            }
        };

        var cells = ScenarioMatrix.Expand([manifest], [scenario], new MatrixOptions());

        Assert.Equal(8, cells.Count);
        Assert.Contains(cells, cell => cell.Connections == 16 && cell.StreamsPerConnection == 10 && cell.Repetition == 2);
        Assert.All(cells, cell => Assert.Equal("h3", cell.Protocol));
    }

    [Fact]
    public void Expands_selected_network_profiles()
    {
        var manifest = new ImplementationManifest { Id = "kestrel-http3", Name = "Kestrel" };
        var scenario = new ScenarioDefinition
        {
            Id = "http.core.plaintext",
            Protocol = "h3",
            NetworkProfile = "clean",
            Benchmark = new BenchmarkLoadShape
            {
                DurationSeconds = 30,
                WarmupSeconds = 5,
                Repetitions = 1,
                Connections = [1],
                StreamsPerConnection = [1]
            }
        };

        var cells = ScenarioMatrix.Expand(
            [manifest],
            [scenario],
            new MatrixOptions(NetworkProfiles: ["clean", "rtt-25ms"]));

        Assert.Equal(2, cells.Count);
        Assert.Contains(cells, cell => cell.NetworkProfile == "clean");
        Assert.Contains(cells, cell => cell.NetworkProfile == "rtt-25ms");
    }
}
