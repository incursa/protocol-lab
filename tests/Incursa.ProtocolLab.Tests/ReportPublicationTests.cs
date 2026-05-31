// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Model;
using Incursa.ProtocolLab.Runner;

namespace Incursa.ProtocolLab.Tests;

public sealed class ReportPublicationTests
{
    [Fact]
    public async Task Publishes_public_bundle_and_registry_entry_from_sample_run()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var sample = await CreateSampleRunAsync(tempRoot);
            var result = await new RunnerEngine().PublishReportAsync(
                TestPaths.RepoRoot,
                CreatePublishOptions(sample.RunRoot, sample.OutputRoot));

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(result.Messages, message => message.Text.Contains("Report bundle written", StringComparison.OrdinalIgnoreCase));

            Assert.True(File.Exists(Path.Combine(sample.OutputRoot, "evidence-report-v1.json")));
            Assert.True(File.Exists(Path.Combine(sample.OutputRoot, "evidence-report-v1.md")));
            Assert.True(File.Exists(Path.Combine(sample.OutputRoot, "artifacts-index.json")));
            Assert.True(File.Exists(Path.Combine(sample.OutputRoot, "publication-manifest.json")));
            Assert.True(File.Exists(Path.Combine(sample.OutputRoot, "publication-warnings.md")));
            Assert.True(File.Exists(Path.Combine(sample.OutputRoot, "publication-skipped.md")));
            Assert.True(File.Exists(Path.Combine(sample.OutputRoot, "report-index-entry.json")));
            Assert.True(File.Exists(Path.Combine(sample.OutputRoot, "report-index.json")));
            Assert.True(File.Exists(Path.Combine(sample.OutputRoot, "artifacts", "kestrel-http3", "http.core.plaintext", "h3", "c1-s1-r1", "manifest.json")));

            var manifest = ResultJson.Deserialize<PublicReportPublicationManifest>(
                await File.ReadAllTextAsync(Path.Combine(sample.OutputRoot, "publication-manifest.json")));
            Assert.NotNull(manifest);
            Assert.Equal("sample-run", manifest!.RunId);
            Assert.Equal("Regression", manifest.ClaimLevel);
            Assert.False(manifest.Publishable);
            Assert.Equal("local-run", manifest.SourceKind);
            Assert.Equal("local-process", manifest.ExecutionProfile);
            Assert.Equal("artifacts", manifest.ArtifactRootKey);
            Assert.True(manifest.CopiedArtifactCount > 0);

            var indexEntry = ResultJson.Deserialize<PublicReportRegistryEntry>(
                await File.ReadAllTextAsync(Path.Combine(sample.OutputRoot, "report-index-entry.json")));
            Assert.NotNull(indexEntry);
            Assert.Equal("sample-run", indexEntry!.RunId);
            Assert.Equal("Regression", indexEntry.ClaimLevel);
            Assert.False(indexEntry.Publishable);
            Assert.Equal(manifest.ArtifactRootKey, indexEntry.ArtifactRootKey);
            Assert.Equal("public/runs/sample-run/", indexEntry.BundlePrefix);
            Assert.Equal("public/runs/sample-run/evidence-report-v1.json", indexEntry.EvidenceReportJsonKey);
            Assert.Equal("public/runs/sample-run/report-index.json", indexEntry.ReportIndexKey);
            Assert.True(indexEntry.EvidenceClasses.Counts.Count > 0);

            var artifactsIndex = ResultJson.Deserialize<PublicReportArtifactsIndex>(
                await File.ReadAllTextAsync(Path.Combine(sample.OutputRoot, "artifacts-index.json")));
            Assert.NotNull(artifactsIndex);
            Assert.Equal("sample-run", artifactsIndex!.RunId);
            Assert.True(artifactsIndex.CopiedArtifactCount > 0);
            Assert.True(artifactsIndex.SkippedArtifactCount > 0);
            Assert.All(artifactsIndex.Cells.SelectMany(cell => cell.Files), file =>
            {
                Assert.DoesNotContain(@"C:\shared", file.Path, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("protocol-lab-internal", file.Path, StringComparison.OrdinalIgnoreCase);
            });

            var evidenceReport = ResultJson.Deserialize<EvidenceReport>(
                await File.ReadAllTextAsync(Path.Combine(sample.OutputRoot, "evidence-report-v1.json")));
            Assert.NotNull(evidenceReport);
            Assert.Equal("sample-run", evidenceReport!.RunId);
            Assert.All(evidenceReport.ArtifactIndex, cell =>
            {
                Assert.All(cell.Files, file =>
                {
                    Assert.True(string.IsNullOrWhiteSpace(file.Path) || file.Path.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase));
                    Assert.DoesNotContain(@"C:\shared", file.Path, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("protocol-lab-internal", file.Path, StringComparison.OrdinalIgnoreCase);
                });
            });
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task Dry_run_validates_bundle_without_writing_output_files()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var sample = await CreateSampleRunAsync(tempRoot);
            var result = await new RunnerEngine().PublishReportAsync(
                TestPaths.RepoRoot,
                CreatePublishOptions(sample.RunRoot, sample.OutputRoot, dryRun: true));

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(result.Messages, message => message.Text.Contains("Dry run: report bundle", StringComparison.OrdinalIgnoreCase));
            Assert.False(Directory.Exists(sample.OutputRoot));
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task Detects_private_path_leaks()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var sample = await CreateSampleRunAsync(tempRoot, leakInManifest: true);
            var result = await new RunnerEngine().PublishReportAsync(
                TestPaths.RepoRoot,
                CreatePublishOptions(sample.RunRoot, sample.OutputRoot));

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(result.Messages, message => message.Severity == RunnerMessageSeverity.Error);
            Assert.False(File.Exists(Path.Combine(sample.OutputRoot, "publication-manifest.json")));
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task Writes_skipped_artifact_entries_when_optional_artifacts_are_missing()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var sample = await CreateSampleRunAsync(tempRoot, omitOptionalArtifact: true);
            var result = await new RunnerEngine().PublishReportAsync(
                TestPaths.RepoRoot,
                CreatePublishOptions(sample.RunRoot, sample.OutputRoot));

            Assert.Equal(0, result.ExitCode);
            var skipped = await File.ReadAllTextAsync(Path.Combine(sample.OutputRoot, "publication-skipped.md"));
            Assert.Contains("missing optional artifact", skipped, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("h2load-output.json", skipped, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task DiagnosticOnly_publication_requires_explicit_flag()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var sample = await CreateDiagnosticOnlyRunAsync(tempRoot);
            var noFlag = await new RunnerEngine().PublishReportAsync(
                TestPaths.RepoRoot,
                CreatePublishOptions(sample.RunRoot, sample.OutputRoot, allowDiagnosticPublication: false));

            Assert.NotEqual(0, noFlag.ExitCode);
            Assert.Contains(noFlag.Messages, message => message.Text.Contains("DiagnosticOnly", StringComparison.OrdinalIgnoreCase));

            var withFlag = await new RunnerEngine().PublishReportAsync(
                TestPaths.RepoRoot,
                CreatePublishOptions(sample.RunRoot, sample.OutputRoot, allowDiagnosticPublication: true));

            Assert.Equal(0, withFlag.ExitCode);
            var manifest = ResultJson.Deserialize<PublicReportPublicationManifest>(
                await File.ReadAllTextAsync(Path.Combine(sample.OutputRoot, "publication-manifest.json")));
            Assert.NotNull(manifest);
            Assert.True(manifest!.DiagnosticOnly);
            Assert.True(manifest.AllowDiagnosticPublication);
            Assert.Equal("DiagnosticOnly", manifest.ClaimLevel);
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task Fails_when_claim_level_is_too_high_for_execution_profile()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var sample = await CreateSampleRunAsync(tempRoot, overrideClaimLevel: ReportClaimLevel.Benchmark);
            var result = await new RunnerEngine().PublishReportAsync(
                TestPaths.RepoRoot,
                CreatePublishOptions(sample.RunRoot, sample.OutputRoot));

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(result.Messages, message => message.Text.Contains("too high for execution profile", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task Fails_when_run_root_folder_name_does_not_match_report_run_id()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var sample = await CreateSampleRunAsync(tempRoot);
            var mismatchedRunRoot = Path.Combine(tempRoot, ".artifacts", "runs", "mismatch-run");
            Directory.Move(sample.RunRoot, mismatchedRunRoot);
            var mismatchedOutputRoot = Path.Combine(tempRoot, ".artifacts", "publication", "mismatch-run");

            var result = await new RunnerEngine().PublishReportAsync(
                TestPaths.RepoRoot,
                CreatePublishOptions(mismatchedRunRoot, mismatchedOutputRoot));

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(result.Messages, message => message.Text.Contains("run directory", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task Fails_when_artifact_path_escapes_run_root()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var sample = await CreateSampleRunAsync(tempRoot);
            var evidenceReportPath = Path.Combine(sample.RunRoot, "evidence-report.json");
            var evidenceReport = ResultJson.Deserialize<EvidenceReport>(await File.ReadAllTextAsync(evidenceReportPath));
            Assert.NotNull(evidenceReport);

            var outsideArtifactPath = Path.Combine(tempRoot, "outside", "manifest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outsideArtifactPath)!);
            await File.WriteAllTextAsync(outsideArtifactPath, "{}");

            var targetCell = evidenceReport!.ArtifactIndex[0];
            var targetFileIndex = -1;
            for (var i = 0; i < targetCell.Files.Count; i++)
            {
                if (string.Equals(targetCell.Files[i].Name, "manifest.json", StringComparison.OrdinalIgnoreCase))
                {
                    targetFileIndex = i;
                    break;
                }
            }

            Assert.True(targetFileIndex >= 0);

            evidenceReport = evidenceReport! with
            {
                ArtifactIndex = evidenceReport.ArtifactIndex.Select((cell, index) =>
                    index == 0
                        ? cell with
                        {
                            Files = cell.Files.Select((file, fileIndex) =>
                                fileIndex == targetFileIndex
                                    ? file with
                                    {
                                        Path = outsideArtifactPath,
                                        Exists = true
                                    }
                                    : file).ToArray()
                        }
                        : cell).ToArray()
            };

            await File.WriteAllTextAsync(evidenceReportPath, ResultJson.Serialize(evidenceReport));

            var result = await new RunnerEngine().PublishReportAsync(
                TestPaths.RepoRoot,
                CreatePublishOptions(sample.RunRoot, sample.OutputRoot));

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(result.Messages, message => message.Text.Contains("escapes the run root", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task Resolves_repo_relative_artifact_paths_against_the_repository_root()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var sample = await CreateSampleRunAsync(tempRoot);
            var evidenceReportPath = Path.Combine(sample.RunRoot, "evidence-report.json");
            var evidenceReport = ResultJson.Deserialize<EvidenceReport>(await File.ReadAllTextAsync(evidenceReportPath));
            Assert.NotNull(evidenceReport);

            var targetCell = evidenceReport!.ArtifactIndex[0];
            var targetFileIndex = -1;
            for (var i = 0; i < targetCell.Files.Count; i++)
            {
                if (string.Equals(targetCell.Files[i].Name, "manifest.json", StringComparison.OrdinalIgnoreCase))
                {
                    targetFileIndex = i;
                    break;
                }
            }

            Assert.True(targetFileIndex >= 0);

            var absoluteManifestPath = targetCell.Files[targetFileIndex].Path;
            var repositoryRelativeManifestPath = Path.GetRelativePath(tempRoot, absoluteManifestPath);
            Assert.StartsWith(".artifacts", repositoryRelativeManifestPath, StringComparison.OrdinalIgnoreCase);

            evidenceReport = evidenceReport with
            {
                ArtifactIndex = evidenceReport.ArtifactIndex.Select((cell, index) =>
                    index == 0
                        ? cell with
                        {
                            Files = cell.Files.Select((file, fileIndex) =>
                                fileIndex == targetFileIndex
                                    ? file with
                                    {
                                        Path = repositoryRelativeManifestPath,
                                        Exists = true
                                    }
                                    : file).ToArray()
                        }
                        : cell).ToArray()
            };

            await File.WriteAllTextAsync(evidenceReportPath, ResultJson.Serialize(evidenceReport));

            var result = await new RunnerEngine().PublishReportAsync(
                tempRoot,
                CreatePublishOptions(sample.RunRoot, sample.OutputRoot));

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(Path.Combine(sample.OutputRoot, "artifacts", "kestrel-http3", "http.core.plaintext", "h3", "c1-s1-r1", "manifest.json")));
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task Skips_missing_allowlisted_artifacts_when_the_report_still_points_to_them()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var sample = await CreateSampleRunAsync(tempRoot);
            var evidenceReportPath = Path.Combine(sample.RunRoot, "evidence-report.json");
            var evidenceReport = ResultJson.Deserialize<EvidenceReport>(await File.ReadAllTextAsync(evidenceReportPath));
            Assert.NotNull(evidenceReport);

            var targetCell = evidenceReport!.ArtifactIndex[0];
            var targetFileIndex = -1;
            for (var i = 0; i < targetCell.Files.Count; i++)
            {
                if (string.Equals(targetCell.Files[i].Name, "docker-inspect.json", StringComparison.OrdinalIgnoreCase))
                {
                    targetFileIndex = i;
                    break;
                }
            }

            Assert.True(targetFileIndex >= 0);

            var absoluteDockerInspectPath = targetCell.Files[targetFileIndex].Path;
            var repositoryRelativeDockerInspectPath = Path.GetRelativePath(tempRoot, absoluteDockerInspectPath);
            File.Delete(absoluteDockerInspectPath);

            evidenceReport = evidenceReport with
            {
                ArtifactIndex = evidenceReport.ArtifactIndex.Select((cell, index) =>
                    index == 0
                        ? cell with
                        {
                            Files = cell.Files.Select((file, fileIndex) =>
                                fileIndex == targetFileIndex
                                    ? file with
                                    {
                                        Path = repositoryRelativeDockerInspectPath,
                                        Exists = true
                                    }
                                    : file).ToArray()
                        }
                        : cell).ToArray()
            };

            await File.WriteAllTextAsync(evidenceReportPath, ResultJson.Serialize(evidenceReport));

            var result = await new RunnerEngine().PublishReportAsync(
                tempRoot,
                CreatePublishOptions(sample.RunRoot, sample.OutputRoot));

            Assert.Equal(0, result.ExitCode);
            var skipped = await File.ReadAllTextAsync(Path.Combine(sample.OutputRoot, "publication-skipped.md"));
            Assert.Contains("docker-inspect.json", skipped, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("missing optional artifact", skipped, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task Fails_for_malformed_aggregate_report()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var sample = await CreateSampleRunAsync(tempRoot);
            await File.WriteAllTextAsync(Path.Combine(sample.RunRoot, "aggregate-results.json"), "{ not json");

            var result = await new RunnerEngine().PublishReportAsync(
                TestPaths.RepoRoot,
                CreatePublishOptions(sample.RunRoot, sample.OutputRoot));

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(result.Messages, message => message.Text.Contains("aggregate report", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    private static RunnerCommandOptions CreatePublishOptions(string runRoot, string outputRoot, bool dryRun = false, bool allowDiagnosticPublication = false)
    {
        return new RunnerCommandOptions(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["run"] = runRoot,
            ["output"] = outputRoot,
            ["visibility"] = "public",
            ["dry-run"] = dryRun ? "true" : "false",
            ["allow-diagnostic-publication"] = allowDiagnosticPublication ? "true" : "false"
        });
    }

    private static string CreateTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"protocol-lab-publication-{Guid.NewGuid():N}");
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static async Task<(string RunRoot, string OutputRoot)> CreateSampleRunAsync(
        string tempRoot,
        bool leakInManifest = false,
        bool omitOptionalArtifact = false,
        ReportClaimLevel? overrideClaimLevel = null)
    {
        var runId = "sample-run";
        var runArtifactsRoot = Path.Combine(tempRoot, ".artifacts", "runs");
        var runRoot = Path.Combine(runArtifactsRoot, runId);
        var outputRoot = Path.Combine(tempRoot, ".artifacts", "publication", runId);
        Directory.CreateDirectory(runRoot);

        var metadata = new RunMetadata(
            "sample-host",
            "Windows 11 Pro",
            ".NET 10",
            "X64",
            "X64",
            16,
            true,
            1_000_000_000,
            2_000_000_000,
            "Docker 27",
            "none",
            TimestampUtc: new DateTimeOffset(2026, 05, 31, 12, 0, 0, TimeSpan.Zero),
            GitCommit: "abc123",
            WorkingTreeStatus: "clean")
        {
            ExecutionProfile = ExecutionProfile.LocalProcess
        };

        var cells = BuildCells(runId);
        var compatibilities = cells.Select(_ => RunCellCompatibility.Supported()).ToArray();
        var results = new List<BenchmarkResult>();

        for (var index = 0; index < cells.Count; index++)
        {
            var cell = cells[index];
            var paths = ArtifactLayout.GetCellPaths(runArtifactsRoot, runId, cell);
            Directory.CreateDirectory(paths.CellDirectory);
            await WriteSafeArtifactsAsync(paths, leakInManifest && index == 0, omitOptionalArtifact && index == 1);
            var result = CreateResult(runId, cell, paths, repetition: cell.Repetition, omitOptionalArtifact: omitOptionalArtifact && index == 1);
            results.Add(result);
        }

        var aggregateReport = RunReportBuilder.Build(runId, new DateTimeOffset(2026, 05, 31, 12, 0, 0, TimeSpan.Zero), metadata, results);
        if (overrideClaimLevel.HasValue)
        {
            aggregateReport = aggregateReport with
            {
                ClaimLevel = overrideClaimLevel.Value
            };
        }

        var evidenceReport = EvidenceReportBuilder.Build(
            runId,
            new DateTimeOffset(2026, 05, 31, 12, 0, 0, TimeSpan.Zero),
            metadata,
            "suite",
            "Suite",
            cells,
            compatibilities,
            results);

        await File.WriteAllTextAsync(Path.Combine(runRoot, "run.json"), ResultJson.Serialize(RunReportBuilder.CreateDescriptor(aggregateReport)));
        await File.WriteAllTextAsync(Path.Combine(runRoot, "aggregate-results.json"), ResultJson.Serialize(aggregateReport));
        await File.WriteAllTextAsync(Path.Combine(runRoot, "evidence-report.json"), ResultJson.Serialize(evidenceReport));

        return (runRoot, outputRoot);
    }

    private static async Task<(string RunRoot, string OutputRoot)> CreateDiagnosticOnlyRunAsync(string tempRoot)
    {
        var runId = "diagnostic-run";
        var runArtifactsRoot = Path.Combine(tempRoot, ".artifacts", "runs");
        var runRoot = Path.Combine(runArtifactsRoot, runId);
        var outputRoot = Path.Combine(tempRoot, ".artifacts", "publication", runId);
        Directory.CreateDirectory(runRoot);

        var metadata = new RunMetadata(
            "sample-host",
            "Windows 11 Pro",
            ".NET 10",
            "X64",
            "X64",
            16,
            true,
            1_000_000_000,
            2_000_000_000,
            "Docker 27",
            "none",
            TimestampUtc: new DateTimeOffset(2026, 05, 31, 12, 0, 0, TimeSpan.Zero),
            GitCommit: "abc123",
            WorkingTreeStatus: "clean")
        {
            ExecutionProfile = ExecutionProfile.LocalProcess
        };

        var aggregateReport = RunReportBuilder.Build(runId, new DateTimeOffset(2026, 05, 31, 12, 0, 0, TimeSpan.Zero), metadata, []);
        var evidenceReport = EvidenceReportBuilder.Build(runId, new DateTimeOffset(2026, 05, 31, 12, 0, 0, TimeSpan.Zero), metadata, null, null, [], [], []);

        Directory.CreateDirectory(Path.Combine(runRoot, "implementations"));

        await File.WriteAllTextAsync(Path.Combine(runRoot, "run.json"), ResultJson.Serialize(RunReportBuilder.CreateDescriptor(aggregateReport)));
        await File.WriteAllTextAsync(Path.Combine(runRoot, "aggregate-results.json"), ResultJson.Serialize(aggregateReport));
        await File.WriteAllTextAsync(Path.Combine(runRoot, "evidence-report.json"), ResultJson.Serialize(evidenceReport));

        return (runRoot, outputRoot);
    }

    private static IReadOnlyList<RunCell> BuildCells(string runId)
    {
        var implementation = new ImplementationManifest
        {
            Id = "kestrel-http3",
            Name = "Kestrel HTTP/3",
            TargetKind = "process",
            Roles = ["server"],
            SupportedProtocols = ["h3"],
            SupportedWorkloadFamilies = ["http.application"]
        };

        var scenario = new ScenarioDefinition
        {
            Id = "http.core.plaintext",
            Name = "HTTP Plaintext",
            Family = "http.application",
            Protocol = "h3",
            ImplementationRole = "server",
            Benchmark = new BenchmarkLoadShape
            {
                DurationSeconds = 10,
                WarmupSeconds = 2,
                Repetitions = 2
            }
        };

        return
        [
            new RunCell(implementation, scenario, "h3", 1, 1, 1, 10, 2, "clean", "local-regression")
            {
                ExecutionProfile = ExecutionProfile.LocalProcess
            },
            new RunCell(implementation, scenario, "h3", 1, 1, 2, 10, 2, "clean", "local-regression")
            {
                ExecutionProfile = ExecutionProfile.LocalProcess
            }
        ];
    }

    private static async Task WriteSafeArtifactsAsync(ArtifactPaths paths, bool leakInManifest, bool omitOptionalArtifact)
    {
        await File.WriteAllTextAsync(paths.ManifestSnapshotJson, leakInManifest
            ? """
              {
                "leak": "C:\shared\secret"
              }
              """
            : """
              {
                "implementation": "kestrel-http3",
                "target": "Kestrel HTTP/3"
              }
              """);

        await File.WriteAllTextAsync(paths.ScenarioSnapshotJson, """
            {
              "scenario": "http.core.plaintext",
              "protocol": "h3"
            }
            """);

        await File.WriteAllTextAsync(paths.DockerInspectJson, """
            {
              "container": "kestrel-http3"
            }
            """);

        await File.WriteAllTextAsync(paths.TargetDockerStatsJsonl, """
            {"cpu":1.2,"memory":128}
            """);

        await File.WriteAllTextAsync(paths.CountersSummaryJson, """
            {
              "cpuMean": 12.5,
              "allocationRateMean": 2048
            }
            """);

        await File.WriteAllTextAsync(paths.LoadToolVersion, "oha 1.0.0");

        if (!omitOptionalArtifact)
        {
            await File.WriteAllTextAsync(paths.H2loadOutputJson, """
                {
                  "requestsPerSecond": 123.4
                }
                """);
        }
    }

    private static BenchmarkResult CreateResult(string runId, RunCell cell, ArtifactPaths paths, int repetition, bool omitOptionalArtifact)
    {
        var validation = new ScenarioValidationResult
        {
            ScenarioId = cell.Scenario.Id,
            TargetId = cell.Implementation.Id,
            AdapterId = "",
            Protocol = cell.Protocol,
            Status = ValidationStatus.Passed,
            Summary = "ok"
        };

        var artifacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["manifestSnapshot"] = paths.ManifestSnapshotJson,
            ["scenarioSnapshot"] = paths.ScenarioSnapshotJson,
            ["dockerInspectJson"] = paths.DockerInspectJson,
            ["targetDockerStatsJsonl"] = paths.TargetDockerStatsJsonl,
            ["countersSummary"] = paths.CountersSummaryJson,
            ["loadToolVersion"] = paths.LoadToolVersion
        };

        if (!omitOptionalArtifact)
        {
            artifacts["h2loadOutputJson"] = paths.H2loadOutputJson;
        }

        var result = BenchmarkResult.FromCell(
            runId,
            cell,
            validation,
            "oha",
            parsedMetricsAvailable: true,
            artifacts,
            metrics: new HttpMetrics
            {
                RequestsPerSecond = 123.4 + repetition,
                LatencyMeanMs = 2.5,
                ThroughputBytesPerSecond = 1000
            },
            loadToolMode: TargetKinds.Process,
            loadToolCategory: "external-reference",
            loadToolVersion: "oha 1.0.0",
            benchmarkExecutionStatus: LoadToolExecutionStatuses.Succeeded,
            targetExecution: new TargetExecutionResult
            {
                Status = TargetExecutionStatuses.Ready,
                TargetExecutionMode = TargetKinds.Process,
                Started = true,
                Ready = true,
                StartTimeUtc = new DateTimeOffset(2026, 05, 31, 12, 0, 0, TimeSpan.Zero),
                ReadyTimeUtc = new DateTimeOffset(2026, 05, 31, 12, 0, 1, TimeSpan.Zero),
                ProcessId = 1234,
                CommandLine = "dotnet run"
            })
            with
            {
                LoadProfileId = "local-regression",
                LoadProfileTitle = "Local Regression",
                LoadProfilePurpose = "regression",
                Evidence = new BenchmarkEvidenceAssessment
                {
                    EvidenceClass = BenchmarkEvidenceClasses.LocalLab,
                    ComparabilityStatus = BenchmarkComparabilityStatuses.ComparableLocal
                }
            };

        return result;
    }
}
