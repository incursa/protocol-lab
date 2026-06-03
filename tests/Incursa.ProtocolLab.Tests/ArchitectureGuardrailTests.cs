// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Xml.Linq;

namespace Incursa.ProtocolLab.Tests;

public sealed class ArchitectureGuardrailTests
{
    private static readonly string[] RunnerOwnedFiles =
    [
        "Abstractions/RunnerCommandOptions.cs",
        "Abstractions/RunnerCommandResult.cs",
        "Compatibility/CompatibilityClassifier.cs",
        "Diagnostics/DiagnosticTargetResolver.cs",
        "Diagnostics/DockerContainerMetricsCapture.cs",
        "Diagnostics/ProcessMetricsCapture.cs",
        "Diagnostics/RunMetadataCapture.cs",
        "Diagnostics/RuntimeCounterCapture.cs",
        "Events/RunnerEvents.cs",
        "Events/RunnerOutputBuffer.cs",
        "Lifecycle/TargetOrchestrator.cs",
        "LoadTools/DockerResourceControl.cs",
        "LoadTools/LoadToolInvoker.cs",
        "LoadTools/ManagedHttp3LoadGenerator.cs",
        "Orchestration/RunnerEngine.cs",
        "Planning/RunPlanBuilder.cs",
        "Validation/HttpScenarioValidator.cs",
        "Validation/ProtocolProofValidator.cs"
    ];

    [Fact]
    public void Project_references_keep_cli_to_runner_to_model_direction()
    {
        var cliReferences = ProjectReferences("src", "Incursa.ProtocolLab.Cli", "Incursa.ProtocolLab.Cli.csproj");
        var runnerReferences = ProjectReferences("src", "Incursa.ProtocolLab.Runner", "Incursa.ProtocolLab.Runner.csproj");
        var modelReferences = ProjectReferences("src", "Incursa.ProtocolLab.Model", "Incursa.ProtocolLab.Model.csproj");

        Assert.Equal(["Incursa.ProtocolLab.Runner.csproj"], cliReferences);
        Assert.Equal(["Incursa.ProtocolLab.Adapter.Contracts.csproj", "Incursa.ProtocolLab.Model.csproj"], runnerReferences);
        Assert.Empty(modelReferences);
    }

    [Fact]
    public void Runner_and_cli_do_not_reference_protocol_implementation_assemblies()
    {
        var projectFiles = new[]
        {
            ProjectPath("src", "Incursa.ProtocolLab.Cli", "Incursa.ProtocolLab.Cli.csproj"),
            ProjectPath("src", "Incursa.ProtocolLab.Runner", "Incursa.ProtocolLab.Runner.csproj")
        };

        foreach (var projectFile in projectFiles)
        {
            var project = XDocument.Load(projectFile);
            var references = project.Descendants()
                .Where(static element => element.Name.LocalName is "ProjectReference" or "PackageReference" or "Reference")
                .Select(element =>
                    ((string?)element.Attribute("Include") ?? "") + " " +
                    ((string?)element.Attribute("Update") ?? ""))
                .ToArray();

            Assert.DoesNotContain(
                references,
                reference =>
                    reference.Contains("Incursa.Quic.Http3", StringComparison.OrdinalIgnoreCase) ||
                    reference.Contains("Incursa.Http3", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Runner_project_does_not_reference_system_commandline()
    {
        var runnerProject = XDocument.Load(ProjectPath("src", "Incursa.ProtocolLab.Runner", "Incursa.ProtocolLab.Runner.csproj"));
        var references = runnerProject.Descendants()
            .Where(static element => element.Name.LocalName is "ProjectReference" or "PackageReference" or "Reference")
            .Select(element =>
                ((string?)element.Attribute("Include") ?? "") + " " +
                ((string?)element.Attribute("Update") ?? ""))
            .ToArray();

        Assert.DoesNotContain(references, reference => reference.Contains("System.CommandLine", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Runner_project_does_not_write_to_process_console()
    {
        var runnerDirectory = Path.Combine(TestPaths.RepoRoot, "src", "Incursa.ProtocolLab.Runner");
        var sourceFiles = Directory.EnumerateFiles(runnerDirectory, "*.cs", SearchOption.AllDirectories);

        foreach (var sourceFile in sourceFiles)
        {
            var source = File.ReadAllText(sourceFile);
            Assert.DoesNotContain("Console.Write", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Console.Error.Write", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Incursa_raw_quic_adapter_builds_the_raw_server_project()
    {
        var references = ProjectReferences("src", "Incursa.ProtocolLab.Adapters.IncursaRawQuic", "Incursa.ProtocolLab.Adapters.IncursaRawQuic.csproj");

        Assert.Contains("IncursaRawQuicServer.csproj", references);
    }

    [Fact]
    public void Cli_project_owns_runner_console_rendering()
    {
        Assert.True(
            File.Exists(ProjectPath("src", "Incursa.ProtocolLab.Cli", "RunnerConsoleRenderer.cs")),
            "CLI should own rendering structured runner messages to the process console.");
    }

    [Fact]
    public void Runner_project_has_no_linked_sources_from_cli_project()
    {
        var runnerProject = XDocument.Load(ProjectPath("src", "Incursa.ProtocolLab.Runner", "Incursa.ProtocolLab.Runner.csproj"));
        var linkedSources = runnerProject.Descendants()
            .Where(static element => element.Name.LocalName == "Compile")
            .Select(element => new
            {
                Include = (string?)element.Attribute("Include") ?? "",
                Link = (string?)element.Attribute("Link") ?? ""
            })
            .Where(entry =>
                entry.Include.Contains("Incursa.ProtocolLab.Cli", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(entry.Link))
            .ToArray();

        Assert.Empty(linkedSources);
    }

    [Fact]
    public void Cli_project_does_not_own_runner_workflow_classes()
    {
        var cliDirectory = Path.Combine(TestPaths.RepoRoot, "src", "Incursa.ProtocolLab.Cli");

        foreach (var file in RunnerOwnedFiles)
        {
            Assert.False(
                File.Exists(Path.Combine(cliDirectory, file)),
                $"{file} is runner-owned and should not live in the CLI project.");
        }
    }

    [Fact]
    public void Runner_owned_source_files_are_physically_under_runner_project()
    {
        var runnerDirectory = Path.Combine(TestPaths.RepoRoot, "src", "Incursa.ProtocolLab.Runner");

        foreach (var file in RunnerOwnedFiles)
        {
            Assert.True(
                File.Exists(Path.Combine(runnerDirectory, file)),
                $"{file} should physically live under the runner project.");
        }
    }

    [Fact]
    public void Runner_spec_artifacts_are_grouped_under_runner_spec_folders()
    {
        Assert.True(File.Exists(ProjectPath("specs", "requirements", "protocol-lab", "SPEC-PL-RUNNER.json")));
        Assert.True(File.Exists(ProjectPath("specs", "architecture", "protocol-lab", "runner", "ARC-PL-RUNNER.json")));
        Assert.True(File.Exists(ProjectPath("specs", "architecture", "protocol-lab", "runner", "ARC-PL-RUNNER-FIXTURES-0001.json")));
        Assert.True(File.Exists(ProjectPath("specs", "verification", "protocol-lab", "runner", "VER-PL-RUNNER-BASELINE.json")));
        Assert.True(File.Exists(ProjectPath("specs", "verification", "protocol-lab", "runner", "VER-PL-RUNNER-FIXTURES-0001.json")));

        Assert.False(File.Exists(ProjectPath("specs", "architecture", "protocol-lab", "ARC-PL-RUNNER.json")));
        Assert.False(File.Exists(ProjectPath("specs", "work-items", "protocol-lab", "runner", "WI-PL-RUNNER-EXTRACT.json")));
        Assert.False(File.Exists(ProjectPath("specs", "work-items", "protocol-lab", "runner", "WI-PL-RUNNER-GREEN.json")));
        Assert.False(File.Exists(ProjectPath("specs", "work-items", "protocol-lab", "runner", "WI-PL-RUNNER-FIXTURES-0001.json")));
        Assert.False(File.Exists(ProjectPath("specs", "verification", "protocol-lab", "VER-PL-RUNNER-BASELINE.json")));
    }

    private static string[] ProjectReferences(params string[] relativePath)
    {
        var project = XDocument.Load(ProjectPath(relativePath));
        return project.Descendants()
            .Where(static element => element.Name.LocalName == "ProjectReference")
            .Select(element => Path.GetFileName((string?)element.Attribute("Include") ?? ""))
            .Where(static fileName => !string.IsNullOrWhiteSpace(fileName))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ProjectPath(params string[] relativePath)
    {
        return Path.Combine([TestPaths.RepoRoot, .. relativePath]);
    }
}
