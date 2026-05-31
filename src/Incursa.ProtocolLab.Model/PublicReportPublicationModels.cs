// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public sealed record PublicReportPublicationWarning
{
    public string Code { get; init; } = "";
    public string Severity { get; init; } = "warning";
    public string Message { get; init; } = "";
    public string? Context { get; init; }
}

public sealed record PublicReportPublicationSkippedArtifact
{
    public string CellKey { get; init; } = "";
    public string Name { get; init; } = "";
    public string Reason { get; init; } = "";
}

public sealed record PublicReportArtifactReference
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public bool Exists { get; init; }
    public bool Copied { get; init; }
    public string? SkipReason { get; init; }
}

public sealed record PublicReportArtifactsCell
{
    public string CellKey { get; init; } = "";
    public string CellDirectory { get; init; } = "";
    public IReadOnlyList<PublicReportArtifactReference> Files { get; init; } = [];
}

public sealed record PublicReportArtifactsIndex
{
    public string SchemaVersion { get; init; } = "protocol-lab.public-report-artifacts-index.v1";
    public string RunId { get; init; } = "";
    public DateTimeOffset GeneratedAt { get; init; }
    public string ArtifactRootKey { get; init; } = "artifacts";
    public int CopiedArtifactCount { get; init; }
    public int SkippedArtifactCount { get; init; }
    public IReadOnlyList<PublicReportArtifactsCell> Cells { get; init; } = [];
}

public sealed record PublicReportBenchmarkCounts
{
    public int Accepted { get; init; }
    public int Rejected { get; init; }
    public int NotRunValidationFailed { get; init; }
    public int NotRunUnsupported { get; init; }
    public int NotRunLoadToolFailed { get; init; }
    public int NotRunParserFailed { get; init; }
}

public sealed record PublicReportEvidenceClassSummary
{
    public IReadOnlyDictionary<string, int> Counts { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}

public sealed record PublicReportPublicationManifest
{
    public string SchemaVersion { get; init; } = "protocol-lab.public-report-manifest.v1";
    public string RunId { get; init; } = "";
    public DateTimeOffset GeneratedAt { get; init; }
    public string Visibility { get; init; } = "public";
    public string SourceKind { get; init; } = "local-sample";
    public string ClaimLevel { get; init; } = "";
    public bool Publishable { get; init; }
    public bool DiagnosticOnly { get; init; }
    public bool AllowDiagnosticPublication { get; init; }
    public string ExecutionProfile { get; init; } = "";
    public IReadOnlyList<string> Implementations { get; init; } = [];
    public IReadOnlyList<string> Scenarios { get; init; } = [];
    public IReadOnlyList<string> Protocols { get; init; } = [];
    public ValidationCounts ValidationCounts { get; init; } = new(0, 0, 0, 0, 0, 0);
    public PublicReportBenchmarkCounts BenchmarkCounts { get; init; } = new();
    public PublicReportEvidenceClassSummary EvidenceClasses { get; init; } = new();
    public int WarningCount { get; init; }
    public int CopiedArtifactCount { get; init; }
    public int SkippedArtifactCount { get; init; }
    public string ArtifactRootKey { get; init; } = "artifacts";
}

public sealed record PublicReportRegistryEntry
{
    public string SchemaVersion { get; init; } = "protocol-lab.public-report-index-entry.v1";
    public string RunId { get; init; } = "";
    public DateTimeOffset GeneratedAt { get; init; }
    public string BundlePrefix { get; init; } = "";
    public string EvidenceReportJsonKey { get; init; } = "";
    public string EvidenceReportMarkdownKey { get; init; } = "";
    public string ArtifactsIndexKey { get; init; } = "";
    public string PublicationManifestKey { get; init; } = "";
    public string PublicationWarningsKey { get; init; } = "";
    public string PublicationSkippedKey { get; init; } = "";
    public string ReportIndexEntryKey { get; init; } = "";
    public string ReportIndexKey { get; init; } = "";
    public string Visibility { get; init; } = "public";
    public string SourceKind { get; init; } = "local-sample";
    public string ClaimLevel { get; init; } = "";
    public bool Publishable { get; init; }
    public bool DiagnosticOnly { get; init; }
    public string ExecutionProfile { get; init; } = "";
    public IReadOnlyList<string> Implementations { get; init; } = [];
    public IReadOnlyList<string> Scenarios { get; init; } = [];
    public IReadOnlyList<string> Protocols { get; init; } = [];
    public ValidationCounts ValidationCounts { get; init; } = new(0, 0, 0, 0, 0, 0);
    public PublicReportBenchmarkCounts BenchmarkCounts { get; init; } = new();
    public PublicReportEvidenceClassSummary EvidenceClasses { get; init; } = new();
    public int WarningCount { get; init; }
    public string ArtifactRootKey { get; init; } = "artifacts";
}

public sealed record PublicReportRegistry
{
    public string SchemaVersion { get; init; } = "protocol-lab.public-report-index.v1";
    public IReadOnlyList<PublicReportRegistryEntry> Entries { get; init; } = [];
}
