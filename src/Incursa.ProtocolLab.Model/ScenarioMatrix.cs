// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public sealed record MatrixOptions(
    IReadOnlyCollection<string>? ImplementationIds = null,
    IReadOnlyCollection<string>? ScenarioIds = null,
    IReadOnlyCollection<string>? Protocols = null,
    IReadOnlyCollection<int>? Connections = null,
    IReadOnlyCollection<int>? StreamsPerConnection = null,
    int? DurationSeconds = null,
    int? WarmupSeconds = null,
    int? Repetitions = null,
    IReadOnlyCollection<string>? NetworkProfiles = null,
    string? LoadProfileId = null);

public sealed record RunCell(
    ImplementationManifest Implementation,
    ScenarioDefinition Scenario,
    string Protocol,
    int Connections,
    int StreamsPerConnection,
    int Repetition,
    int DurationSeconds,
    int WarmupSeconds,
    string NetworkProfile,
    string? LoadProfileId = null);

public static class ScenarioMatrix
{
    public static IReadOnlyList<RunCell> Expand(
        IReadOnlyList<ImplementationManifest> implementations,
        IReadOnlyList<ScenarioDefinition> scenarios,
        MatrixOptions options)
    {
        var selectedImplementations = FilterById(implementations, options.ImplementationIds, static item => item.Id);
        var selectedScenarios = FilterById(scenarios, options.ScenarioIds, static item => item.Id);
        var cells = new List<RunCell>();

        foreach (var implementation in selectedImplementations)
        {
            foreach (var scenario in selectedScenarios)
            {
                var protocols = options.Protocols is { Count: > 0 } ? options.Protocols : [scenario.Protocol];
                var connections = options.Connections is { Count: > 0 } ? options.Connections : scenario.Benchmark.Connections;
                var streams = options.StreamsPerConnection is { Count: > 0 } ? options.StreamsPerConnection : scenario.Benchmark.StreamsPerConnection;
                var repetitions = options.Repetitions ?? scenario.Benchmark.Repetitions;
                var networkProfiles = options.NetworkProfiles is { Count: > 0 } ? options.NetworkProfiles : [scenario.NetworkProfile];

                foreach (var protocol in protocols)
                {
                    foreach (var connection in connections)
                    {
                        foreach (var stream in streams)
                        {
                            foreach (var networkProfile in networkProfiles)
                            {
                                for (var repetition = 1; repetition <= repetitions; repetition++)
                                {
                                    cells.Add(new RunCell(
                                        implementation,
                                        scenario,
                                        protocol,
                                        connection,
                                        stream,
                                        repetition,
                                        options.DurationSeconds ?? scenario.Benchmark.DurationSeconds,
                                        options.WarmupSeconds ?? scenario.Benchmark.WarmupSeconds,
                                        networkProfile,
                                        options.LoadProfileId));
                                }
                            }
                        }
                    }
                }
            }
        }

        return cells;
    }

    private static IReadOnlyList<T> FilterById<T>(
        IReadOnlyList<T> items,
        IReadOnlyCollection<string>? ids,
        Func<T, string> getId)
    {
        if (ids is null || ids.Count == 0)
        {
            return items;
        }

        var selected = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        return items.Where(item => selected.Contains(getId(item))).ToArray();
    }
}
