// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

public sealed class RunPlanBuilder
{
    public IReadOnlyList<RunCell> Build(string root, RunnerCommandOptions options)
    {
        var implementations = ManifestCatalog.Load(Path.Combine(root, "implementations"));
        var scenarios = ScenarioCatalog.Load(Path.Combine(root, "scenarios"));
        var profiles = LoadProfileCatalog.Load(Path.Combine(root, "load-profiles"));
        var matrixOptions = BuildMatrixOptions(options, profiles);

        return Build(implementations, scenarios, matrixOptions);
    }

    public IReadOnlyList<RunCell> Build(
        IReadOnlyList<ImplementationManifest> implementations,
        IReadOnlyList<ScenarioDefinition> scenarios,
        MatrixOptions options)
    {
        var cells = ScenarioMatrix.Expand(implementations, scenarios, options);
        if (cells.Count == 0)
        {
            throw new InvalidOperationException("No run cells were selected. Check implementation and scenario IDs.");
        }

        return cells;
    }

    public static MatrixOptions BuildMatrixOptions(RunnerCommandOptions options, IReadOnlyList<LoadProfileDefinition>? profiles = null)
    {
        var loadProfileId = options.Get("load-profile") ?? options.Get("profile");
        var protocols = SplitCsv(options.Get("protocol"));
        var matrixOptions = new MatrixOptions(
            SplitCsv(options.Get("implementations") ?? options.Get("implementation")),
            SplitCsv(options.Get("scenarios") ?? options.Get("scenario")),
            protocols is { Count: > 0 }
                ? protocols.Select(ProtocolIds.Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                : null,
            SplitIntCsv(options.Get("concurrency") ?? options.Get("connections")),
            SplitIntCsv(options.Get("streams-per-connection") ?? options.Get("streams")),
            ParseInt(options.Get("duration")),
            ParseInt(options.Get("warmup")),
            ParseInt(options.Get("repetitions")),
            SplitCsv(options.Get("network-profiles") ?? options.Get("network-profile")),
            null);

        if (!string.IsNullOrWhiteSpace(loadProfileId) && profiles is { Count: > 0 })
        {
            var profile = profiles.FirstOrDefault(p =>
                string.Equals(p.Id, loadProfileId, StringComparison.OrdinalIgnoreCase));
            if (profile is not null)
            {
                matrixOptions = MergeProfileIntoOptions(matrixOptions, profile);
            }
        }

        return matrixOptions;
    }

    public static MatrixOptions MergeProfileIntoOptions(MatrixOptions options, LoadProfileDefinition profile)
    {
        return new MatrixOptions(
            options.ImplementationIds,
            options.ScenarioIds,
            options.Protocols,
            options.Connections,
            options.StreamsPerConnection,
            options.DurationSeconds ?? profile.DurationSeconds,
            options.WarmupSeconds ?? profile.WarmupSeconds,
            options.Repetitions ?? profile.Repetitions,
            options.NetworkProfiles,
            profile.Id);
    }

    private static IReadOnlyCollection<string>? SplitCsv(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static IReadOnlyCollection<int>? SplitIntCsv(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
    }

    private static int? ParseInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }
}
