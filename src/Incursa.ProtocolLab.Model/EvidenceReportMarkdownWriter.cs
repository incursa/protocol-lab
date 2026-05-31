// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Globalization;
using System.Text;

namespace Incursa.ProtocolLab.Model;

public static class EvidenceReportMarkdownWriter
{
    public static string Write(EvidenceReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# ProtocolLab Evidence Report: {report.RunId}");
        builder.AppendLine();

        WriteIdentity(builder, report.Identity);
        WriteMatrixSummary(builder, report.MatrixSummary);
        WriteValidationSummary(builder, report.ValidationSummary);
        WriteBenchmarkAcceptance(builder, report.BenchmarkAcceptance);
        WriteComparisonTables(builder, report.ComparisonGroups);
        WriteEvidenceWarnings(builder, report.Warnings);
        WriteArtifactIndex(builder, report.ArtifactIndex);

        return builder.ToString();
    }

    private static void WriteIdentity(StringBuilder builder, EvidenceReportIdentity identity)
    {
        builder.AppendLine("## 1. Run Identity");
        builder.AppendLine();
        builder.AppendLine($"- **Run ID**: {Escape(identity.RunId)}");
        builder.AppendLine($"- **Timestamp**: {identity.Timestamp:O}");
        if (!string.IsNullOrWhiteSpace(identity.SuiteId))
        {
            var suiteLabel = !string.IsNullOrWhiteSpace(identity.SuiteTitle)
                ? $"{identity.SuiteId} ({identity.SuiteTitle})"
                : identity.SuiteId;
            builder.AppendLine($"- **Suite**: {Escape(suiteLabel)}");
        }

        if (!string.IsNullOrWhiteSpace(identity.GitCommit))
        {
            builder.AppendLine($"- **Git Commit**: {Escape(identity.GitCommit)}");
        }

        builder.AppendLine($"- **Host**: {Escape(identity.HostName ?? "unknown")}");
        builder.AppendLine($"- **OS**: {Escape(identity.OperatingSystem ?? "unknown")} ({Escape(identity.OperatingSystemArchitecture ?? "unknown")})");
        builder.AppendLine($"- **Runtime**: {Escape(identity.FrameworkDescription ?? "unknown")} ({Escape(identity.ProcessArchitecture ?? "unknown")})");
        builder.AppendLine($"- **Processor Count**: {identity.ProcessorCount}");
        if (identity.TotalAvailableMemoryBytes.HasValue)
        {
            builder.AppendLine($"- **Total Memory**: {FormatBytes(identity.TotalAvailableMemoryBytes.Value)}");
        }

        builder.AppendLine($"- **Execution Mode**: {Escape(identity.ExecutionMode ?? "process")}");
        builder.AppendLine($"- **Evidence Tier**: {Escape(identity.EvidenceTier ?? "local-smoke")}");

        if (identity.Warnings.Count > 0)
        {
            builder.AppendLine($"- **Warning**: {Escape(string.Join("; ", identity.Warnings))}");
        }

        builder.AppendLine();
    }

    private static void WriteMatrixSummary(StringBuilder builder, EvidenceReportMatrixSummary matrix)
    {
        builder.AppendLine("## 2. Matrix Summary");
        builder.AppendLine();
        builder.AppendLine("| Category | Count |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Total cells | {matrix.TotalCells} |");
        builder.AppendLine($"| Supported cells | {matrix.SupportedCells} |");
        builder.AppendLine($"| **Unsupported cells** | **{matrix.UnsupportedCells}** |");
        builder.AppendLine($"| - Missing capability | {matrix.MissingCapability} |");
        builder.AppendLine($"| - Missing load tool | {matrix.MissingLoadTool} |");
        builder.AppendLine($"| - Incompatible traffic shape | {matrix.IncompatibleTrafficShape} |");
        builder.AppendLine($"| - Incompatible load profile | {matrix.IncompatibleLoadProfile} |");
        builder.AppendLine($"| - Experimental disabled | {matrix.ExperimentalDisabled} |");
        builder.AppendLine($"| - Placeholder not runnable | {matrix.PlaceholderNotRunnable} |");
        builder.AppendLine($"| - Other unsupported | {matrix.OtherUnsupported} |");
        builder.AppendLine();

        if (matrix.UnsupportedCellDetails.Count > 0)
        {
            builder.AppendLine("> Unsupported cells are not failures. They reflect known tooling/protocol/apparatus limitations.");
            builder.AppendLine();
            builder.AppendLine("### Unsupported Cell Details");
            builder.AppendLine();
            foreach (var detail in matrix.UnsupportedCellDetails)
            {
                builder.AppendLine($"- {Escape(detail)}");
            }

            builder.AppendLine();
        }
    }

    private static void WriteValidationSummary(StringBuilder builder, EvidenceReportValidationSummary validation)
    {
        builder.AppendLine("## 3. Validation Summary");
        builder.AppendLine();
        builder.AppendLine("| Status | Count |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Passed | {validation.Passed} |");
        builder.AppendLine($"| Failed | {validation.Failed} |");
        builder.AppendLine($"| Unsupported | {validation.Unsupported} |");
        builder.AppendLine($"| Not Applicable | {validation.NotApplicable} |");
        builder.AppendLine($"| Inconclusive | {validation.Inconclusive} |");
        builder.AppendLine($"| Infrastructure Failure | {validation.InfrastructureFailure} |");
        builder.AppendLine();

        if (validation.ProofArtifacts.Count > 0)
        {
            builder.AppendLine("### Validation Proof Artifacts");
            builder.AppendLine();
            builder.AppendLine("| Cell | Status | Proof Directory | Artifacts |");
            builder.AppendLine("| --- | --- | --- | --- |");
            foreach (var proof in validation.ProofArtifacts.Take(20))
            {
                builder.Append("| ");
                builder.Append(Escape(proof.CellKey));
                builder.Append(" | ");
                builder.Append(Escape(proof.Status));
                builder.Append(" | ");
                builder.Append(Escape(proof.ProofDirectory ?? "n/a"));
                builder.Append(" | ");
                builder.Append(Escape(proof.Artifacts.Count > 0
                    ? string.Join(", ", proof.Artifacts.Take(3))
                    : "none"));
                builder.AppendLine(" |");
            }

            if (validation.ProofArtifacts.Count > 20)
            {
                builder.AppendLine($"| ... | ... {validation.ProofArtifacts.Count - 20} more | ... | ... |");
            }

            builder.AppendLine();
        }
    }

    private static void WriteBenchmarkAcceptance(StringBuilder builder, EvidenceReportBenchmarkAcceptance acceptance)
    {
        builder.AppendLine("## 4. Benchmark Acceptance Summary");
        builder.AppendLine();
        builder.AppendLine("| Category | Count |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| **Accepted benchmark results** | **{acceptance.AcceptedBenchmarks}** |");
        builder.AppendLine($"| Rejected benchmark results | {acceptance.RejectedBenchmarks} |");
        builder.AppendLine($"| Not run (validation failed) | {acceptance.NotRunValidationFailed} |");
        builder.AppendLine($"| Not run (unsupported) | {acceptance.NotRunUnsupported} |");
        builder.AppendLine($"| Not run (load tool failed) | {acceptance.NotRunLoadToolFailed} |");
        builder.AppendLine($"| Not run (parser failed) | {acceptance.NotRunParserFailed} |");
        builder.AppendLine();

        if (acceptance.AcceptedDetails.Count > 0)
        {
            builder.AppendLine("### Accepted Benchmarks");
            builder.AppendLine();
            builder.AppendLine("| Implementation | Scenario | Protocol | Connections | Streams | Rep | Evidence Tier |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
            foreach (var item in acceptance.AcceptedDetails.Take(50))
            {
                builder.Append("| ");
                builder.Append(Escape(item.ImplementationId));
                builder.Append(" | ");
                builder.Append(Escape(item.ScenarioId));
                builder.Append(" | ");
                builder.Append(Escape(item.Protocol));
                builder.Append(" | ");
                builder.Append(item.Connections);
                builder.Append(" | ");
                builder.Append(item.StreamsPerConnection);
                builder.Append(" | ");
                builder.Append(item.Repetition);
                builder.Append(" | ");
                builder.Append(Escape(item.EvidenceTier ?? "n/a"));
                builder.AppendLine(" |");
            }

            if (acceptance.AcceptedDetails.Count > 50)
            {
                builder.AppendLine($"| ... | ... {acceptance.AcceptedDetails.Count - 50} more | ... | ... | ... | ... | ... |");
            }

            builder.AppendLine();
        }

        if (acceptance.RejectedDetails.Count > 0)
        {
            builder.AppendLine("### Rejected Benchmarks");
            builder.AppendLine();
            builder.AppendLine("| Implementation | Scenario | Protocol | Reason |");
            builder.AppendLine("| --- | --- | --- | --- |");
            foreach (var item in acceptance.RejectedDetails.Take(50))
            {
                builder.Append("| ");
                builder.Append(Escape(item.ImplementationId));
                builder.Append(" | ");
                builder.Append(Escape(item.ScenarioId));
                builder.Append(" | ");
                builder.Append(Escape(item.Protocol));
                builder.Append(" | ");
                builder.Append(Escape(item.Reason ?? "n/a"));
                builder.AppendLine(" |");
            }

            if (acceptance.RejectedDetails.Count > 50)
            {
                builder.AppendLine($"| ... | ... | ... | ... {acceptance.RejectedDetails.Count - 50} more |");
            }

            builder.AppendLine();
        }

        if (acceptance.NotRunDetails.Count > 0)
        {
            builder.AppendLine("### Not-Run Benchmarks");
            builder.AppendLine();
            builder.AppendLine("| Implementation | Scenario | Protocol | Reason |");
            builder.AppendLine("| --- | --- | --- | --- |");
            foreach (var item in acceptance.NotRunDetails.Take(50))
            {
                builder.Append("| ");
                builder.Append(Escape(item.ImplementationId));
                builder.Append(" | ");
                builder.Append(Escape(item.ScenarioId));
                builder.Append(" | ");
                builder.Append(Escape(item.Protocol));
                builder.Append(" | ");
                builder.Append(Escape(item.Reason ?? "n/a"));
                builder.AppendLine(" |");
            }

            if (acceptance.NotRunDetails.Count > 50)
            {
                builder.AppendLine($"| ... | ... | ... | ... {acceptance.NotRunDetails.Count - 50} more |");
            }

            builder.AppendLine();
        }
    }

    private static void WriteComparisonTables(StringBuilder builder, IReadOnlyList<EvidenceReportComparisonGroup> groups)
    {
        builder.AppendLine("## 5. Comparison Tables");
        builder.AppendLine();

        if (groups.Count == 0)
        {
            builder.AppendLine("_No comparable groups found. Groups require at least 2 implementations with accepted benchmarks in the same scenario/protocol/load-tool/backend tuple._");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("> Results are grouped by scenario, protocol, load profile, load tool, execution backend, and evidence tier. Comparisons across different load tools, profiles, or execution modes are not made.");
        builder.AppendLine();

        foreach (var group in groups)
        {
            var groupLabel = $"{group.Family}/{group.ScenarioId} | {group.Protocol} | {group.LoadProfileTitle} | {group.LoadTool} | {group.ExecutionBackend}";
            builder.AppendLine($"### {Escape(groupLabel)}");
            builder.AppendLine();
            builder.AppendLine($"- **Evidence Tier**: {Escape(group.EvidenceTier)}");
            builder.AppendLine($"- **Load Shape**: c{group.Connections}-s{group.StreamsPerConnection} @ {group.DurationSeconds}s");
            builder.AppendLine($"- **Network**: {Escape(group.NetworkProfile)}");
            builder.AppendLine();

            if (group.ComparabilityWarnings.Count > 0)
            {
                builder.AppendLine("> **Comparability Warnings:**");
                foreach (var warning in group.ComparabilityWarnings)
                {
                    builder.AppendLine($"> - {Escape(warning)}");
                }

                builder.AppendLine();
            }

            builder.AppendLine("| Implementation | Mode | Protocol | Reps | Validation | Benchmark | Evidence | RPS (med) | p50 ms | p95 ms | p99 ms | mean ms | Throughput B/s |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var entry in group.Entries)
            {
                builder.Append("| ");
                builder.Append(FormatLabel(entry.ImplementationId, entry.ImplementationName));
                builder.Append(" | ");
                builder.Append(Escape(entry.LoadToolMode ?? "n/a"));
                builder.Append(" | ");
                builder.Append(Escape(entry.ProvenProtocol ?? "n/a"));
                builder.Append(" | ");
                builder.Append(entry.Repetitions);
                builder.Append(" | ");
                builder.Append(Escape(entry.ValidationStatus));
                builder.Append(" | ");
                builder.Append(Escape(entry.BenchmarkStatus));
                builder.Append(" | ");
                builder.Append(Escape(entry.EvidenceClass));
                builder.Append(" | ");
                builder.Append(FormatNullableDouble(entry.RequestsPerSecondMedian));
                builder.Append(" | ");
                builder.Append(FormatNullableDouble(entry.LatencyP50MsMedian));
                builder.Append(" | ");
                builder.Append(FormatNullableDouble(entry.LatencyP95MsMedian));
                builder.Append(" | ");
                builder.Append(FormatNullableDouble(entry.LatencyP99MsMedian));
                builder.Append(" | ");
                builder.Append(FormatNullableDouble(entry.LatencyMeanMsMedian));
                builder.Append(" | ");
                builder.Append(FormatNullableDouble(entry.ThroughputBytesPerSecondMedian));
                builder.AppendLine(" |");
            }

            builder.AppendLine();
        }
    }

    private static void WriteEvidenceWarnings(StringBuilder builder, IReadOnlyList<EvidenceReportWarning> warnings)
    {
        builder.AppendLine("## 6. Evidence Warnings");
        builder.AppendLine();

        if (warnings.Count == 0)
        {
            builder.AppendLine("_No evidence warnings._");
            builder.AppendLine();
            return;
        }

        var byCategory = warnings
            .GroupBy(w => w.Category)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var category in byCategory)
        {
            builder.AppendLine($"### {Escape(Capitalize(category.Key))} Warnings");
            builder.AppendLine();
            foreach (var warning in category.DistinctBy(w => w.Message))
            {
                builder.AppendLine($"- {Escape(warning.Message)}");
            }

            builder.AppendLine();
        }
    }

    private static void WriteArtifactIndex(StringBuilder builder, IReadOnlyList<EvidenceReportArtifactEntry> entries)
    {
        builder.AppendLine("## 7. Artifact Index");
        builder.AppendLine();

        if (entries.Count == 0)
        {
            builder.AppendLine("_No artifact entries._");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Cell | Directory | Key Artifacts |");
        builder.AppendLine("| --- | --- | --- |");
        foreach (var entry in entries.Take(50))
        {
            var presentFiles = entry.Files
                .Where(f => !string.IsNullOrWhiteSpace(f.Path))
                .Select(f => f.Name)
                .Take(5)
                .ToArray();

            builder.Append("| ");
            builder.Append(Escape(entry.CellKey));
            builder.Append(" | ");
            builder.Append(Escape(entry.CellDirectory));
            builder.Append(" | ");
            builder.Append(Escape(presentFiles.Length > 0
                ? string.Join(", ", presentFiles)
                : "none"));
            builder.AppendLine(" |");
        }

        if (entries.Count > 50)
        {
            builder.AppendLine($"| ... | ... | ... {entries.Count - 50} more |");
        }

        builder.AppendLine();
    }

    private static string FormatLabel(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || string.Equals(id, name, StringComparison.Ordinal))
        {
            return Escape(id);
        }

        return $"{Escape(id)} ({Escape(name)})";
    }

    private static string FormatNullableDouble(double? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.###", CultureInfo.InvariantCulture)
            : "n/a";
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824d:F1} GB",
            >= 1_048_576 => $"{bytes / 1_048_576d:F1} MB",
            >= 1_024 => $"{bytes / 1_024d:F1} KB",
            _ => $"{bytes} B"
        };
    }

    private static string Capitalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return char.ToUpper(value[0], CultureInfo.InvariantCulture) + value[1..];
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty).Replace("|", "\\|", StringComparison.Ordinal);
    }
}
