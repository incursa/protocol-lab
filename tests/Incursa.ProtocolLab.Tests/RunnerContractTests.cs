// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Runner;

namespace Incursa.ProtocolLab.Tests;

public sealed class RunnerContractTests
{
    [Fact]
    public async Task Check_returns_structured_result()
    {
        var result = await new RunnerEngine().CheckAsync(TestPaths.RepoRoot);

        Assert.Equal(RunnerCommandKind.Check, result.Kind);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains(result.Messages, message => message.Text.Contains("ProtocolLab check", StringComparison.Ordinal));
        Assert.Contains(result.Messages, message => message.Text.Contains("Load tools:", StringComparison.Ordinal));
        Assert.Contains(result.Messages, message => message.Text.Contains("Warnings and remediation:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Validation_failure_returns_structured_result()
    {
        var output = Path.Combine(Path.GetTempPath(), $"protocol-lab-validation-contract-{Guid.NewGuid():N}");
        var options = new RunnerCommandOptions(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["implementations"] = "kestrel-http3",
            ["scenarios"] = "http.core.plaintext",
            ["protocol"] = "h1",
            ["base-url"] = "http://127.0.0.1:1",
            ["output"] = output,
            ["run-id"] = "validation-failure"
        });

        try
        {
            var result = await new RunnerEngine().ValidateAsync(TestPaths.RepoRoot, options);

            Assert.Equal(RunnerCommandKind.Validate, result.Kind);
            Assert.Equal(1, result.ExitCode);
            Assert.Contains(result.Messages, message => message.Text.Contains("failed", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Artifacts, artifact => artifact.Kind == "validation-results" && File.Exists(artifact.Path));
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
    public async Task Run_result_records_report_artifact_references()
    {
        var output = Path.Combine(Path.GetTempPath(), $"protocol-lab-run-contract-{Guid.NewGuid():N}");
        var options = new RunnerCommandOptions(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["implementations"] = "kestrel-http3",
            ["scenarios"] = "http.core.plaintext",
            ["protocol"] = "h1",
            ["output"] = output,
            ["run-id"] = "artifact-references",
            ["test-executor"] = "definitely-missing-protocol-lab-tool"
        });

        try
        {
            var result = await new RunnerEngine().RunBenchmarkAsync(TestPaths.RepoRoot, options);

            Assert.Equal(RunnerCommandKind.Run, result.Kind);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains(result.Artifacts, artifact => artifact.Kind == "run-descriptor" && File.Exists(artifact.Path));
            Assert.Contains(result.Artifacts, artifact => artifact.Kind == "aggregate-results" && File.Exists(artifact.Path));
            Assert.Contains(result.Artifacts, artifact => artifact.Kind == "summary" && File.Exists(artifact.Path));
            Assert.Contains(result.Artifacts, artifact => artifact.Kind == "evidence-report-v1-json" && File.Exists(artifact.Path));
            Assert.Contains(result.Artifacts, artifact => artifact.Kind == "public-report-bundle" && Directory.Exists(artifact.Path));
            Assert.True(File.Exists(Path.Combine(output, "artifact-references", "evidence-report-v1.json")));
            Assert.True(File.Exists(Path.Combine(output, "publication", "artifact-references", "evidence-report-v1.json")));
            Assert.True(File.Exists(Path.Combine(output, "publication", "artifact-references", "artifacts-index.json")));
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
    public async Task Recording_event_sink_captures_status_events()
    {
        var events = new RecordingRunnerEventSink();

        var result = await new RunnerEngine().CheckAsync(TestPaths.RepoRoot, events);

        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(events.Events);
        Assert.Contains(events.Events, runnerEvent => runnerEvent.CommandKind == RunnerCommandKind.Check);
        Assert.Contains(events.Events, runnerEvent => runnerEvent.Message.Contains("ProtocolLab check", StringComparison.Ordinal));
    }
}
