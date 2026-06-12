// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Adapter.Conformance;
using Incursa.ProtocolLab.Cli;
using Incursa.ProtocolLab.Model;
using System.Text.Json;

namespace Incursa.ProtocolLab.Tests;

public sealed class PublicConformanceHarnessTests
{
    [Theory]
    [InlineData("neutral-test-executor")]
    [InlineData("reference-http1-test-executor")]
    [InlineData("neutral-adapter-implementation")]
    [InlineData("reference-http1-implementation")]
    [InlineData("neutral-scenario-pack")]
    [InlineData("http1-core-scenario-pack")]
    [InlineData("neutral-toolchain")]
    public async Task Public_contract_package_fixtures_pass_package_v2_conformance(string fixtureName)
    {
        var validator = new PackageConformanceValidator();
        var report = await validator.ValidateAsync(
            Path.Combine(TestPaths.RepoRoot, "fixtures", "public-contracts", "packages", fixtureName),
            PackageOptions());

        Assert.Equal(PackageConformanceOutcome.Passed, report.Outcome);
        Assert.All(report.Steps, step => Assert.Equal(PackageConformanceOutcome.Passed, step.Outcome));
    }

    [Theory]
    [InlineData("implementation-missing-scenarios", "implementation-compatibility-metadata")]
    [InlineData("test-executor-missing-scenarios", "test-executor-compatibility-metadata")]
    [InlineData("scenario-pack-entry-mismatch", "scenario-id-match")]
    [InlineData("toolchain-missing-required-capabilities", "toolchain-capability-metadata")]
    public async Task Public_contract_invalid_package_fixtures_fail_package_v2_conformance(string fixtureName, string expectedStep)
    {
        var validator = new PackageConformanceValidator();
        var report = await validator.ValidateAsync(
            Path.Combine(TestPaths.RepoRoot, "fixtures", "public-contracts", "packages", "invalid", fixtureName),
            PackageOptions());

        Assert.Equal(PackageConformanceOutcome.Failed, report.Outcome);
        Assert.Contains(report.Steps, step =>
            step.Step == expectedStep &&
            step.Outcome == PackageConformanceOutcome.Failed);
    }

    [Theory]
    [InlineData("http1-core-smoke-reference.json")]
    [InlineData("http1-conformance-smoke-reference.json")]
    [InlineData("http1-benchmark-smoke-reference.json")]
    public async Task Public_contract_valid_run_plan_fixture_passes_run_plan_conformance_against_package_set(string runPlanFixture)
    {
        var validator = new RunPlanConformanceValidator();
        var report = await validator.ValidateAsync(
            Path.Combine(TestPaths.RepoRoot, "fixtures", "public-contracts", "run-plans", "valid", runPlanFixture),
            RunPlanOptions(
                "http1-core-scenario-pack",
                "reference-http1-test-executor",
                "reference-http1-implementation"));

        Assert.Equal(RunPlanConformanceOutcome.Passed, report.Outcome);
        Assert.All(report.Steps, step => Assert.Equal(RunPlanConformanceOutcome.Passed, step.Outcome));
    }

    [Fact]
    public async Task Public_contract_run_plan_conformance_rejects_unknown_selected_scenario()
    {
        var validator = new RunPlanConformanceValidator();
        var report = await validator.ValidateAsync(
            Path.Combine(TestPaths.RepoRoot, "fixtures", "public-contracts", "run-plans", "incompatible", "unknown-scenario-selection.json"),
            RunPlanOptions(
                "http1-core-scenario-pack",
                "reference-http1-test-executor",
                "reference-http1-implementation"));

        Assert.Equal(RunPlanConformanceOutcome.Failed, report.Outcome);
        Assert.Contains(report.Steps, step =>
            step.Step == "run-plan-selector-compatibility" &&
            step.Outcome == RunPlanConformanceOutcome.Failed &&
            step.Diagnostics is not null &&
            step.Diagnostics.Any(diagnostic => diagnostic.Contains("http.core.not-real", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Public_contract_run_plan_conformance_rejects_missing_resolved_package()
    {
        var validator = new RunPlanConformanceValidator();
        var report = await validator.ValidateAsync(
            Path.Combine(TestPaths.RepoRoot, "fixtures", "public-contracts", "run-plans", "valid", "http1-core-smoke-reference.json"),
            RunPlanOptions(
                "http1-core-scenario-pack",
                "reference-http1-test-executor"));

        Assert.Equal(RunPlanConformanceOutcome.Failed, report.Outcome);
        Assert.Contains(report.Steps, step =>
            step.Step == "run-plan-package-references" &&
            step.Outcome == RunPlanConformanceOutcome.Failed &&
            step.Diagnostics is not null &&
            step.Diagnostics.Any(diagnostic => diagnostic.Contains("kestrel-http1", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Http1_core_scenario_pack_declares_selectable_scenarios_without_executor_or_implementation_pins()
    {
        var packageRoot = Path.Combine(TestPaths.RepoRoot, "fixtures", "public-contracts", "packages", "http1-core-scenario-pack");
        var manifestJson = await File.ReadAllTextAsync(Path.Combine(packageRoot, "protocol-lab-package.json"));
        using var document = JsonDocument.Parse(manifestJson);
        var root = document.RootElement;

        Assert.Equal("scenario-pack", root.GetProperty("kind").GetString());
        Assert.Equal("protocol-lab-http1-core-scenarios", root.GetProperty("packageId").GetString());
        Assert.Equal(
            ["http.core.plaintext", "http.core.json", "http.payload.bytes.1kb"],
            root.GetProperty("providedScenarios").EnumerateArray()
                .Select(item => item.GetProperty("scenarioId").GetString()!)
                .ToArray());
        var suites = root.GetProperty("providedSuites").EnumerateArray().ToArray();
        Assert.Contains(suites, suite =>
            suite.GetProperty("suiteId").GetString() == "http1-core-smoke" &&
            suite.GetProperty("purpose").GetString() == "conformance" &&
            suite.GetProperty("resultKind").GetString() == "conformance" &&
            !suite.TryGetProperty("testExecutors", out _));
        Assert.Contains(suites, suite =>
            suite.GetProperty("suiteId").GetString() == "conformance-smoke" &&
            suite.GetProperty("purpose").GetString() == "conformance" &&
            suite.GetProperty("resultKind").GetString() == "conformance");
        Assert.Contains(suites, suite =>
            suite.GetProperty("suiteId").GetString() == "benchmark-smoke" &&
            suite.GetProperty("purpose").GetString() == "benchmark" &&
            suite.GetProperty("resultKind").GetString() == "benchmark");
        Assert.False(root.TryGetProperty("providedImplementations", out _));
        Assert.False(root.TryGetProperty("providedTestExecutors", out _));

        foreach (var entryManifest in root.GetProperty("entryManifests").EnumerateArray().Select(entry => entry.GetString()!))
        {
            if (!entryManifest.StartsWith("scenarios/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var scenario = YamlFile.Load<ScenarioDefinition>(Path.Combine(packageRoot, entryManifest));
            Assert.Equal("h1", scenario.Protocol);
            Assert.Equal([scenario.Id], root.GetProperty("providedScenarios").EnumerateArray()
                .Where(item => item.GetProperty("scenarioId").GetString() == scenario.Id)
                .Select(item => item.GetProperty("scenarioId").GetString()!)
                .ToArray());
        }
    }

    [Fact]
    public void Http1_core_scenario_pack_ids_match_public_scenario_specs_and_suite()
    {
        var packageRoot = Path.Combine(TestPaths.RepoRoot, "fixtures", "public-contracts", "packages", "http1-core-scenario-pack");
        var packageManifestJson = File.ReadAllText(Path.Combine(packageRoot, "protocol-lab-package.json"));
        using var document = JsonDocument.Parse(packageManifestJson);

        var providedScenarioIds = document.RootElement
            .GetProperty("providedScenarios")
            .EnumerateArray()
            .Select(item => item.GetProperty("scenarioId").GetString()!)
            .ToArray();
        var packagedScenarioIds = document.RootElement
            .GetProperty("entryManifests")
            .EnumerateArray()
            .Select(entry => entry.GetString()!)
            .Where(static entry => entry.StartsWith("scenarios/", StringComparison.OrdinalIgnoreCase))
            .Select(entry => YamlFile.Load<ScenarioDefinition>(Path.Combine(packageRoot, entry)).Id)
            .ToArray();
        var rootSuite = YamlFile.Load<SuiteDefinition>(Path.Combine(TestPaths.RepoRoot, "suites", "http1-core-smoke.yaml"));
        var packageSuite = YamlFile.Load<SuiteDefinition>(Path.Combine(packageRoot, "suites", "http1-core-smoke.yaml"));
        var conformanceSuite = YamlFile.Load<SuiteDefinition>(Path.Combine(packageRoot, "suites", "conformance-smoke.yaml"));
        var benchmarkSuite = YamlFile.Load<SuiteDefinition>(Path.Combine(packageRoot, "suites", "benchmark-smoke.yaml"));

        Assert.Equal(rootSuite.Scenarios, providedScenarioIds);
        Assert.Equal(rootSuite.Scenarios, packagedScenarioIds);
        Assert.Equal(rootSuite.Scenarios, packageSuite.Scenarios);
        Assert.Equal(rootSuite.Scenarios, conformanceSuite.Scenarios);
        Assert.Equal(rootSuite.Scenarios, benchmarkSuite.Scenarios);
        Assert.Equal("conformance", conformanceSuite.Purpose);
        Assert.Equal("conformance", conformanceSuite.ResultKind);
        Assert.Equal("benchmark", benchmarkSuite.Purpose);
        Assert.Equal("benchmark", benchmarkSuite.ResultKind);

        foreach (var scenarioId in packagedScenarioIds)
        {
            var publicScenario = ScenarioCatalog.Load(Path.Combine(TestPaths.RepoRoot, "scenarios"))
                .Single(scenario => scenario.Id == scenarioId);
            Assert.Equal("h1", publicScenario.Protocol);
        }
    }

    [Fact]
    public async Task Package_conformance_rejects_scenario_pack_entries_with_run_plan_semantics()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), $"protocol-lab-bad-scenario-pack-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(packageRoot, "scenarios"));
            await File.WriteAllTextAsync(Path.Combine(packageRoot, "protocol-lab-package.json"), """
            {
              "schemaVersion": "protocol-lab-package-v2",
              "packageId": "bad-scenario-pack",
              "packageVersion": "0.0.0-test",
              "kind": "scenario-pack",
              "entryManifests": ["scenarios/bad.yaml"],
              "providedScenarios": [
                {
                  "scenarioId": "http.core.bad",
                  "protocols": ["h1"]
                }
              ],
              "environments": [
                {
                  "os": "linux",
                  "arch": "x64",
                  "entrypoint": {
                    "kind": "bash",
                    "path": "scripts/noop.sh",
                    "arguments": [],
                    "workingDirectory": "."
                  }
                }
              ],
              "dependencies": {
                "requiresDotNet": false,
                "requiresDocker": false,
                "requiresPwsh": false,
                "requiresBash": true
              }
            }
            """);
            await File.WriteAllTextAsync(Path.Combine(packageRoot, "scenarios", "bad.yaml"), """
            schemaVersion: "1.0"
            id: http.core.bad
            title: Bad Scenario
            description: Invalid scenario that tries to smuggle run-plan selectors.
            status: draft
            kind: workload
            layer: application
            protocol: h1
            roles:
              - server
            requires:
              capabilities:
                - http.server
              protocols:
                - h1
              roles:
                - server
            trafficShape: request-response
            validation:
              required: true
              checks:
                - status
            implementationIds:
              - kestrel-http1
            testExecutorIds:
              - protocol-lab-http-smoke-executor
            loadProfileId: smoke
            """);

            var validator = new PackageConformanceValidator();
            var report = await validator.ValidateAsync(
                packageRoot,
                PackageOptions());

            Assert.Equal(PackageConformanceOutcome.Failed, report.Outcome);
            Assert.Contains(report.Steps, step =>
                step.Step == "scenario-no-run-plan-semantics" &&
                step.Outcome == PackageConformanceOutcome.Failed &&
                step.Diagnostics is not null &&
                step.Diagnostics.Any(diagnostic => diagnostic.Contains("implementationIds", StringComparison.OrdinalIgnoreCase)) &&
                step.Diagnostics.Any(diagnostic => diagnostic.Contains("testExecutorIds", StringComparison.OrdinalIgnoreCase)) &&
                step.Diagnostics.Any(diagnostic => diagnostic.Contains("loadProfileId", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            if (Directory.Exists(packageRoot))
            {
                Directory.Delete(packageRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Cli_package_conformance_command_accepts_neutral_test_executor_fixture()
    {
        var exitCode = await ProtocolLabCommand.RunAsync(
        [
            "conformance",
            "package",
            "--package",
            Path.Combine(TestPaths.RepoRoot, "fixtures", "public-contracts", "packages", "neutral-test-executor"),
            "--root",
            TestPaths.RepoRoot
        ]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Public_docs_show_local_third_party_conformance_commands()
    {
        var quickstart = Read("docs", "quickstart.md");
        var adapterConformance = Read("docs", "runner", "adapter-conformance.md");
        var testExecutorConformance = Read("docs", "runner", "test-executor-conformance.md");
        var packageV2 = Read("docs", "lab", "package-v2.md");
        var fixtureReadme = Read("fixtures", "public-contracts", "README.md");

        Assert.Contains("conformance package --package", quickstart, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("conformance adapter --base-url", adapterConformance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("conformance test-executor --base-url", testExecutorConformance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("neutral-test-executor", packageV2, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("http1-core-scenario-pack", packageV2, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("conformance run-plan", packageV2, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--run-plan fixtures", packageV2, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("neutral-adapter-implementation", fixtureReadme, StringComparison.OrdinalIgnoreCase);
    }

    private static PackageConformanceOptions PackageOptions()
    {
        return new PackageConformanceOptions
        {
            PackageSchemaPath = Path.Combine(TestPaths.RepoRoot, "schemas", "package", "v2", "package.schema.json"),
            TestExecutorSchemaRootPath = Path.Combine(TestPaths.RepoRoot, "schemas", "test-executor", "v1"),
            ScenarioSchemaPath = Path.Combine(TestPaths.RepoRoot, "schemas", "scenario.schema.json")
        };
    }

    private static RunPlanConformanceOptions RunPlanOptions(params string[] packageFixtureNames)
    {
        return new RunPlanConformanceOptions
        {
            RunPlanSchemaPath = Path.Combine(TestPaths.RepoRoot, "schemas", "run-plan", "v1", "run-plan.schema.json"),
            PackageSchemaPath = Path.Combine(TestPaths.RepoRoot, "schemas", "package", "v2", "package.schema.json"),
            TestExecutorSchemaRootPath = Path.Combine(TestPaths.RepoRoot, "schemas", "test-executor", "v1"),
            ScenarioSchemaPath = Path.Combine(TestPaths.RepoRoot, "schemas", "scenario.schema.json"),
            PackagePaths = packageFixtureNames
                .Select(name => Path.Combine(TestPaths.RepoRoot, "fixtures", "public-contracts", "packages", name))
                .ToArray()
        };
    }

    private static string Read(params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine([TestPaths.RepoRoot, .. pathParts]));
    }
}
