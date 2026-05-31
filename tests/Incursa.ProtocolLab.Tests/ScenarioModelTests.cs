// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Model;
using Incursa.ProtocolLab.Runner;

namespace Incursa.ProtocolLab.Tests;

public sealed class ScenarioModelTests
{
    [Fact]
    public void Validates_missing_required_fields()
    {
        var scenario = new ScenarioDefinition();
        var errors = ScenarioValidator.Validate(scenario);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Field == nameof(scenario.Id));
        Assert.Contains(errors, e => e.Field == nameof(scenario.Title));
        Assert.Contains(errors, e => e.Field == nameof(scenario.SchemaVersion));
        Assert.Contains(errors, e => e.Field == nameof(scenario.Description));
    }

    [Fact]
    public void Rejects_invalid_scenario_id_format()
    {
        var scenario = new ScenarioDefinition
        {
            Id = "noperiods",
            SchemaVersion = "1.0",
            Title = "Test",
            Description = "Test scenario",
            Protocol = "h3",
            Roles = ["server"],
            ImplementationRole = "server"
        };

        var errors = ScenarioValidator.Validate(scenario);

        Assert.Contains(errors, e => e.Field == "id" && e.Message.Contains("dotted format"));
    }

    [Fact]
    public void Rejects_implementation_specific_scenario_id()
    {
        var scenario = new ScenarioDefinition
        {
            Id = "kestrel.plaintext",
            SchemaVersion = "1.0",
            Title = "Test",
            Description = "Test scenario",
            Protocol = "h3",
            Roles = ["server"],
            ImplementationRole = "server"
        };

        var errors = ScenarioValidator.Validate(scenario);

        Assert.Contains(errors, e => e.Field == "id" && e.Message.Contains("implementation-specific"));
    }

    [Fact]
    public void Rejects_msquic_in_scenario_id()
    {
        var scenario = new ScenarioDefinition
        {
            Id = "quic.msquic.stream",
            SchemaVersion = "1.0",
            Title = "Test",
            Description = "Test scenario",
            Protocol = "quic",
            Roles = ["server"],
            ImplementationRole = "server"
        };

        var errors = ScenarioValidator.Validate(scenario);

        Assert.Contains(errors, e => e.Field == "id" && e.Message.Contains("implementation-specific"));
    }

    [Fact]
    public void Rejects_docker_in_scenario_id()
    {
        var scenario = new ScenarioDefinition
        {
            Id = "http.docker.test",
            SchemaVersion = "1.0",
            Title = "Test",
            Description = "Test scenario",
            Protocol = "h3",
            Roles = ["server"],
            ImplementationRole = "server"
        };

        var errors = ScenarioValidator.Validate(scenario);

        Assert.Contains(errors, e => e.Field == "id" && e.Message.Contains("implementation-specific"));
    }

    [Fact]
    public void Accepts_valid_scenario_id()
    {
        var scenario = new ScenarioDefinition
        {
            Id = "http.plaintext",
            SchemaVersion = "1.0",
            Title = "Test",
            Description = "Test scenario",
            Protocol = "h3",
            Roles = ["server"],
            ImplementationRole = "server",
            Validation = new ValidationRules { Required = true, Checks = ["status"] }
        };

        var errors = ScenarioValidator.Validate(scenario);

        Assert.DoesNotContain(errors, e => e.Field == "id");
    }

    [Fact]
    public void Validates_invalid_status_value()
    {
        var scenario = new ScenarioDefinition
        {
            Id = "http.test",
            SchemaVersion = "1.0",
            Title = "Test",
            Description = "Test scenario",
            Protocol = "h3",
            Status = "not-real",
            Roles = ["server"],
            ImplementationRole = "server"
        };

        var errors = ScenarioValidator.Validate(scenario);

        Assert.Contains(errors, e => e.Field == nameof(scenario.Status));
    }

    [Fact]
    public void Validates_invalid_traffic_shape()
    {
        var scenario = new ScenarioDefinition
        {
            Id = "http.test",
            SchemaVersion = "1.0",
            Title = "Test",
            Description = "Test scenario",
            Protocol = "h3",
            Status = "stable",
            TrafficShape = "garbage",
            Roles = ["server"],
            ImplementationRole = "server"
        };

        var errors = ScenarioValidator.Validate(scenario);

        Assert.Contains(errors, e => e.Field == nameof(scenario.TrafficShape));
    }

    [Fact]
    public void All_catalog_scenarios_are_valid()
    {
        var scenarios = ScenarioCatalog.Load(Path.Combine(TestPaths.RepoRoot, "scenarios"));

        Assert.NotEmpty(scenarios);
        foreach (var scenario in scenarios)
        {
            var errors = ScenarioValidator.Validate(scenario);
            if (errors.Count > 0)
            {
                Assert.Fail($"Scenario '{scenario.Id}' has validation errors: {string.Join("; ", errors)}");
            }
        }
    }

    [Fact]
    public void Compatibility_resolver_returns_supported_for_valid_cell()
    {
        var manifest = new ImplementationManifest
        {
            Id = "test",
            Roles = ["server"],
            SupportedProtocols = ["h3"],
            SupportedWorkloadFamilies = ["http.application"],
            Capabilities = ["httpPlaintext"]
        };
        var scenario = new ScenarioDefinition
        {
            Id = "http.core.plaintext",
            Status = "stable",
            Family = "http.application",
            Protocol = "h3",
            ImplementationRole = "server",
            RequiredCapabilities = ["httpPlaintext"]
        };
        var cell = new RunCell(manifest, scenario, "h3", 1, 1, 1, 30, 5, "clean");
        var loadTools = new List<LoadToolManifest>
        {
            new() { Id = "h2load", SupportedProtocols = ["h3"], SupportedScenarioFamilies = ["http.application"] }
        };

        var result = CompatibilityClassifier.Classify(cell, loadTools);

        Assert.Equal(RunCellCompatibilityStatuses.Supported, result.Status);
        Assert.True(result.CanRun);
    }

    [Fact]
    public void Compatibility_resolver_returns_missing_capability()
    {
        var manifest = new ImplementationManifest
        {
            Id = "test",
            Roles = ["server"],
            SupportedProtocols = ["h3"],
            SupportedWorkloadFamilies = ["http.application"],
            Capabilities = []
        };
        var scenario = new ScenarioDefinition
        {
            Id = "http.core.plaintext",
            Status = "stable",
            Family = "http.application",
            Protocol = "h3",
            ImplementationRole = "server",
            RequiredCapabilities = ["httpPlaintext"]
        };
        var cell = new RunCell(manifest, scenario, "h3", 1, 1, 1, 30, 5, "clean");

        var result = CompatibilityClassifier.Classify(cell);

        Assert.Equal(RunCellCompatibilityStatuses.MissingCapability, result.Status);
        Assert.False(result.CanRun);
    }

    [Fact]
    public void Compatibility_resolver_returns_missing_load_tool()
    {
        var manifest = new ImplementationManifest
        {
            Id = "test",
            Roles = ["server"],
            SupportedProtocols = ["h3"],
            SupportedWorkloadFamilies = ["http.application"],
            Capabilities = ["httpPlaintext"]
        };
        var scenario = new ScenarioDefinition
        {
            Id = "http.core.plaintext",
            Status = "stable",
            Family = "http.application",
            Protocol = "h3",
            ImplementationRole = "server",
            RequiredCapabilities = ["httpPlaintext"]
        };
        var cell = new RunCell(manifest, scenario, "h3", 1, 1, 1, 30, 5, "clean");
        var loadTools = new List<LoadToolManifest>
        {
            new() { Id = "h2load", SupportedProtocols = ["quic"], SupportedScenarioFamilies = ["quic.transport"] }
        };

        var result = CompatibilityClassifier.Classify(cell, loadTools);

        Assert.Equal(RunCellCompatibilityStatuses.MissingLoadTool, result.Status);
        Assert.False(result.CanRun);
    }

    [Fact]
    public void Compatibility_resolver_blocks_placeholder_scenario()
    {
        var manifest = new ImplementationManifest
        {
            Id = "test",
            Roles = ["server"],
            SupportedProtocols = ["h3"],
            SupportedWorkloadFamilies = ["webtransport"],
            Capabilities = ["webtransport"]
        };
        var scenario = new ScenarioDefinition
        {
            Id = "webtransport.session-bidi-echo",
            Status = "placeholder",
            Family = "webtransport",
            Protocol = "h3",
            ImplementationRole = "server",
            RequiredCapabilities = ["webtransport"]
        };
        var cell = new RunCell(manifest, scenario, "h3", 1, 1, 1, 30, 5, "clean");

        var result = CompatibilityClassifier.Classify(cell);

        Assert.Equal(RunCellCompatibilityStatuses.PlaceholderNotRunnable, result.Status);
        Assert.False(result.CanRun);
    }

    [Fact]
    public void Compatibility_resolver_blocks_experimental_scenario_without_opt_in()
    {
        var manifest = new ImplementationManifest
        {
            Id = "test",
            Roles = ["server"],
            SupportedProtocols = ["h3"],
            SupportedWorkloadFamilies = ["h3.protocol"],
            Capabilities = ["h3Protocol"]
        };
        var scenario = new ScenarioDefinition
        {
            Id = "http3.settings",
            Status = "experimental",
            Family = "h3.protocol",
            Protocol = "h3",
            ImplementationRole = "server",
            RequiredCapabilities = ["h3Protocol"]
        };
        var cell = new RunCell(manifest, scenario, "h3", 1, 1, 1, 30, 5, "clean");

        var result = CompatibilityClassifier.Classify(cell);

        Assert.Equal(RunCellCompatibilityStatuses.ExperimentalNotEnabled, result.Status);
        Assert.False(result.CanRun);
    }

    [Fact]
    public void Compatibility_resolver_allows_experimental_with_opt_in()
    {
        var manifest = new ImplementationManifest
        {
            Id = "test",
            Roles = ["server"],
            SupportedProtocols = ["h3"],
            SupportedWorkloadFamilies = ["h3.protocol"],
            Capabilities = ["h3Protocol"]
        };
        var scenario = new ScenarioDefinition
        {
            Id = "http3.settings",
            Status = "experimental",
            Family = "h3.protocol",
            Protocol = "h3",
            ImplementationRole = "server",
            RequiredCapabilities = ["h3Protocol"]
        };
        var cell = new RunCell(manifest, scenario, "h3", 1, 1, 1, 30, 5, "clean");
        var loadTools = new List<LoadToolManifest>
        {
            new() { Id = "h2load", SupportedProtocols = ["h3"], SupportedScenarioFamilies = ["h3.protocol"] }
        };

        var result = CompatibilityClassifier.Classify(cell, loadTools, allowExperimental: true);

        Assert.True(result.CanRun);
    }

    [Fact]
    public void Placeholder_scenario_remains_blocked_even_with_opt_in()
    {
        var manifest = new ImplementationManifest
        {
            Id = "test",
            Roles = ["server"],
            SupportedProtocols = ["ws"],
            SupportedWorkloadFamilies = ["websocket"],
            Capabilities = ["websocket.server"]
        };
        var scenario = new ScenarioDefinition
        {
            Id = "websocket.echo",
            Status = "placeholder",
            Family = "websocket",
            Protocol = "ws",
            ImplementationRole = "server",
            RequiredCapabilities = ["websocket.server"]
        };
        var cell = new RunCell(manifest, scenario, "ws", 1, 1, 1, 30, 5, "clean");

        var result = CompatibilityClassifier.Classify(cell, allowPlaceholder: true);

        Assert.Equal(RunCellCompatibilityStatuses.PlaceholderNotRunnable, result.Status);
        Assert.False(result.CanRun);
    }
}
