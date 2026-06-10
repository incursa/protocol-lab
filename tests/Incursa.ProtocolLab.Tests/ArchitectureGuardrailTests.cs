// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Xml.Linq;
using Incursa.ProtocolLab.Model;

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

        Assert.Equal(["Incursa.ProtocolLab.Adapter.Conformance.csproj", "Incursa.ProtocolLab.Runner.csproj"], cliReferences);
        Assert.Equal(["Incursa.ProtocolLab.Adapter.Contracts.csproj", "Incursa.ProtocolLab.Model.csproj"], runnerReferences);
        Assert.Empty(modelReferences);
    }

    [Fact]
    public void Runner_and_cli_do_not_reference_adapter_implementation_assemblies()
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
                    reference.Contains("Incursa.ProtocolLab.Adapters.", StringComparison.OrdinalIgnoreCase) ||
                    reference.Contains("Incursa.ProtocolLab.Servers.", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Runner_and_cli_do_not_reference_concrete_protocol_implementation_libraries()
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
                    reference.Contains("Incursa.Qpack", StringComparison.OrdinalIgnoreCase) ||
                    reference.Contains("Incursa.Quic", StringComparison.OrdinalIgnoreCase));
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
    public void Public_solution_does_not_include_concrete_adapter_or_server_projects()
    {
        var solution = File.ReadAllText(ProjectPath("Incursa.ProtocolLab.sln"));

        Assert.DoesNotContain("Incursa.ProtocolLab.Adapters.Kestrel", solution, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Incursa.ProtocolLab.Adapters.IncursaHttp3", solution, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Incursa.ProtocolLab.Adapters.IncursaRawQuic", solution, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Incursa.ProtocolLab.Adapters.MsQuicDotNet", solution, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("KestrelBenchServer", solution, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IncursaRawQuicServer", solution, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Public_source_tree_does_not_carry_concrete_dotnet_adapter_projects()
    {
        var sourceRoot = ProjectPath("src");
        var concreteAdapterProjects = Directory
            .EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Incursa.ProtocolLab.Adapters.", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(TestPaths.RepoRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(concreteAdapterProjects);
    }

    [Fact]
    public void Public_repo_does_not_carry_quic_dotnet_package_templates()
    {
        var templateRoot = ProjectPath("templates", "lab", "quic-dotnet");
        if (!Directory.Exists(templateRoot))
        {
            return;
        }

        var files = Directory
            .EnumerateFiles(templateRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(TestPaths.RepoRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(files);
    }

    [Fact]
    public void Public_implementation_manifest_local_paths_resolve_inside_public_repo()
    {
        var implementationRoot = ProjectPath("implementations");
        var manifests = Directory
            .EnumerateFiles(implementationRoot, "*.yaml", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(path => new
            {
                Path = path,
                Manifest = YamlFile.Load<ImplementationManifest>(path)
            })
            .ToArray();

        Assert.NotEmpty(manifests);

        foreach (var entry in manifests)
        {
            AssertManifestFilePath(entry.Path, entry.Manifest.Project, "project");
            AssertManifestDirectoryPath(entry.Path, entry.Manifest.WorkingDirectory, "workingDirectory");
            AssertManifestFilePath(entry.Path, entry.Manifest.Dockerfile, "dockerfile");
            AssertManifestDirectoryPath(entry.Path, entry.Manifest.BuildContext, "buildContext");
        }
    }

    [Fact]
    public void Public_boundary_docs_pin_contract_first_roles()
    {
        var labRoles = File.ReadAllText(ProjectPath("docs", "architecture", "lab-roles.md"));
        var packageV2 = File.ReadAllText(ProjectPath("docs", "lab", "package-v2.md"));
        var testExecutorContract = File.ReadAllText(ProjectPath("docs", "architecture", "test-executor-contract-v1.md"));
        var productBoundaries = File.ReadAllText(ProjectPath("docs", "protocol-lab", "product-boundaries.md"));

        Assert.Contains("does not need to own production implementations, production test executors, or hosted worker infrastructure", labRoles, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ProtocolLab must not silently choose a different implementation, test executor, or protocol lane", labRoles, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`test-executor` packages use `test-executors/`", packageV2, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("load-runner", packageV2, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/protocol-lab/test-executor/v1", testExecutorContract, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Public must not depend on internal", productBoundaries, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Contract-first integration", productBoundaries, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Public_entry_docs_do_not_advertise_removed_production_adapter_workflows()
    {
        var entryDocs = new[]
        {
            ProjectPath("README.md"),
            ProjectPath("docs", "README.md"),
            ProjectPath("docs", "quickstart.md"),
            ProjectPath("docs", "architecture.md"),
            ProjectPath("docs", "v1-definition-of-done.md"),
            ProjectPath("docs", "vision.md"),
            ProjectPath("docs", "runner", "fixture-lab.md"),
            ProjectPath("docs", "spec", "raw-quic-load-generator-contract.md"),
            ProjectPath("docs", "protocol-lab", "vision.md"),
            ProjectPath("docs", "protocol-lab", "public-seed-readiness.md"),
            ProjectPath("docs", "protocol-lab", "public-repo-readiness.md")
        };
        var removedWorkflowMarkers = new[]
        {
            "Build-IncursaHttp3BenchServerImage",
            "Build-KestrelBenchServerImage",
            "repo-owned Incursa HTTP/3 adapter",
            "src\\Incursa.ProtocolLab.Adapters.IncursaHttp3",
            "src/Incursa.ProtocolLab.Adapters.IncursaHttp3",
            "docs/runner/kestrel-adapter.md",
            "docs\\runner\\kestrel-adapter.md",
            "Kestrel Adapter v1"
        };

        foreach (var entryDoc in entryDocs)
        {
            var text = File.ReadAllText(entryDoc);
            foreach (var marker in removedWorkflowMarkers)
            {
                Assert.DoesNotContain(marker, text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Public_raw_quic_contract_surfaces_do_not_depend_on_adapter_source_layout()
    {
        var contractFiles = new[]
        {
            ProjectPath("docs", "spec", "raw-quic-load-generator-contract.md"),
            ProjectPath("load-tools", "quic-go-raw-load.yaml"),
            ProjectPath("scripts", "lab", "New-ProtocolLabRawQuicComponentPackages.ps1")
        };
        var forbiddenMarkers = new[]
        {
            "src/Incursa.ProtocolLab.Adapters.QuicGo",
            "src\\Incursa.ProtocolLab.Adapters.QuicGo",
            "Incursa.ProtocolLab.Adapters.QuicGo/cmd/quic-go-raw-load",
            "Incursa.ProtocolLab.Adapters.QuicGo\\cmd\\quic-go-raw-load"
        };

        foreach (var file in contractFiles)
        {
            var text = File.ReadAllText(file);
            foreach (var marker in forbiddenMarkers)
            {
                Assert.DoesNotContain(marker, text, StringComparison.OrdinalIgnoreCase);
            }
        }

        var packageBuilder = File.ReadAllText(ProjectPath("scripts", "lab", "New-ProtocolLabRawQuicComponentPackages.ps1"));
        Assert.Contains("tools/test-executors/quic-go-raw-load", packageBuilder, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("source/go.mod", packageBuilder, StringComparison.OrdinalIgnoreCase);
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

    private static void AssertManifestFilePath(string manifestPath, string value, string propertyName)
    {
        AssertManifestPath(manifestPath, value, propertyName, File.Exists);
    }

    private static void AssertManifestDirectoryPath(string manifestPath, string value, string propertyName)
    {
        AssertManifestPath(manifestPath, value, propertyName, Directory.Exists);
    }

    private static void AssertManifestPath(string manifestPath, string value, string propertyName, Func<string, bool> exists)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        Assert.False(Path.IsPathRooted(value), $"{manifestPath} {propertyName} must be package/repo relative: {value}");
        Assert.False(IsUriPath(value), $"{manifestPath} {propertyName} must not be a URI: {value}");
        Assert.DoesNotContain(
            value.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries),
            segment => segment == "..");

        var fullPath = Path.GetFullPath(Path.Combine(TestPaths.RepoRoot, value));
        var repoRoot = Path.GetFullPath(TestPaths.RepoRoot);
        Assert.True(
            fullPath.Equals(repoRoot, StringComparison.OrdinalIgnoreCase) ||
            fullPath.StartsWith(repoRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            $"{manifestPath} {propertyName} must stay inside repo root: {value}");
        Assert.True(exists(fullPath), $"{manifestPath} {propertyName} path does not exist: {value}");
    }

    private static bool IsUriPath(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            !string.IsNullOrWhiteSpace(uri.Scheme);
    }

    private static string ProjectPath(params string[] relativePath)
    {
        return Path.Combine([TestPaths.RepoRoot, .. relativePath]);
    }
}
