// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Model;
using Incursa.ProtocolLab.Runner;

namespace Incursa.ProtocolLab.Tests;

public sealed class LoadToolContractV1Tests
{
    // ── Manifest Validation ──────────────────────────────────────────────────

    [Fact]
    public void Parses_h2load_v1_manifest()
    {
        var tool = LoadYaml("h2load.yaml");

        Assert.Equal("protocol-lab.load-tool.v1", tool.SchemaVersion);
        Assert.Equal("h2load", tool.Id);
        Assert.Equal("h2load HTTP/3", tool.Title);
        Assert.Equal("process", tool.Kind);
        Assert.Equal("external-reference", tool.Category);
        Assert.NotNull(tool.Supports);
        Assert.Contains("h3", tool.GetEffectiveProtocols());
        Assert.Contains("http.application", tool.SupportedScenarioFamilies);
        Assert.Contains("request-response", tool.GetEffectiveTrafficShapes());
        Assert.Contains("download", tool.GetEffectiveTrafficShapes());
        Assert.Contains("client", tool.GetEffectiveRoles());
        Assert.Contains("requestsPerSecond", tool.GetEffectivePrimaryMetrics());
        Assert.Contains("latencyP50", tool.GetEffectivePrimaryMetrics());
        Assert.Contains("latencyP95", tool.GetEffectivePrimaryMetrics());
        Assert.Contains("throughputBytesPerSecond", tool.GetEffectivePrimaryMetrics());
        Assert.Contains("bytesWritten", tool.GetEffectiveSecondaryMetrics());
        Assert.Equal("h2load", tool.GetEffectiveParserType());
        Assert.True(tool.PreservesRawOutput);
        Assert.Contains("load.stdout.log", tool.GetEffectiveRequiredArtifacts());
        Assert.Contains("load.stderr.log", tool.GetEffectiveRequiredArtifacts());
        Assert.Contains("load.metrics.json", tool.GetEffectiveOptionalArtifacts());
        Assert.Contains("validation", tool.Purposes);
        Assert.Contains("benchmark", tool.Purposes);
        Assert.NotEmpty(tool.Limitations);
        Assert.Equal("incursa/protocol-lab-h2load-http3:local", tool.DockerImage);
        Assert.Equal("h2load", tool.DockerCommand);
        Assert.Equal("host.docker.internal", tool.DockerHostRewrite);
        Assert.Equal("localhost", tool.Sni);
    }

    [Fact]
    public void Parses_managed_httpclient_v1_manifest()
    {
        var tool = LoadYaml("managed-httpclient-h3-load.yaml");

        Assert.Equal("protocol-lab.load-tool.v1", tool.SchemaVersion);
        Assert.Equal("managed-httpclient-h3-load", tool.Id);
        Assert.Equal("managed", tool.Kind);
        Assert.Equal("managed-lab", tool.Category);
        Assert.Contains("h3", tool.GetEffectiveProtocols());
        Assert.Contains("request-response", tool.GetEffectiveTrafficShapes());
        Assert.Contains("client", tool.GetEffectiveRoles());
        Assert.Contains("responseVersionFailures", tool.GetEffectiveSecondaryMetrics());
        Assert.Equal("managed-httpclient-h3-json", tool.GetEffectiveParserType());
        Assert.True(tool.PreservesRawOutput);
    }

    [Fact]
    public void Parses_oha_v1_manifest()
    {
        var tool = LoadYaml("oha.yaml");

        Assert.Equal("protocol-lab.load-tool.v1", tool.SchemaVersion);
        Assert.Equal("oha", tool.Id);
        Assert.Equal("process", tool.Kind);
        Assert.Contains("h1", tool.GetEffectiveProtocols());
        Assert.Contains("h2", tool.GetEffectiveProtocols());
        Assert.DoesNotContain("h3", tool.GetEffectiveProtocols());
        Assert.Contains("request-response", tool.GetEffectiveTrafficShapes());
        Assert.Contains("benchmark", tool.Purposes);
        Assert.Contains("profile", tool.Purposes);
        Assert.DoesNotContain("validation", tool.Purposes);
        Assert.Equal("oha-json", tool.GetEffectiveParserType());
    }

    [Fact]
    public void All_load_tool_manifests_parse_successfully()
    {
        var tools = LoadToolCatalog.Load(Path.Combine(TestPaths.RepoRoot, "load-tools"));

        Assert.Contains(tools, t => t.Id == "h2load");
        Assert.Contains(tools, t => t.Id == "managed-httpclient-h3-load");
        Assert.Contains(tools, t => t.Id == "oha");

        foreach (var tool in tools)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Id), $"Tool {tool.Name} has empty Id");
            Assert.NotEmpty(tool.GetEffectiveProtocols());
        }
    }

    [Fact]
    public void All_load_tool_manifests_have_v1_schema_version()
    {
        var tools = LoadToolCatalog.Load(Path.Combine(TestPaths.RepoRoot, "load-tools"));

        foreach (var tool in tools)
        {
            Assert.Equal("protocol-lab.load-tool.v1", tool.SchemaVersion);
        }
    }

    // ── Compatibility Matching ───────────────────────────────────────────────

    [Fact]
    public void H2load_supports_h3_request_response_traffic_shape()
    {
        var h2load = LoadYaml("h2load.yaml");

        Assert.True(h2load.SupportsProtocol("h3"));
        Assert.True(h2load.SupportsTrafficShape("request-response"));
        Assert.True(h2load.SupportsTrafficShape("download"));
        Assert.True(h2load.SupportsRole("client"));
    }

    [Fact]
    public void H2load_does_not_support_unknown_traffic_shape()
    {
        var h2load = LoadYaml("h2load.yaml");

        Assert.False(h2load.SupportsTrafficShape("datagram"));
        Assert.False(h2load.SupportsTrafficShape("bidirectionalStream"));
    }

    [Fact]
    public void Load_tool_with_empty_traffic_shapes_accepts_all()
    {
        var tool = new LoadToolManifest
        {
            Id = "generic",
            SupportedProtocols = ["h1"],
            SupportedScenarioFamilies = ["http.application"]
        };

        Assert.True(tool.SupportsTrafficShape("request-response"));
        Assert.True(tool.SupportsTrafficShape("datagram"));
        Assert.True(tool.SupportsTrafficShape("anything"));
    }

    [Fact]
    public void Load_tool_with_empty_roles_accepts_all()
    {
        var tool = new LoadToolManifest
        {
            Id = "generic",
            SupportedProtocols = ["h1"],
            SupportedScenarioFamilies = ["http.application"]
        };

        Assert.True(tool.SupportsRole("client"));
        Assert.True(tool.SupportsRole("server"));
        Assert.True(tool.SupportsRole("proxy"));
    }

    [Fact]
    public void Traffic_shape_compatibility_uses_classifier()
    {
        var tools = new List<LoadToolManifest>
        {
            new()
            {
                Id = "h2load",
                SupportedProtocols = ["h3"],
                SupportedScenarioFamilies = ["http.application"],
                SupportedTrafficShapes = ["request-response", "download"],
                SupportedRoles = ["client"]
            }
        };

        var requestResponseCell = NewCell(protocol: "h3", trafficShape: "request-response");
        var downloadCell = NewCell(protocol: "h3", trafficShape: "download");
        var datagramCell = NewCell(protocol: "h3", trafficShape: "datagram");

        var requestResponseCompat = CompatibilityClassifier.Classify(requestResponseCell, tools);
        var downloadCompat = CompatibilityClassifier.Classify(downloadCell, tools);
        var datagramCompat = CompatibilityClassifier.Classify(datagramCell, tools);

        Assert.True(requestResponseCompat.CanRun);
        Assert.True(downloadCompat.CanRun);
        Assert.False(datagramCompat.CanRun);
        Assert.Equal("incompatible-traffic-shape", datagramCompat.Status);
    }

    [Fact]
    public void Compatibility_classifier_reports_incompatible_traffic_shape_for_requested_tool()
    {
        var tools = new List<LoadToolManifest>
        {
            new()
            {
                Id = "h2load",
                SupportedProtocols = ["h3"],
                SupportedScenarioFamilies = ["http.application"],
                SupportedTrafficShapes = ["request-response"],
                SupportedRoles = ["client"]
            }
        };

        var cell = NewCell(protocol: "h3", trafficShape: "datagram");
        var compat = CompatibilityClassifier.Classify(cell, tools, requestedLoadTool: "h2load");

        Assert.False(compat.CanRun);
        Assert.Equal("incompatible-traffic-shape", compat.Status);
    }

    [Fact]
    public void Load_tool_resolver_filters_by_traffic_shape()
    {
        var h2load = new LoadToolManifest
        {
            Id = "h2load",
            Kind = LoadToolKinds.Docker,
            SupportedProtocols = ["h3"],
            SupportedScenarioFamilies = ["http.application"],
            SupportedTrafficShapes = ["request-response"],
            DockerImage = "example/h2load:latest"
        };

        var compatibleCell = NewCell(protocol: "h3", family: "http.application", trafficShape: "request-response");
        var incompatibleCell = NewCell(protocol: "h3", family: "http.application", trafficShape: "datagram");

        var compatibleResolution = LoadToolInvoker.Resolve([h2load], compatibleCell, requestedTool: null, requestedMode: null);
        var incompatibleResolution = LoadToolInvoker.Resolve([h2load], incompatibleCell, requestedTool: null, requestedMode: null);

        Assert.True(compatibleResolution.CanExecute, $"Expected compatible but got {compatibleResolution.Result.Status}: {string.Join(", ", compatibleResolution.Result.Errors)}");
        Assert.False(incompatibleResolution.CanExecute, $"Expected incompatible but got {incompatibleResolution.Result.Status}");
    }

    // ── Parser Failure and Raw Output Preservation ──────────────────────────

    [Fact]
    public void Parser_failure_returns_metrics_unavailable_and_warning()
    {
        var manifest = new LoadToolManifest
        {
            Id = "h2load",
            OutputParserId = "h2load"
        };

        var parsed = LoadToolInvoker.Parse(manifest, "unparseable garbage output", "some stderr");

        Assert.False(parsed.ParsedMetricsAvailable);
        Assert.Contains(parsed.Warnings, w => w.Contains("no metrics were parsed"));
    }

    [Fact]
    public void Parser_failure_does_not_throw()
    {
        var manifest = new LoadToolManifest
        {
            Id = "unknown-tool",
            ParserType = "nonexistent-parser"
        };

        var exception = Record.Exception(() =>
            LoadToolInvoker.Parse(manifest, "some output", "some stderr"));

        Assert.Null(exception);
    }

    [Fact]
    public void Parser_with_no_implementation_reports_no_parser_message()
    {
        var manifest = new LoadToolManifest
        {
            Id = "custom-tool",
            ParserType = "custom-parser"
        };

        var parsed = LoadToolInvoker.Parse(manifest, "output", "stderr");

        Assert.False(parsed.ParsedMetricsAvailable);
        Assert.Contains(parsed.Warnings, w => w.Contains("No parser is implemented"));
    }

    [Fact]
    public void Parser_type_falls_back_to_output_parser_id()
    {
        var manifest = new LoadToolManifest
        {
            Id = "oha",
            OutputParserId = "oha-json"
        };

        var output = """{"summary":{"requestsPerSec":100}}""";
        var parsed = LoadToolInvoker.Parse(manifest, output, "");

        Assert.True(parsed.ParsedMetricsAvailable);
    }

    // ── LoadToolDefinition / ToManifest Conversion ──────────────────────────

    [Fact]
    public void LoadToolDefinition_converts_to_manifest()
    {
        var def = new LoadToolDefinition
        {
            SchemaVersion = "protocol-lab.load-tool.v1",
            Id = "test-tool",
            Title = "Test Tool",
            Kind = LoadToolKind.Process,
            Supports = new LoadToolSupport
            {
                Protocols = ["h1", "h2"],
                TrafficShapes = ["request-response"],
                Roles = ["client"]
            },
            Metrics = new LoadToolMetrics
            {
                Primary = ["requestsPerSecond", "latencyP50"],
                Secondary = ["bytesRead"]
            },
            Parser = new LoadToolParser
            {
                Type = "test-parser",
                PreservesRawOutput = true
            },
            Execution = new LoadToolExecution
            {
                Process = new LoadToolProcessExecution
                {
                    Executable = "test-tool",
                    DefaultArguments = ["--verbose"],
                    VersionCommand = ["--version"],
                    AvailabilityCheck = "path"
                }
            },
            Artifacts = new LoadToolArtifacts
            {
                Required = ["load.stdout.log"],
                Optional = ["load.metrics.json"]
            },
            Purposes = [LoadToolPurpose.Benchmark],
            Limitations = ["Test limitation"],
            Description = "A test tool"
        };

        var manifest = def.ToManifest();

        Assert.Equal("protocol-lab.load-tool.v1", manifest.SchemaVersion);
        Assert.Equal("test-tool", manifest.Id);
        Assert.Equal("Test Tool", manifest.Name);
        Assert.Equal(LoadToolKinds.Process, manifest.Kind);
        Assert.Contains("h1", manifest.SupportedProtocols);
        Assert.Contains("h2", manifest.SupportedProtocols);
        Assert.Contains("request-response", manifest.SupportedTrafficShapes);
        Assert.Contains("client", manifest.SupportedRoles);
        Assert.Contains("requestsPerSecond", manifest.PrimaryMetrics);
        Assert.Contains("bytesRead", manifest.SecondaryMetrics);
        Assert.Equal("test-parser", manifest.ParserType);
        Assert.True(manifest.PreservesRawOutput);
        Assert.Contains("load.stdout.log", manifest.RequiredArtifacts);
        Assert.Contains("benchmark", manifest.Purposes);
        Assert.Contains("Test limitation", manifest.Limitations);
        Assert.Equal("test-tool", manifest.Executable);
        Assert.Contains("--verbose", manifest.DefaultArguments);
    }

    [Fact]
    public void LoadToolManifest_converts_to_definition()
    {
        var manifest = new LoadToolManifest
        {
            SchemaVersion = "protocol-lab.load-tool.v1",
            Id = "test-tool",
            Name = "Test Tool",
            Kind = LoadToolKinds.Docker,
            SupportedProtocols = ["h3"],
            SupportedTrafficShapes = ["request-response"],
            SupportedRoles = ["client"],
            PrimaryMetrics = ["requestsPerSecond"],
            SecondaryMetrics = ["bytesRead"],
            ParserType = "test-parser",
            PreservesRawOutput = true,
            RequiredArtifacts = ["load.stdout.log"],
            DockerImage = "test/image:latest",
            DockerCommand = "test-tool",
            DockerHostRewrite = "host.docker.internal",
            Sni = "localhost",
            Limitations = ["Test limitation"],
            Purposes = ["benchmark", "validation"]
        };

        var def = manifest.ToDefinition();

        Assert.Equal("protocol-lab.load-tool.v1", def.SchemaVersion);
        Assert.Equal("test-tool", def.Id);
        Assert.Equal("Test Tool", def.Title);
        Assert.Equal(LoadToolKind.Docker, def.Kind);
        Assert.Contains("h3", def.Supports.Protocols);
        Assert.Contains("request-response", def.Supports.TrafficShapes);
        Assert.Contains("client", def.Supports.Roles);
        Assert.Contains("requestsPerSecond", def.Metrics.Primary);
        Assert.Contains("bytesRead", def.Metrics.Secondary);
        Assert.Equal("test-parser", def.Parser.Type);
        Assert.True(def.Parser.PreservesRawOutput);
        Assert.NotNull(def.Execution?.Docker);
        Assert.Equal("test/image:latest", def.Execution.Docker.Image);
        Assert.Contains(LoadToolPurpose.Benchmark, def.Purposes);
        Assert.Contains(LoadToolPurpose.Validation, def.Purposes);
        Assert.Contains("Test limitation", def.Limitations);
    }

    [Fact]
    public void LoadToolManifest_to_definition_roundtrips_through_ToManifest()
    {
        var original = new LoadToolManifest
        {
            SchemaVersion = "protocol-lab.load-tool.v1",
            Id = "roundtrip-test",
            Name = "Roundtrip Test",
            Kind = LoadToolKinds.Process,
            Category = LoadToolCategories.ExternalReference,
            SupportedProtocols = ["h1", "h2"],
            SupportedScenarioFamilies = ["http.application"],
            SupportedTrafficShapes = ["request-response"],
            SupportedRoles = ["client"],
            PrimaryMetrics = ["requestsPerSecond"],
            SecondaryMetrics = ["bytesRead"],
            ParserType = "h2load",
            PreservesRawOutput = true,
            RequiredArtifacts = ["load.stdout.log"],
            OptionalArtifacts = ["load.metrics.json"],
            Executable = "h2load",
            DefaultArguments = ["--some-flag"],
            VersionCommand = ["--version"],
            AvailabilityCheck = "path",
            Limitations = ["Test"],
            Purposes = ["benchmark"],
            Notes = "test notes"
        };

        var def = original.ToDefinition();
        var converted = def.ToManifest();

        Assert.Equal(original.SchemaVersion, converted.SchemaVersion);
        Assert.Equal(original.Id, converted.Id);
        Assert.Equal(original.Name, converted.Name);
        Assert.Equal(original.Kind, converted.Kind);
        Assert.Equal(original.SupportedProtocols, converted.SupportedProtocols);
        Assert.Equal(original.SupportedTrafficShapes, converted.SupportedTrafficShapes);
        Assert.Equal(original.SupportedRoles, converted.SupportedRoles);
        Assert.Equal(original.PrimaryMetrics, converted.PrimaryMetrics);
        Assert.Equal(original.SecondaryMetrics, converted.SecondaryMetrics);
        Assert.Equal(original.ParserType, converted.ParserType);
        Assert.Equal(original.PreservesRawOutput, converted.PreservesRawOutput);
        Assert.Equal(original.RequiredArtifacts, converted.RequiredArtifacts);
        Assert.Equal(original.OptionalArtifacts, converted.OptionalArtifacts);
        Assert.Equal(original.Executable, converted.Executable);
        Assert.Equal(original.AvailabilityCheck, converted.AvailabilityCheck);
        Assert.Equal(original.Limitations, converted.Limitations);
    }

    // ── New Model Type Tests ────────────────────────────────────────────────

    [Fact]
    public void LoadToolDefinition_requires_schema_version()
    {
        var ex = Record.Exception(() => new LoadToolDefinition
        {
            SchemaVersion = "protocol-lab.load-tool.v1",
            Id = "test",
            Title = "Test",
            Kind = LoadToolKind.Process,
            Supports = new LoadToolSupport { Protocols = ["h1"], TrafficShapes = ["request-response"], Roles = ["client"] },
            Metrics = new LoadToolMetrics(),
            Parser = new LoadToolParser { Type = "none" }
        });

        Assert.Null(ex);
    }

    [Fact]
    public void LoadToolKind_enum_values_are_correct()
    {
        Assert.Equal(0, (int)LoadToolKind.Managed);
        Assert.Equal(1, (int)LoadToolKind.Process);
        Assert.Equal(2, (int)LoadToolKind.Docker);
    }

    [Fact]
    public void LoadToolPurpose_enum_values_are_correct()
    {
        Assert.Equal(0, (int)LoadToolPurpose.Validation);
        Assert.Equal(1, (int)LoadToolPurpose.Benchmark);
        Assert.Equal(2, (int)LoadToolPurpose.Profile);
        Assert.Equal(3, (int)LoadToolPurpose.Diagnostic);
    }

    [Fact]
    public void CompatibilityStatus_enum_has_incompatible_traffic_shape()
    {
        var values = Enum.GetValues<CompatibilityStatus>();
        Assert.Contains(CompatibilityStatus.IncompatibleTrafficShape, values);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LoadToolManifest LoadYaml(string fileName)
    {
        return YamlFile.Load<LoadToolManifest>(
            Path.Combine(TestPaths.RepoRoot, "load-tools", fileName));
    }

    private static RunCell NewCell(
        string protocol = "h3",
        string family = "http.application",
        string trafficShape = "request-response")
    {
        return new RunCell(
            new ImplementationManifest
            {
                Id = "kestrel-http3",
                Name = "Kestrel",
                Roles = ["server", "client"],
                SupportedProtocols = [protocol],
                SupportedWorkloadFamilies = [family]
            },
            new ScenarioDefinition
            {
                Id = "http.core.plaintext",
                Family = family,
                Protocol = protocol,
                TrafficShape = trafficShape,
                ImplementationRole = "server"
            },
            protocol,
            16,
            10,
            1,
            30,
            5,
            "clean");
    }
}
