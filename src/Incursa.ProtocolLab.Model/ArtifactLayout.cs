// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public sealed record ArtifactPaths(
    string CellDirectory,
    string ResultJson,
    string ValidationJson,
    string ValidationProofDirectory,
    string ValidationProofRequestJson,
    string ValidationProofResponseJson,
    string ValidationProofProtocolJson,
    string ValidationProofTransportJson,
    string ValidationProofHeadersJson,
    string ValidationProofBodySummaryJson,
    string ValidationProofNotes,
    string ProtocolProofJson,
    string ProtocolProofStdout,
    string ProtocolProofStderr,
    string LoadToolStdout,
    string LoadToolStderr,
    string H2loadStdout,
    string H2loadStderr,
    string H2loadOutputJson,
    string H2loadCommandTxt,
    string DockerCommandTxt,
    string LoadToolDockerInspectJson,
    string LoadToolDockerStatsRawTxt,
    string LoadToolDockerStatsJsonl,
    string LoadToolDockerMetricsSummaryJson,
    string LoadToolVersion,
    string LoadToolExecutionJson,
    string ServerStdout,
    string ServerStderr,
    string TargetStdout,
    string TargetStderr,
    string DockerInspectJson,
    string TargetDockerInspectJson,
    string TargetDockerCommandTxt,
    string TargetDockerNetworkInspectJson,
    string TargetDockerStatsRawTxt,
    string TargetDockerStatsJsonl,
    string TargetDockerMetricsSummaryJson,
    string DockerNetworkCommandTxt,
    string DockerNetworkInspectJson,
    string DockerNetworkCleanupTxt,
    string DockerResourceLimitsJson,
    string DockerCleanupJson,
    string ManifestSnapshotJson,
    string ScenarioSnapshotJson,
    string TargetExecutionJson,
    string DiagnosticTargetJson,
    string AdapterHealthJson,
    string AdapterManifestJson,
    string AdapterSessionCreateJson,
    string AdapterPrepareJson,
    string AdapterStartJson,
    string AdapterStatusJsonl,
    string AdapterEndpointsJson,
    string AdapterMetricsJson,
    string AdapterArtifactsJson,
    string AdapterStopJson,
    string AdapterDeleteJson,
    string CountersStdout,
    string CountersStderr,
    string CountersRawJson,
    string CountersRawCsv,
    string CountersSummaryJson,
    string QlogDirectory,
    string SslKeyLogDirectory,
    string PcapDirectory,
    string Notes);

public static class ArtifactLayout
{
    public static string GetRunRoot(string outputRoot, string runId)
    {
        return Path.Combine(outputRoot, runId);
    }

    public static string GetCellDirectory(string outputRoot, string runId, RunCell cell)
    {
        return Path.Combine(
            GetRunRoot(outputRoot, runId),
            "implementations",
            SanitizeSegment(cell.Implementation.Id),
            SanitizeSegment(cell.Scenario.Id),
            SanitizeSegment(cell.Protocol),
            $"c{cell.Connections}-s{cell.StreamsPerConnection}-r{cell.Repetition}");
    }

    public static ArtifactPaths GetCellPaths(string outputRoot, string runId, RunCell cell)
    {
        var cellDirectory = GetCellDirectory(outputRoot, runId, cell);
        var validationProofDirectory = Path.Combine(cellDirectory, "validation-proof");

        return new ArtifactPaths(
            cellDirectory,
            Path.Combine(cellDirectory, "result.json"),
            Path.Combine(cellDirectory, "validation.json"),
            validationProofDirectory,
            Path.Combine(validationProofDirectory, "request.json"),
            Path.Combine(validationProofDirectory, "response.json"),
            Path.Combine(validationProofDirectory, "protocol.json"),
            Path.Combine(validationProofDirectory, "transport.json"),
            Path.Combine(validationProofDirectory, "headers.json"),
            Path.Combine(validationProofDirectory, "body-summary.json"),
            Path.Combine(validationProofDirectory, "notes.md"),
            Path.Combine(cellDirectory, "protocol-proof.json"),
            Path.Combine(cellDirectory, "protocol-proof.stdout.txt"),
            Path.Combine(cellDirectory, "protocol-proof.stderr.txt"),
            Path.Combine(cellDirectory, "load-tool.stdout.txt"),
            Path.Combine(cellDirectory, "load-tool.stderr.txt"),
            Path.Combine(cellDirectory, "h2load.stdout.txt"),
            Path.Combine(cellDirectory, "h2load.stderr.txt"),
            Path.Combine(cellDirectory, "h2load-output.json"),
            Path.Combine(cellDirectory, "h2load-command.txt"),
            Path.Combine(cellDirectory, "docker-command.txt"),
            Path.Combine(cellDirectory, "load-tool-docker-inspect.json"),
            Path.Combine(cellDirectory, "load-tool-docker-stats.raw.txt"),
            Path.Combine(cellDirectory, "load-tool-docker-stats.jsonl"),
            Path.Combine(cellDirectory, "load-tool-docker-metrics-summary.json"),
            Path.Combine(cellDirectory, "load-tool.version.txt"),
            Path.Combine(cellDirectory, "load-tool-execution.json"),
            Path.Combine(cellDirectory, "server.stdout.txt"),
            Path.Combine(cellDirectory, "server.stderr.txt"),
            Path.Combine(cellDirectory, "target.stdout.txt"),
            Path.Combine(cellDirectory, "target.stderr.txt"),
            Path.Combine(cellDirectory, "docker-inspect.json"),
            Path.Combine(cellDirectory, "target-docker-inspect.json"),
            Path.Combine(cellDirectory, "target-docker-command.txt"),
            Path.Combine(cellDirectory, "target-docker-network-inspect.json"),
            Path.Combine(cellDirectory, "target-docker-stats.raw.txt"),
            Path.Combine(cellDirectory, "target-docker-stats.jsonl"),
            Path.Combine(cellDirectory, "target-docker-metrics-summary.json"),
            Path.Combine(cellDirectory, "docker-network-command.txt"),
            Path.Combine(cellDirectory, "docker-network-inspect.json"),
            Path.Combine(cellDirectory, "docker-network-cleanup.txt"),
            Path.Combine(cellDirectory, "docker-resource-limits.json"),
            Path.Combine(cellDirectory, "docker-cleanup.json"),
            Path.Combine(cellDirectory, "manifest.json"),
            Path.Combine(cellDirectory, "scenario.json"),
            Path.Combine(cellDirectory, "target-execution.json"),
            Path.Combine(cellDirectory, "diagnostic-target.json"),
            Path.Combine(cellDirectory, "adapter-health.json"),
            Path.Combine(cellDirectory, "adapter-manifest.json"),
            Path.Combine(cellDirectory, "adapter-session-create.json"),
            Path.Combine(cellDirectory, "adapter-prepare.json"),
            Path.Combine(cellDirectory, "adapter-start.json"),
            Path.Combine(cellDirectory, "adapter-status.jsonl"),
            Path.Combine(cellDirectory, "adapter-endpoints.json"),
            Path.Combine(cellDirectory, "adapter-metrics.json"),
            Path.Combine(cellDirectory, "adapter-artifacts.json"),
            Path.Combine(cellDirectory, "adapter-stop.json"),
            Path.Combine(cellDirectory, "adapter-delete.json"),
            Path.Combine(cellDirectory, "counters.stdout.txt"),
            Path.Combine(cellDirectory, "counters.stderr.txt"),
            Path.Combine(cellDirectory, "counters.raw.json"),
            Path.Combine(cellDirectory, "counters.raw.csv"),
            Path.Combine(cellDirectory, "counters-summary.json"),
            Path.Combine(cellDirectory, "qlog"),
            Path.Combine(cellDirectory, "sslkeylog"),
            Path.Combine(cellDirectory, "pcap"),
            Path.Combine(cellDirectory, "notes.txt"));
    }

    public static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalid.Contains(character) ? '_' : character));
    }
}
