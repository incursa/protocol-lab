// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Cli;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Tests;

public sealed class CliRunTests
{
    [Fact]
    public async Task Check_reports_known_load_tools()
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var exitCode = await ProtocolLabCommand.RunAsync(["check", "--root", TestPaths.RepoRoot]);

            Assert.Equal(0, exitCode);
            var output = writer.ToString();
            Assert.Contains("h2load", output);
            Assert.Contains("oha", output);
            Assert.Contains("Docker:", output);
            Assert.Contains(".NET SDK:", output);
            Assert.Contains("dotnet tool restore state:", output);
            Assert.Contains("dotnet-counters:", output);
            Assert.Contains("h2load Docker image:", output);
            Assert.Contains("--h3 proof:", output);
            Assert.Contains("--output-file proof:", output);
            Assert.Contains("--qlog-file-base proof:", output);
            Assert.Contains("managed H3 proof:", output);
            Assert.Contains("managed H3 load:", output);
            Assert.Contains("Kestrel manifest:", output);
            Assert.Contains("Incursa manifest:", output);
            Assert.Contains("Warnings and remediation:", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task Run_without_base_url_preserves_load_tool_artifact_files()
    {
        var output = Path.Combine(Path.GetTempPath(), $"protocol-lab-{Guid.NewGuid():N}");

        try
        {
            var exitCode = await ProtocolLabCommand.RunAsync(
            [
                "run",
                "--root", TestPaths.RepoRoot,
                "--implementations", "kestrel-http3",
                "--scenarios", "http.core.plaintext",
                "--protocol", "h1",
                "--output", output,
                "--run-id", "artifact-test",
                "--load-tool", "definitely-missing-protocol-lab-tool"
            ]);

            Assert.Equal(0, exitCode);

            var cellDirectory = Path.Combine(
                output,
                "artifact-test",
                "implementations",
                "kestrel-http3",
                "http.core.plaintext",
                "h1",
                "c1-s1-r1");

            Assert.True(File.Exists(Path.Combine(cellDirectory, "load-tool.stdout.txt")));
            Assert.True(File.Exists(Path.Combine(cellDirectory, "load-tool.stderr.txt")));
            Assert.True(File.Exists(Path.Combine(cellDirectory, "target.stdout.txt")));
            Assert.True(File.Exists(Path.Combine(cellDirectory, "target.stderr.txt")));
            Assert.True(File.Exists(Path.Combine(cellDirectory, "manifest.json")));
            Assert.True(File.Exists(Path.Combine(cellDirectory, "scenario.json")));
            Assert.True(File.Exists(Path.Combine(cellDirectory, "target-execution.json")));
            Assert.Contains("Load tool 'definitely-missing-protocol-lab-tool' does not have a manifest", File.ReadAllText(Path.Combine(cellDirectory, "load-tool.stderr.txt")));

            var runDescriptor = ResultJson.Deserialize<RunDescriptor>(
                File.ReadAllText(Path.Combine(output, "artifact-test", "run.json")));
            Assert.NotNull(runDescriptor);
            Assert.Equal("artifact-test", runDescriptor.RunId);
            Assert.NotNull(runDescriptor.Metadata);
            Assert.False(string.IsNullOrWhiteSpace(runDescriptor.Metadata.HostName));

            var report = ResultJson.Deserialize<RunReport>(
                File.ReadAllText(Path.Combine(output, "artifact-test", "aggregate-results.json")));
            Assert.NotNull(report);
            Assert.Equal(1, report.Totals.ResultCount);
            Assert.Equal(0, report.Totals.BenchmarkAttemptCount);
            Assert.Single(report.Aggregates);
            Assert.Equal(1, report.Aggregates[0].Validation.Passed);
            var evidence = report.Aggregates[0].Evidence;
            Assert.NotNull(evidence);
            Assert.Equal(BenchmarkEvidenceClasses.LocalSmoke, evidence!.EvidenceClass);
            Assert.Equal(BenchmarkComparabilityStatuses.Invalid, evidence.ComparabilityStatus);

            var summary = File.ReadAllText(Path.Combine(output, "artifact-test", "summary.md"));
            Assert.Contains("## Run Metadata", summary);
            Assert.Contains("## Aggregate Results", summary);
            Assert.Contains("## Target Metadata", summary);
            Assert.Contains("## Interpretation", summary);
            Assert.Contains("HTTP Plaintext", summary);
            Assert.Contains("local-smoke", summary);
            Assert.Contains("invalid", summary);
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
    public async Task Validate_with_unsupported_network_profile_reports_unsupported()
    {
        var output = Path.Combine(Path.GetTempPath(), $"protocol-lab-{Guid.NewGuid():N}");

        try
        {
            var exitCode = await ProtocolLabCommand.RunAsync(
            [
                "validate",
                "--root", TestPaths.RepoRoot,
                "--implementations", "kestrel-http3",
                "--scenarios", "http.core.plaintext",
                "--protocol", "h3",
                "--network-profile", "rtt-25ms",
                "--output", output,
                "--run-id", "unsupported-network"
            ]);

            Assert.Equal(0, exitCode);

            var validations = ResultJson.Deserialize<List<ScenarioValidationResult>>(
                File.ReadAllText(Path.Combine(output, "unsupported-network", "validation-results.json")));

            Assert.NotNull(validations);
            var validation = Assert.Single(validations);
            Assert.Equal(ValidationStatus.Unsupported, validation.Status);
            Assert.Contains("docker-tc", validation.Summary);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }
}
