// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Runner;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Tests;

public sealed class RunnerBoundaryTests
{
    [Fact]
    public void Run_plan_builder_expands_selected_cells_deterministically()
    {
        var builder = new RunPlanBuilder();
        var implementation = NewImplementation();
        var scenario = NewHttpScenario();

        var cells = builder.Build(
            [implementation],
            [scenario],
            new MatrixOptions(
                ImplementationIds: ["kestrel-http3"],
                ScenarioIds: ["http.core.plaintext"],
                Protocols: ["h1"],
                Connections: [1, 4],
                StreamsPerConnection: [1, 2],
                DurationSeconds: 10,
                WarmupSeconds: 1,
                Repetitions: 2,
                NetworkProfiles: ["clean"]));

        Assert.Collection(
            cells,
            cell => AssertCell(cell, 1, 1, 1),
            cell => AssertCell(cell, 1, 1, 2),
            cell => AssertCell(cell, 1, 2, 1),
            cell => AssertCell(cell, 1, 2, 2),
            cell => AssertCell(cell, 4, 1, 1),
            cell => AssertCell(cell, 4, 1, 2),
            cell => AssertCell(cell, 4, 2, 1),
            cell => AssertCell(cell, 4, 2, 2));
        Assert.All(cells, cell =>
        {
            Assert.Equal("kestrel-http3", cell.Implementation.Id);
            Assert.Equal("http.core.plaintext", cell.Scenario.Id);
            Assert.Equal("h1", cell.Protocol);
            Assert.Equal(10, cell.DurationSeconds);
            Assert.Equal(1, cell.WarmupSeconds);
            Assert.Equal("clean", cell.NetworkProfile);
        });
    }

    [Fact]
    public void Compatibility_classifier_reports_runnable_and_unsupported_cells()
    {
        var runnable = NewCell();
        var unsupported = NewCell(protocol: "h3", implementation: NewImplementation(supportedProtocols: ["h1"]));
        var loadTools = new[]
        {
            new LoadToolManifest
            {
                Id = "h2load",
                Name = "h2load",
                Kind = LoadToolKinds.Process,
                SupportedProtocols = ["h1"],
                SupportedScenarioFamilies = ["http.application"]
            }
        };

        var runnableResult = CompatibilityClassifier.Classify(runnable, loadTools);
        var unsupportedResult = CompatibilityClassifier.Classify(unsupported, loadTools);
        var missingToolResult = CompatibilityClassifier.Classify(runnable, loadTools, requestedLoadTool: "missing");

        Assert.True(runnableResult.CanRun);
        Assert.Equal(RunCellCompatibilityStatuses.MissingCapability, unsupportedResult.Status);
        Assert.Contains("protocol 'h3' is not supported", unsupportedResult.Reason);
        Assert.Equal(RunCellCompatibilityStatuses.MissingLoadTool, missingToolResult.Status);
        Assert.Contains("missing", missingToolResult.Reason);
    }

    [Fact]
    public async Task Validation_failure_blocks_accepted_load_results()
    {
        var output = Path.Combine(Path.GetTempPath(), $"protocol-lab-runner-{Guid.NewGuid():N}");
        var runId = "validation-fails";
        var options = new RunnerCommandOptions(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["implementations"] = "kestrel-http3",
            ["scenarios"] = "http.core.plaintext",
            ["protocol"] = "h1",
            ["base-url"] = "http://127.0.0.1:1",
            ["output"] = output,
            ["run-id"] = runId
        });

        try
        {
            var commandResult = await new RunnerEngine().RunBenchmarkAsync(TestPaths.RepoRoot, options);

            Assert.Equal(1, commandResult.ExitCode);
            Assert.Equal(RunnerCommandKind.Run, commandResult.Kind);
            var resultPath = Path.Combine(
                output,
                runId,
                "implementations",
                "kestrel-http3",
                "http.core.plaintext",
                "h1",
                TestPaths.ExecutionProfileId,
                "clean",
                "no-load-profile",
                "c1-s1-r1",
                "result.json");
            var result = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(resultPath));

            Assert.NotNull(result);
            Assert.Equal(ValidationStatus.Failed, result.ValidationResult.Status);
            Assert.Equal(LoadToolExecutionStatuses.Skipped, result.BenchmarkExecutionStatus);
            Assert.False(result.ParsedMetricsAvailable);
            Assert.Equal(BenchmarkComparabilityStatuses.Invalid, result.Evidence?.ComparabilityStatus);
            Assert.Contains(result.Warnings, warning => warning.Contains("validation did not pass", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("Validation did not pass; load tool was not invoked.", await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(resultPath)!, "load-tool.stderr.txt")));
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Readiness_failure_records_target_diagnostics()
    {
        var output = Path.Combine(Path.GetTempPath(), $"protocol-lab-readiness-{Guid.NewGuid():N}");
        var cell = NewCell(implementation: NewImplementation(
            executable: "dotnet",
            commandArguments: ["--info"],
            readiness: new ReadinessCheck
            {
                Type = ReadinessCheckTypes.Http,
                Url = "/plaintext",
                TimeoutSeconds = 1
            }));
        var paths = ArtifactLayout.GetCellPaths(output, "readiness-fails", cell);

        try
        {
            Directory.CreateDirectory(paths.CellDirectory);

            await using var target = await TargetOrchestrator.StartAsync(
                TestPaths.RepoRoot,
                cell.Implementation,
                externalBaseUrl: null,
                paths,
                requestedProtocol: "h1",
                new TargetStartOptions(Mode: TargetKinds.Process));

            Assert.True(target.Result.Failed);
            Assert.False(target.Result.Ready);
            Assert.Contains(target.Result.Errors, error => error.Contains("ready", StringComparison.OrdinalIgnoreCase) || error.Contains("exited", StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(paths.TargetExecutionJson));
            Assert.True(File.Exists(paths.TargetStdout));
            Assert.True(File.Exists(paths.TargetStderr));

            var targetExecutionJson = await File.ReadAllTextAsync(paths.TargetExecutionJson);
            Assert.Contains(TargetExecutionStatuses.Failed, targetExecutionJson);
            Assert.Contains("target.stdout.txt", targetExecutionJson);
            Assert.Contains("target.stderr.txt", targetExecutionJson);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    private static void AssertCell(RunCell cell, int connections, int streams, int repetition)
    {
        Assert.Equal(connections, cell.Connections);
        Assert.Equal(streams, cell.StreamsPerConnection);
        Assert.Equal(repetition, cell.Repetition);
    }

    private static RunCell NewCell(string protocol = "h1", ImplementationManifest? implementation = null)
    {
        return new RunCell(
            implementation ?? NewImplementation(),
            NewHttpScenario(protocol),
            protocol,
            1,
            1,
            1,
            30,
            5,
            "clean")
        {
            ExecutionProfile = TestPaths.ExpectedExecutionProfile
        };
    }

    private static ImplementationManifest NewImplementation(
        IReadOnlyList<string>? supportedProtocols = null,
        string executable = "",
        IReadOnlyList<string>? commandArguments = null,
        ReadinessCheck? readiness = null)
    {
        return new ImplementationManifest
        {
            Id = "kestrel-http3",
            Name = "Kestrel",
            TargetKind = string.IsNullOrWhiteSpace(executable) ? "" : TargetKinds.Process,
            Executable = executable,
            CommandArguments = commandArguments?.ToList() ?? [],
            BaseUrl = "http://127.0.0.1:1",
            Roles = ["server"],
            SupportedProtocols = supportedProtocols?.ToList() ?? ["h1"],
            SupportedWorkloadFamilies = ["http.application"],
            Capabilities = ["httpPlaintext"],
            ReadinessCheck = readiness ?? new ReadinessCheck { Type = ReadinessCheckTypes.None }
        };
    }

    private static ScenarioDefinition NewHttpScenario(string protocol = "h1")
    {
        return new ScenarioDefinition
        {
            Id = "http.core.plaintext",
            Name = "Plaintext",
            Family = "http.application",
            Protocol = protocol,
            ImplementationRole = "server",
            RequiredCapabilities = ["httpPlaintext"],
            Endpoint = new HttpEndpointSpec
            {
                Method = "GET",
                Path = "/plaintext",
                ExpectedStatus = 200,
                ExpectedBodyRule = "exact",
                ExpectedBody = "Hello, World!"
            },
            Benchmark = new BenchmarkLoadShape
            {
                DurationSeconds = 30,
                WarmupSeconds = 5,
                Repetitions = 1,
                Connections = [1],
                StreamsPerConnection = [1]
            },
            NetworkProfile = "clean"
        };
    }
}
