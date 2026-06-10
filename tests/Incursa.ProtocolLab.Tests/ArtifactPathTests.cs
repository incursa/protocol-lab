// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Tests;

public sealed class ArtifactPathTests
{
    [Fact]
    public void Generates_deterministic_cell_paths()
    {
        var cell = new RunCell(
            new ImplementationManifest { Id = "kestrel-http3", Name = "Kestrel" },
            new ScenarioDefinition { Id = "http.core.plaintext", Protocol = "h3" },
            "h3",
            16,
            10,
            3,
            30,
            5,
            "clean");

        var paths = ArtifactLayout.GetCellPaths(".artifacts/runs", "run-1", cell);
        var expectedCellRoot = Path.Combine(
            "kestrel-http3",
            "http.core.plaintext",
            "h3",
            "local-process",
            "clean",
            "no-load-profile",
            "c16-s10-r3");

        Assert.EndsWith(Path.Combine(expectedCellRoot, "result.json"), paths.ResultJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "validation.json"), paths.ValidationJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "validation-proof"), paths.ValidationProofDirectory);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "validation-proof", "request.json"), paths.ValidationProofRequestJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "validation-proof", "response.json"), paths.ValidationProofResponseJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "validation-proof", "protocol.json"), paths.ValidationProofProtocolJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "validation-proof", "transport.json"), paths.ValidationProofTransportJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "validation-proof", "headers.json"), paths.ValidationProofHeadersJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "validation-proof", "body-summary.json"), paths.ValidationProofBodySummaryJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "validation-proof", "notes.md"), paths.ValidationProofNotes);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "protocol-proof.json"), paths.ProtocolProofJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "protocol-proof.stdout.txt"), paths.ProtocolProofStdout);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "protocol-proof.stderr.txt"), paths.ProtocolProofStderr);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "load-tool.stdout.txt"), paths.LoadToolStdout);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "load-tool.stderr.txt"), paths.LoadToolStderr);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "load-tool-command.txt"), paths.LoadToolCommandTxt);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "load-tool-docker-inspect.json"), paths.LoadToolDockerInspectJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "load-tool-docker-stats.raw.txt"), paths.LoadToolDockerStatsRawTxt);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "load-tool-docker-stats.jsonl"), paths.LoadToolDockerStatsJsonl);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "load-tool-docker-metrics-summary.json"), paths.LoadToolDockerMetricsSummaryJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "load-tool.version.txt"), paths.LoadToolVersion);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "load-tool-execution.json"), paths.LoadToolExecutionJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "target.stdout.txt"), paths.TargetStdout);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "target.stderr.txt"), paths.TargetStderr);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "target-docker-inspect.json"), paths.TargetDockerInspectJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "target-docker-network-inspect.json"), paths.TargetDockerNetworkInspectJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "target-docker-command.txt"), paths.TargetDockerCommandTxt);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "target-docker-stats.raw.txt"), paths.TargetDockerStatsRawTxt);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "target-docker-stats.jsonl"), paths.TargetDockerStatsJsonl);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "target-docker-metrics-summary.json"), paths.TargetDockerMetricsSummaryJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "docker-network-command.txt"), paths.DockerNetworkCommandTxt);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "docker-network-inspect.json"), paths.DockerNetworkInspectJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "docker-network-cleanup.txt"), paths.DockerNetworkCleanupTxt);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "docker-resource-limits.json"), paths.DockerResourceLimitsJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "docker-cleanup.json"), paths.DockerCleanupJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "manifest.json"), paths.ManifestSnapshotJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "scenario.json"), paths.ScenarioSnapshotJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "target-execution.json"), paths.TargetExecutionJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "diagnostic-target.json"), paths.DiagnosticTargetJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "counters.stdout.txt"), paths.CountersStdout);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "counters.stderr.txt"), paths.CountersStderr);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "counters.raw.json"), paths.CountersRawJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "counters.raw.csv"), paths.CountersRawCsv);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "counters-summary.json"), paths.CountersSummaryJson);
        Assert.EndsWith(Path.Combine(expectedCellRoot, "qlog"), paths.QlogDirectory);
    }
}
