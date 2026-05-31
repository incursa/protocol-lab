// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text;
using System.Text.RegularExpressions;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

internal static class ReportPublicationWorkflow
{
    private const string DefaultVisibility = "public";
    private const int MaxCopiedArtifactBytes = 1_048_576;

    private static readonly HashSet<string> AllowedArtifactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "manifest.json",
        "scenario.json",
        "docker-inspect.json",
        "target-docker-stats.jsonl",
        "docker-resource-limits.json",
        "docker-cleanup.json",
        "counters-summary.json",
        "load-tool.version.txt",
        "h2load-output.json",
        "load-tool-docker-metrics-summary.json",
        "target-docker-metrics-summary.json",
        "adapter-manifest.json",
        "adapter-artifacts.json"
    };

    private static readonly string[] ForbiddenPathMarkers =
    [
        @"C:\shared",
        @"C:\src",
        "protocol-lab-internal"
    ];

    private static readonly Regex[] SecretPatterns =
    [
        new Regex(@"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----", RegexOptions.Compiled | RegexOptions.CultureInvariant),
        new Regex(@"\b(api[_-]?key|client[_-]?secret|access[_-]?token|refresh[_-]?token|password|passwd|pwd)\s*[:=]\s*\S+", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
        new Regex(@"\bAKIA[0-9A-Z]{16}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant),
        new Regex(@"\bgh[pousr]_[A-Za-z0-9_]{20,}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant)
    ];

    public static async Task<RunnerCommandResult> PublishAsync(string root, RunnerCommandOptions options, IRunnerEventSink? eventSink = null)
    {
        var output = new RunnerOutputBuffer(RunnerCommandKind.PublishReport, eventSink);
        var repositoryRoot = Path.GetFullPath(root);
        var visibility = (options.Get("visibility") ?? DefaultVisibility).Trim();
        var dryRun = IsTruthy(options.Get("dry-run"));
        var allowDiagnosticPublication = IsTruthy(options.Get("allow-diagnostic-publication"));

        if (!string.Equals(visibility, DefaultVisibility, StringComparison.OrdinalIgnoreCase))
        {
            output.WriteError($"Unsupported visibility '{visibility}'. Only 'public' is supported.");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        var sourceRunRoot = ResolveRunRoot(repositoryRoot, options);
        if (sourceRunRoot is null)
        {
            output.WriteError("publish-report requires --run <path> or --run-id <id>.");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (!Directory.Exists(sourceRunRoot))
        {
            output.WriteError($"Run directory not found: {sourceRunRoot}");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (!TryLoadReport(Path.Combine(sourceRunRoot, "aggregate-results.json"), "aggregate report", out RunReport? aggregateReport, out var aggregateError))
        {
            output.WriteError(aggregateError ?? "Unable to load aggregate report.");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        var evidenceReportPath = FirstExistingPath(
            Path.Combine(sourceRunRoot, "evidence-report-v1.json"),
            Path.Combine(sourceRunRoot, "evidence-report.json"));
        if (evidenceReportPath is null)
        {
            output.WriteError($"Evidence report not found in {sourceRunRoot}. Expected evidence-report-v1.json or evidence-report.json.");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (!TryLoadReport(evidenceReportPath, "evidence report", out EvidenceReport? evidenceReport, out var evidenceError))
        {
            output.WriteError(evidenceError ?? "Unable to load evidence report.");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        RunDescriptor? runDescriptor = null;
        var runDescriptorPath = Path.Combine(sourceRunRoot, "run.json");
        if (File.Exists(runDescriptorPath) && !TryLoadReport(runDescriptorPath, "run descriptor", out runDescriptor, out var runError))
        {
            output.WriteError(runError ?? "Unable to load run descriptor.");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (!string.Equals(aggregateReport!.RunId, evidenceReport!.RunId, StringComparison.OrdinalIgnoreCase))
        {
            output.WriteError($"Report mismatch: aggregate-results.json run id '{aggregateReport.RunId}' does not match evidence report run id '{evidenceReport.RunId}'.");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (runDescriptor is not null && !string.Equals(runDescriptor.RunId, aggregateReport.RunId, StringComparison.OrdinalIgnoreCase))
        {
            output.WriteError($"Report mismatch: run.json run id '{runDescriptor.RunId}' does not match aggregate-results.json run id '{aggregateReport.RunId}'.");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        var sourceRunId = Path.GetFileName(sourceRunRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(sourceRunId))
        {
            output.WriteError($"Unable to determine the run id from '{sourceRunRoot}'.");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (!string.Equals(sourceRunId, aggregateReport.RunId, StringComparison.OrdinalIgnoreCase))
        {
            output.WriteError($"Report mismatch: run directory '{sourceRunId}' does not match aggregate-results.json run id '{aggregateReport.RunId}'.");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (!string.Equals(sourceRunId, evidenceReport.RunId, StringComparison.OrdinalIgnoreCase))
        {
            output.WriteError($"Report mismatch: run directory '{sourceRunId}' does not match evidence report run id '{evidenceReport.RunId}'.");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (runDescriptor is not null && !string.Equals(runDescriptor.RunId, sourceRunId, StringComparison.OrdinalIgnoreCase))
        {
            output.WriteError($"Report mismatch: run.json run id '{runDescriptor.RunId}' does not match run directory '{sourceRunId}'.");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        var implementationTreeRoot = Path.Combine(sourceRunRoot, "implementations");
        if (!Directory.Exists(implementationTreeRoot))
        {
            output.WriteError($"Run directory is missing the per-cell artifact tree: {implementationTreeRoot}");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        var executionProfile = aggregateReport.Metadata?.ExecutionProfile
            ?? runDescriptor?.Metadata?.ExecutionProfile
            ?? ExecutionProfile.LocalProcess;
        var claimLevel = aggregateReport.ClaimLevel;
        var loadProfiles = LoadProfileCatalog.Load(Path.Combine(repositoryRoot, "load-profiles"));
        var loadProfilePublishable = DeterminePublishable(loadProfiles, aggregateReport.Aggregates, out var loadProfileWarnings);
        var hasLoadProfileIdentifiers = aggregateReport.Aggregates.Any(static aggregate => !string.IsNullOrWhiteSpace(aggregate.LoadProfileId));
        foreach (var warning in loadProfileWarnings)
        {
            output.WriteWarning(warning);
        }

        if (claimLevel == ReportClaimLevel.DiagnosticOnly && !allowDiagnosticPublication)
        {
            output.WriteError("DiagnosticOnly reports require --allow-diagnostic-publication before they can be prepared for public publication.");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (claimLevel == ReportClaimLevel.DiagnosticOnly && allowDiagnosticPublication)
        {
            output.WriteWarning("DiagnosticOnly publication explicitly allowed; the bundle will be labeled diagnostic-only.");
        }

        if (!IsClaimAllowedForExecutionProfile(claimLevel, executionProfile))
        {
            output.WriteError($"Claim level {claimLevel} is too high for execution profile {ExecutionProfiles.ToId(executionProfile)}.");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (!loadProfilePublishable && (claimLevel is ReportClaimLevel.Benchmark or ReportClaimLevel.Verified))
        {
            output.WriteError($"Claim level {claimLevel} requires a publishable load profile, but the run used a non-publishable profile.");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (!loadProfilePublishable && hasLoadProfileIdentifiers)
        {
            output.WriteWarning("The selected load profile(s) are marked publishable=false; the bundle will be labeled as non-publishable.");
        }

        var outputRoot = Path.GetFullPath(ResolveOutputRoot(repositoryRoot, sourceRunRoot, options));
        var sourceRunRootFull = Path.GetFullPath(sourceRunRoot);
        var normalizedOutputRoot = outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedSourceRunRoot = sourceRunRootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(normalizedOutputRoot, normalizedSourceRunRoot, StringComparison.OrdinalIgnoreCase) || IsUnderRoot(sourceRunRootFull, outputRoot))
        {
            output.WriteError($"Publication output root must be separate from the run root: {outputRoot}");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (!dryRun)
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }

            Directory.CreateDirectory(outputRoot);
        }

        var plan = await PrepareArtifactsAsync(repositoryRoot, sourceRunRoot, outputRoot, evidenceReport.ArtifactIndex, dryRun);
        foreach (var warning in plan.Warnings)
        {
            output.WriteWarning(warning.Message);
        }

        if (!plan.CanPublish)
        {
            output.WriteError(plan.ErrorMessage ?? "Report bundle preparation failed.");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        var publicArtifactIndex = BuildPublicArtifactIndex(evidenceReport, aggregateReport, plan);
        var publicEvidenceReport = SanitizeEvidenceReport(evidenceReport, publicArtifactIndex.Cells);
        var publicationManifest = BuildPublicationManifest(aggregateReport, evidenceReport, publicArtifactIndex, claimLevel, executionProfile, loadProfilePublishable, allowDiagnosticPublication);
        var registryEntry = BuildRegistryEntry(aggregateReport, evidenceReport, publicArtifactIndex, claimLevel, executionProfile, loadProfilePublishable);
        var registry = new PublicReportRegistry
        {
            Entries = [registryEntry]
        };

        var evidenceReportJson = ResultJson.Serialize(publicEvidenceReport);
        var evidenceReportMarkdown = EvidenceReportMarkdownWriter.Write(publicEvidenceReport);
        var artifactsIndexJson = ResultJson.Serialize(publicArtifactIndex);
        var manifestJson = ResultJson.Serialize(publicationManifest);
        var warningsMarkdown = BuildWarningsMarkdown(aggregateReport, evidenceReport, publicationManifest, plan);
        var skippedMarkdown = BuildSkippedMarkdown(plan);
        var registryEntryJson = ResultJson.Serialize(registryEntry);
        var registryJson = ResultJson.Serialize(registry);

        if (ContainsForbiddenContent(evidenceReportJson, out var evidenceLeak))
        {
            output.WriteError($"Public evidence report still contains a forbidden path or secret marker: {evidenceLeak}");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (ContainsForbiddenContent(evidenceReportMarkdown, out evidenceLeak))
        {
            output.WriteError($"Public evidence markdown still contains a forbidden path or secret marker: {evidenceLeak}");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (ContainsForbiddenContent(artifactsIndexJson, out evidenceLeak))
        {
            output.WriteError($"Public artifact index still contains a forbidden path or secret marker: {evidenceLeak}");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (ContainsForbiddenContent(manifestJson, out evidenceLeak))
        {
            output.WriteError($"Publication manifest still contains a forbidden path or secret marker: {evidenceLeak}");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (ContainsForbiddenContent(registryEntryJson, out evidenceLeak))
        {
            output.WriteError($"Registry entry still contains a forbidden path or secret marker: {evidenceLeak}");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (ContainsForbiddenContent(registryJson, out evidenceLeak))
        {
            output.WriteError($"Registry index still contains a forbidden path or secret marker: {evidenceLeak}");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (ContainsForbiddenContent(warningsMarkdown, out evidenceLeak))
        {
            output.WriteError($"Publication warnings still contain a forbidden path or secret marker: {evidenceLeak}");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        if (ContainsForbiddenContent(skippedMarkdown, out evidenceLeak))
        {
            output.WriteError($"Publication skipped list still contains a forbidden path or secret marker: {evidenceLeak}");
            return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 1, output.Messages);
        }

        var artifacts = new List<RunnerArtifactReference>();

        if (dryRun)
        {
            output.WriteLine($"Dry run: report bundle for {aggregateReport.RunId} is valid and would be written to {outputRoot}.");
            output.WriteLine($"  claim level: {claimLevel}");
            output.WriteLine($"  execution profile: {ExecutionProfiles.ToId(executionProfile)}");
            output.WriteLine($"  copied artifacts: {plan.CopiedCount}");
            output.WriteLine($"  skipped artifacts: {plan.SkippedCount}");
        }
        else
        {
            await WriteTextAsync(Path.Combine(outputRoot, "evidence-report-v1.json"), evidenceReportJson);
            await WriteTextAsync(Path.Combine(outputRoot, "evidence-report-v1.md"), evidenceReportMarkdown);
            await WriteTextAsync(Path.Combine(outputRoot, "artifacts-index.json"), artifactsIndexJson);
            await WriteTextAsync(Path.Combine(outputRoot, "publication-manifest.json"), manifestJson);
            await WriteTextAsync(Path.Combine(outputRoot, "publication-warnings.md"), warningsMarkdown);
            await WriteTextAsync(Path.Combine(outputRoot, "publication-skipped.md"), skippedMarkdown);
            await WriteTextAsync(Path.Combine(outputRoot, "report-index-entry.json"), registryEntryJson);
            await WriteTextAsync(Path.Combine(outputRoot, "report-index.json"), registryJson);

            artifacts.Add(new RunnerArtifactReference("evidence-report-v1-json", Path.Combine(outputRoot, "evidence-report-v1.json")));
            artifacts.Add(new RunnerArtifactReference("evidence-report-v1-md", Path.Combine(outputRoot, "evidence-report-v1.md")));
            artifacts.Add(new RunnerArtifactReference("artifacts-index", Path.Combine(outputRoot, "artifacts-index.json")));
            artifacts.Add(new RunnerArtifactReference("publication-manifest", Path.Combine(outputRoot, "publication-manifest.json")));
            artifacts.Add(new RunnerArtifactReference("publication-warnings", Path.Combine(outputRoot, "publication-warnings.md")));
            artifacts.Add(new RunnerArtifactReference("publication-skipped", Path.Combine(outputRoot, "publication-skipped.md")));
            artifacts.Add(new RunnerArtifactReference("report-index-entry", Path.Combine(outputRoot, "report-index-entry.json")));
            artifacts.Add(new RunnerArtifactReference("report-index", Path.Combine(outputRoot, "report-index.json")));

            output.WriteLine($"Report bundle written to {outputRoot}");
        }

        output.WriteLine($"Run ID: {aggregateReport.RunId}");
        output.WriteLine($"Claim level: {claimLevel}");
        output.WriteLine($"Execution profile: {ExecutionProfiles.ToId(executionProfile)}");
        output.WriteLine($"Publishable load profiles: {(loadProfilePublishable ? "yes" : "no")}");
        output.WriteLine($"Copied artifacts: {plan.CopiedCount}");
        output.WriteLine($"Skipped artifacts: {plan.SkippedCount}");
        output.WriteLine($"Warnings: {publicationManifest.WarningCount}");

        return RunnerCommandResult.Create(RunnerCommandKind.PublishReport, 0, output.Messages, artifacts);
    }

    private static async Task<PreparedArtifactPlan> PrepareArtifactsAsync(
        string repositoryRoot,
        string sourceRunRoot,
        string outputRoot,
        IReadOnlyList<EvidenceReportArtifactEntry> artifactIndex,
        bool dryRun)
    {
        var cells = new List<PreparedArtifactCell>();
        var warnings = new List<PublicReportPublicationWarning>();
        var skipped = new List<PublicReportPublicationSkippedArtifact>();
        var copiedCount = 0;
        var skippedCount = 0;

        foreach (var entry in artifactIndex)
        {
            var cellDirectoryRelative = CombinePublicPath("artifacts", entry.CellKey);
            var files = new List<PreparedArtifactFile>();
            var publicFiles = new List<EvidenceReportArtifactFile>();

            foreach (var file in entry.Files)
            {
                if (string.IsNullOrWhiteSpace(file.Name))
                {
                    return PreparedArtifactPlan.Fail($"Artifact index entry '{entry.CellKey}' contains a blank artifact name.");
                }

                if (!IsSelectedArtifact(file.Name))
                {
                    skipped.Add(new PublicReportPublicationSkippedArtifact
                    {
                        CellKey = entry.CellKey,
                        Name = file.Name,
                        Reason = "not public-safe by default"
                    });
                    skippedCount++;
                    publicFiles.Add(new EvidenceReportArtifactFile
                    {
                        Name = file.Name,
                        Path = "",
                        Exists = false
                    });
                    files.Add(new PreparedArtifactFile(file.Name, "", false, "not public-safe by default"));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(file.Path))
                {
                    if (file.Exists)
                    {
                        return PreparedArtifactPlan.Fail($"Artifact '{file.Name}' in cell '{entry.CellKey}' is marked present but does not have a path.");
                    }

                    skipped.Add(new PublicReportPublicationSkippedArtifact
                    {
                        CellKey = entry.CellKey,
                        Name = file.Name,
                        Reason = "missing optional artifact"
                    });
                    skippedCount++;
                    publicFiles.Add(new EvidenceReportArtifactFile
                    {
                        Name = file.Name,
                        Path = "",
                        Exists = false
                    });
                    files.Add(new PreparedArtifactFile(file.Name, "", false, "missing optional artifact"));
                    continue;
                }

                if (!TryResolveArtifactSourcePath(repositoryRoot, sourceRunRoot, file.Path, out var sourcePath, out var resolveError))
                {
                    return PreparedArtifactPlan.Fail(resolveError);
                }

                if (!File.Exists(sourcePath))
                {
                    skipped.Add(new PublicReportPublicationSkippedArtifact
                    {
                        CellKey = entry.CellKey,
                        Name = file.Name,
                        Reason = "missing optional artifact"
                    });
                    skippedCount++;
                    publicFiles.Add(new EvidenceReportArtifactFile
                    {
                        Name = file.Name,
                        Path = "",
                        Exists = false
                    });
                    files.Add(new PreparedArtifactFile(file.Name, "", false, "missing optional artifact"));
                    continue;
                }

                var sourceText = await File.ReadAllTextAsync(sourcePath);
                if (ContainsForbiddenContent(sourceText, out var leak))
                {
                    return PreparedArtifactPlan.Fail($"Artifact '{file.Name}' in cell '{entry.CellKey}' contains forbidden content: {leak}");
                }

                var sourceInfo = new FileInfo(sourcePath);
                if (sourceInfo.Length > MaxCopiedArtifactBytes)
                {
                    skipped.Add(new PublicReportPublicationSkippedArtifact
                    {
                        CellKey = entry.CellKey,
                        Name = file.Name,
                        Reason = $"oversize artifact ({sourceInfo.Length} bytes > {MaxCopiedArtifactBytes} bytes)"
                    });
                    skippedCount++;
                    publicFiles.Add(new EvidenceReportArtifactFile
                    {
                        Name = file.Name,
                        Path = "",
                        Exists = false
                    });
                    files.Add(new PreparedArtifactFile(file.Name, "", false, "oversize artifact"));
                    continue;
                }

                var destinationRelative = CombinePublicPath(cellDirectoryRelative, file.Name);
                if (!dryRun)
                {
                    var destinationPath = Path.Combine(outputRoot, destinationRelative.Replace('/', Path.DirectorySeparatorChar));
                    var destinationDirectory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrWhiteSpace(destinationDirectory))
                    {
                        Directory.CreateDirectory(destinationDirectory);
                    }

                    File.Copy(sourcePath, destinationPath, overwrite: true);
                }

                copiedCount++;
                publicFiles.Add(new EvidenceReportArtifactFile
                {
                    Name = file.Name,
                    Path = destinationRelative,
                    Exists = true
                });
                files.Add(new PreparedArtifactFile(file.Name, destinationRelative, true, null));
            }

            cells.Add(new PreparedArtifactCell(
                entry.CellKey,
                cellDirectoryRelative,
                publicFiles,
                files));
        }

        return PreparedArtifactPlan.Success(
            cells,
            warnings,
            skipped,
            copiedCount,
            skippedCount);
    }

    private static PublicReportArtifactsIndex BuildPublicArtifactIndex(
        EvidenceReport evidenceReport,
        RunReport aggregateReport,
        PreparedArtifactPlan plan)
    {
        var cells = plan.Cells
            .Select(cell => new PublicReportArtifactsCell
            {
                CellKey = cell.CellKey,
                CellDirectory = cell.CellDirectory,
                Files = cell.Files.Select(file => new PublicReportArtifactReference
                {
                    Name = file.Name,
                    Path = file.Path,
                    Exists = file.Exists,
                    Copied = file.Exists,
                    SkipReason = file.SkipReason
                }).ToArray()
            })
            .ToArray();

        return new PublicReportArtifactsIndex
        {
            RunId = evidenceReport.RunId,
            GeneratedAt = aggregateReport.GeneratedAt,
            ArtifactRootKey = "artifacts",
            CopiedArtifactCount = plan.CopiedCount,
            SkippedArtifactCount = plan.SkippedCount,
            Cells = cells
        };
    }

    private static EvidenceReport SanitizeEvidenceReport(EvidenceReport report, IReadOnlyList<PublicReportArtifactsCell> publicArtifactCells)
    {
        var sanitizedIndex = publicArtifactCells
            .Select(cell => new EvidenceReportArtifactEntry
            {
                CellKey = cell.CellKey,
                CellDirectory = cell.CellDirectory,
                Files = cell.Files.Select(file => new EvidenceReportArtifactFile
                {
                    Name = file.Name,
                    Path = file.Exists ? file.Path : "",
                    Exists = file.Exists
                }).ToArray()
            })
            .ToArray();

        var sanitizedProofs = report.ValidationSummary.ProofArtifacts
            .Select(proof => proof with
            {
                ProofDirectory = null,
                Artifacts = []
            })
            .ToArray();

        return report with
        {
            ValidationSummary = report.ValidationSummary with
            {
                ProofArtifacts = sanitizedProofs
            },
            ArtifactIndex = sanitizedIndex
        };
    }

    private static PublicReportPublicationManifest BuildPublicationManifest(
        RunReport aggregateReport,
        EvidenceReport evidenceReport,
        PublicReportArtifactsIndex artifactIndex,
        ReportClaimLevel claimLevel,
        ExecutionProfile executionProfile,
        bool publishable,
        bool allowDiagnosticPublication)
    {
        var evidenceClasses = BuildEvidenceClassSummary(aggregateReport);
        var benchmarkCounts = BuildBenchmarkCounts(evidenceReport);

        return new PublicReportPublicationManifest
        {
            RunId = aggregateReport.RunId,
            GeneratedAt = aggregateReport.GeneratedAt,
            Visibility = DefaultVisibility,
            SourceKind = "local-run",
            ClaimLevel = claimLevel.ToString(),
            Publishable = publishable,
            DiagnosticOnly = claimLevel == ReportClaimLevel.DiagnosticOnly,
            AllowDiagnosticPublication = allowDiagnosticPublication,
            ExecutionProfile = ExecutionProfiles.ToId(executionProfile),
            Implementations = aggregateReport.Aggregates
                .Select(static aggregate => aggregate.ImplementationId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Scenarios = aggregateReport.Aggregates
                .Select(static aggregate => aggregate.ScenarioId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Protocols = aggregateReport.Aggregates
                .Select(static aggregate => aggregate.Protocol)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ValidationCounts = aggregateReport.Totals.Validation,
            BenchmarkCounts = benchmarkCounts,
            EvidenceClasses = evidenceClasses,
            WarningCount = evidenceReport.Warnings.Count,
            CopiedArtifactCount = artifactIndex.CopiedArtifactCount,
            SkippedArtifactCount = artifactIndex.SkippedArtifactCount,
            ArtifactRootKey = artifactIndex.ArtifactRootKey
        };
    }

    private static PublicReportRegistryEntry BuildRegistryEntry(
        RunReport aggregateReport,
        EvidenceReport evidenceReport,
        PublicReportArtifactsIndex artifactIndex,
        ReportClaimLevel claimLevel,
        ExecutionProfile executionProfile,
        bool publishable)
    {
        var evidenceClasses = BuildEvidenceClassSummary(aggregateReport);
        var benchmarkCounts = BuildBenchmarkCounts(evidenceReport);
        var bundlePrefix = CombinePublicPath("public", "runs", aggregateReport.RunId);

        return new PublicReportRegistryEntry
        {
            RunId = aggregateReport.RunId,
            GeneratedAt = aggregateReport.GeneratedAt,
            BundlePrefix = bundlePrefix + "/",
            EvidenceReportJsonKey = CombinePublicPath(bundlePrefix, "evidence-report-v1.json"),
            EvidenceReportMarkdownKey = CombinePublicPath(bundlePrefix, "evidence-report-v1.md"),
            ArtifactsIndexKey = CombinePublicPath(bundlePrefix, "artifacts-index.json"),
            PublicationManifestKey = CombinePublicPath(bundlePrefix, "publication-manifest.json"),
            PublicationWarningsKey = CombinePublicPath(bundlePrefix, "publication-warnings.md"),
            PublicationSkippedKey = CombinePublicPath(bundlePrefix, "publication-skipped.md"),
            ReportIndexEntryKey = CombinePublicPath(bundlePrefix, "report-index-entry.json"),
            ReportIndexKey = CombinePublicPath(bundlePrefix, "report-index.json"),
            Visibility = DefaultVisibility,
            SourceKind = "local-run",
            ClaimLevel = claimLevel.ToString(),
            Publishable = publishable,
            DiagnosticOnly = claimLevel == ReportClaimLevel.DiagnosticOnly,
            ExecutionProfile = ExecutionProfiles.ToId(executionProfile),
            Implementations = aggregateReport.Aggregates
                .Select(static aggregate => aggregate.ImplementationId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Scenarios = aggregateReport.Aggregates
                .Select(static aggregate => aggregate.ScenarioId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Protocols = aggregateReport.Aggregates
                .Select(static aggregate => aggregate.Protocol)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ValidationCounts = aggregateReport.Totals.Validation,
            BenchmarkCounts = benchmarkCounts,
            EvidenceClasses = evidenceClasses,
            WarningCount = evidenceReport.Warnings.Count,
            ArtifactRootKey = artifactIndex.ArtifactRootKey
        };
    }

    private static PublicReportEvidenceClassSummary BuildEvidenceClassSummary(RunReport report)
    {
        var counts = report.Aggregates
            .Select(static aggregate => aggregate.Evidence?.EvidenceClass)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return new PublicReportEvidenceClassSummary
        {
            Counts = counts
        };
    }

    private static PublicReportBenchmarkCounts BuildBenchmarkCounts(EvidenceReport report)
    {
        return new PublicReportBenchmarkCounts
        {
            Accepted = report.BenchmarkAcceptance.AcceptedBenchmarks,
            Rejected = report.BenchmarkAcceptance.RejectedBenchmarks,
            NotRunValidationFailed = report.BenchmarkAcceptance.NotRunValidationFailed,
            NotRunUnsupported = report.BenchmarkAcceptance.NotRunUnsupported,
            NotRunLoadToolFailed = report.BenchmarkAcceptance.NotRunLoadToolFailed,
            NotRunParserFailed = report.BenchmarkAcceptance.NotRunParserFailed
        };
    }

    private static bool DeterminePublishable(
        IReadOnlyList<LoadProfileDefinition> loadProfiles,
        IReadOnlyList<RunAggregate> aggregates,
        out IReadOnlyList<string> warnings)
    {
        var messages = new List<string>();
        var publishable = true;
        var loadProfileIds = aggregates
            .Select(static aggregate => aggregate.LoadProfileId)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (loadProfileIds.Length == 0)
        {
            messages.Add("No load profile identifiers were present in the aggregate report; publishable=false is assumed.");
            warnings = messages;
            return false;
        }

        foreach (var loadProfileId in loadProfileIds)
        {
            var profile = loadProfiles.FirstOrDefault(lp => string.Equals(lp.Id, loadProfileId, StringComparison.OrdinalIgnoreCase));
            if (profile is null)
            {
                messages.Add($"Load profile '{loadProfileId}' was not found in the repository catalog; publishable=false is assumed.");
                publishable = false;
                continue;
            }

            if (!profile.Evidence.Publishable)
            {
                messages.Add($"Load profile '{profile.Id}' is marked publishable=false.");
                publishable = false;
            }
        }

        warnings = messages;
        return publishable;
    }

    private static bool IsClaimAllowedForExecutionProfile(ReportClaimLevel claimLevel, ExecutionProfile executionProfile)
    {
        return claimLevel switch
        {
            ReportClaimLevel.Benchmark or ReportClaimLevel.Verified => executionProfile is ExecutionProfile.DedicatedLabBareMetal or ExecutionProfile.DedicatedLabContainer,
            _ => true
        };
    }

    private static string BuildWarningsMarkdown(
        RunReport aggregateReport,
        EvidenceReport evidenceReport,
        PublicReportPublicationManifest manifest,
        PreparedArtifactPlan plan)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Publication Warnings");
        builder.AppendLine();
        builder.AppendLine($"- Run ID: {manifest.RunId}");
        builder.AppendLine($"- Claim level: {manifest.ClaimLevel}");
        builder.AppendLine($"- Publishable load profiles: {(manifest.Publishable ? "yes" : "no")}");
        builder.AppendLine($"- Diagnostic publication allowed: {(manifest.AllowDiagnosticPublication ? "yes" : "no")}");
        builder.AppendLine($"- Copied artifacts: {manifest.CopiedArtifactCount}");
        builder.AppendLine($"- Skipped artifacts: {manifest.SkippedArtifactCount}");
        builder.AppendLine();

        if (aggregateReport.Totals.WarningCount == 0 && evidenceReport.Warnings.Count == 0 && plan.Warnings.Count == 0 && !manifest.DiagnosticOnly && manifest.Publishable)
        {
            builder.AppendLine("_No publication warnings._");
            builder.AppendLine();
            return builder.ToString();
        }

        builder.AppendLine("## Warnings");
        builder.AppendLine();
        foreach (var warning in plan.Warnings)
        {
            builder.AppendLine($"- [{EscapeMarkdown(warning.Code)}] {EscapeMarkdown(warning.Message)}");
        }

        if (evidenceReport.Warnings.Count > 0)
        {
            foreach (var warning in evidenceReport.Warnings)
            {
                builder.AppendLine($"- [report:{EscapeMarkdown(warning.Category)}] {EscapeMarkdown(warning.Message)}");
            }
        }

        builder.AppendLine();
        return builder.ToString();
    }

    private static string BuildSkippedMarkdown(PreparedArtifactPlan plan)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Publication Skipped Artifacts");
        builder.AppendLine();

        if (plan.Skipped.Count == 0)
        {
            builder.AppendLine("_No skipped artifacts._");
            builder.AppendLine();
            return builder.ToString();
        }

        builder.AppendLine("| Cell | Artifact | Reason |");
        builder.AppendLine("| --- | --- | --- |");
        foreach (var artifact in plan.Skipped)
        {
            builder.Append("| ");
            builder.Append(EscapeMarkdown(artifact.CellKey));
            builder.Append(" | ");
            builder.Append(EscapeMarkdown(artifact.Name));
            builder.Append(" | ");
            builder.Append(EscapeMarkdown(artifact.Reason));
            builder.AppendLine(" |");
        }

        builder.AppendLine();
        return builder.ToString();
    }

    private static bool IsSelectedArtifact(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name.EndsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        return AllowedArtifactNames.Contains(name);
    }

    private static bool ContainsForbiddenContent(string text, out string evidence)
    {
        foreach (var marker in ForbiddenPathMarkers)
        {
            if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                evidence = marker;
                return true;
            }
        }

        foreach (var pattern in SecretPatterns)
        {
            if (pattern.IsMatch(text))
            {
                evidence = pattern.ToString();
                return true;
            }
        }

        evidence = "";
        return false;
    }

    private static bool TryLoadReport<T>(string path, string label, out T? value, out string? error)
    {
        try
        {
            var text = File.ReadAllText(path);
            value = ResultJson.Deserialize<T>(text);
            if (value is null)
            {
                error = $"Malformed {label}: '{path}'.";
                return false;
            }

            error = null;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            value = default;
            error = $"Failed to load {label} '{path}': {ex.Message}";
            return false;
        }
    }

    private static string? FirstExistingPath(params string[] paths)
    {
        return paths.FirstOrDefault(File.Exists);
    }

    private static string ResolveOutputRoot(string repositoryRoot, string sourceRunRoot, RunnerCommandOptions options)
    {
        var requested = options.Get("output");
        if (!string.IsNullOrWhiteSpace(requested))
        {
            return Path.GetFullPath(requested, repositoryRoot);
        }

        var runId = Path.GetFileName(sourceRunRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return Path.GetFullPath(Path.Combine(repositoryRoot, ".artifacts", "publication", runId));
    }

    private static string? ResolveRunRoot(string repositoryRoot, RunnerCommandOptions options)
    {
        var runPath = options.Get("run");
        if (!string.IsNullOrWhiteSpace(runPath))
        {
            return Path.GetFullPath(runPath, repositoryRoot);
        }

        var runId = options.Get("run-id");
        if (!string.IsNullOrWhiteSpace(runId))
        {
            return Path.GetFullPath(Path.Combine(repositoryRoot, ".artifacts", "runs", runId));
        }

        return null;
    }

    private static bool IsTruthy(string? value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnderRoot(string root, string path)
    {
        var rootFull = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var candidateFull = Path.GetFullPath(path);
        return candidateFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveArtifactSourcePath(
        string repositoryRoot,
        string sourceRunRoot,
        string artifactPath,
        out string sourcePath,
        out string error)
    {
        var repositoryRootFull = Path.GetFullPath(repositoryRoot);
        var sourceRunRootFull = Path.GetFullPath(sourceRunRoot);
        var candidates = new[]
        {
            Path.GetFullPath(artifactPath, repositoryRootFull),
            Path.GetFullPath(artifactPath, sourceRunRootFull)
        };

        var allowedCandidates = candidates
            .Where(candidate => IsUnderRoot(sourceRunRootFull, candidate))
            .ToArray();

        if (allowedCandidates.Length == 0)
        {
            sourcePath = "";
            error = $"Artifact path escapes the run root: {artifactPath}";
            return false;
        }

        sourcePath = allowedCandidates.FirstOrDefault(File.Exists) ?? allowedCandidates[0];
        error = "";
        return true;
    }

    private static string CombinePublicPath(params string[] segments)
    {
        return string.Join('/', segments.SelectMany(segment => segment.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)));
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static async Task WriteTextAsync(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content);
    }

    private sealed record PreparedArtifactFile(
        string Name,
        string Path,
        bool Exists,
        string? SkipReason);

    private sealed record PreparedArtifactCell(
        string CellKey,
        string CellDirectory,
        IReadOnlyList<EvidenceReportArtifactFile> PublicFiles,
        IReadOnlyList<PreparedArtifactFile> Files);

    private sealed record PreparedArtifactPlan(
        bool CanPublish,
        string? ErrorMessage,
        IReadOnlyList<PreparedArtifactCell> Cells,
        IReadOnlyList<PublicReportPublicationWarning> Warnings,
        IReadOnlyList<PublicReportPublicationSkippedArtifact> Skipped,
        int CopiedCount,
        int SkippedCount)
    {
        public static PreparedArtifactPlan Success(
            IReadOnlyList<PreparedArtifactCell> cells,
            IReadOnlyList<PublicReportPublicationWarning> warnings,
            IReadOnlyList<PublicReportPublicationSkippedArtifact> skipped,
            int copiedCount,
            int skippedCount)
        {
            return new PreparedArtifactPlan(true, null, cells, warnings, skipped, copiedCount, skippedCount);
        }

        public static PreparedArtifactPlan Fail(string errorMessage)
        {
            return new PreparedArtifactPlan(false, errorMessage, [], [], [], 0, 0);
        }
    }
}
