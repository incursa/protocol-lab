// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Model;
using Incursa.ProtocolLab.Runner;

namespace Incursa.ProtocolLab.Tests;

public sealed class LoadProfileContractV1Tests
{
    // ── Manifest Parsing and Schema Validation ───────────────────────────────

    [Fact]
    public void Parses_smoke_profile()
    {
        var profile = LoadProfile("smoke.yaml");

        Assert.Equal("protocol-lab.load-profile.v1", profile.SchemaVersion);
        Assert.Equal("smoke", profile.Id);
        Assert.Equal("Smoke", profile.Title);
        Assert.Equal("stable", profile.Status);
        Assert.Equal("smoke", profile.Purpose);
        Assert.Equal(5, profile.DurationSeconds);
        Assert.Equal(1, profile.WarmupSeconds);
        Assert.Equal(1, profile.Repetitions);
        Assert.NotNull(profile.Http);
        Assert.Equal(1, profile.Http.Connections);
        Assert.Equal(1, profile.Http.Concurrency);
        Assert.NotNull(profile.Http3);
        Assert.Equal(1, profile.Http3.StreamsPerConnection);
        Assert.False(profile.Evidence.Publishable);
        Assert.Equal("smoke", profile.Evidence.MinimumTier);
    }

    [Fact]
    public void Parses_local_regression_profile()
    {
        var profile = LoadProfile("local-regression.yaml");

        Assert.Equal("local-regression", profile.Id);
        Assert.Equal("regression", profile.Purpose);
        Assert.True(profile.IsStable());
        Assert.Equal(15, profile.DurationSeconds);
        Assert.Equal(5, profile.WarmupSeconds);
        Assert.Equal(2, profile.Repetitions);
        Assert.NotNull(profile.Http);
        Assert.Equal(16, profile.Http.Connections);
        Assert.NotNull(profile.Http3);
        Assert.Equal(10, profile.Http3.StreamsPerConnection);
        Assert.Equal(LoadProfilePurpose.Regression, profile.GetPurpose());
    }

    [Fact]
    public void Parses_local_comparison_profile()
    {
        var profile = LoadProfile("local-comparison.yaml");

        Assert.Equal("local-comparison", profile.Id);
        Assert.Equal("comparison", profile.Purpose);
        Assert.Equal(30, profile.DurationSeconds);
        Assert.Equal(10, profile.WarmupSeconds);
        Assert.Equal(5, profile.CooldownSeconds);
        Assert.Equal(3, profile.Repetitions);
        Assert.NotNull(profile.Http);
        Assert.Equal(128, profile.Http.Connections);
        Assert.Equal(128, profile.Http.Concurrency);
        Assert.Equal(10, profile.Http.RequestTimeoutSeconds);
        Assert.NotNull(profile.Http3);
        Assert.Equal(100, profile.Http3.StreamsPerConnection);
        Assert.NotNull(profile.Quic);
        Assert.Equal(32, profile.Quic.Connections);
        Assert.Equal(16, profile.Quic.StreamsPerConnection);
        Assert.Equal(1024, profile.Quic.StreamBytes);
        Assert.Equal("local-comparison", profile.Evidence.MinimumTier);
        Assert.False(profile.Evidence.Publishable);
    }

    [Fact]
    public void All_profiles_parse_successfully()
    {
        var profiles = LoadProfileCatalog.Load(Path.Combine(TestPaths.RepoRoot, "load-profiles"));

        Assert.Contains(profiles, p => p.Id == "smoke");
        Assert.Contains(profiles, p => p.Id == "local-regression");
        Assert.Contains(profiles, p => p.Id == "local-comparison");

        foreach (var profile in profiles)
        {
            Assert.False(string.IsNullOrWhiteSpace(profile.Id));
            Assert.False(string.IsNullOrWhiteSpace(profile.SchemaVersion));
            Assert.Equal("protocol-lab.load-profile.v1", profile.SchemaVersion);
        }
    }

    [Fact]
    public void Catalog_load_returns_empty_for_missing_directory()
    {
        var profiles = LoadProfileCatalog.Load(Path.Combine(TestPaths.RepoRoot, "does-not-exist"));

        Assert.Empty(profiles);
    }

    // ── Protocol-specific accessors ──────────────────────────────────────────

    [Fact]
    public void GetConnections_returns_protocol_specific_values()
    {
        var profile = LoadProfile("local-comparison.yaml");

        Assert.Equal(128, profile.GetConnections("h1"));
        Assert.Equal(128, profile.GetConnections("h2"));
        Assert.Equal(128, profile.GetConnections("h3"));
        Assert.Equal(32, profile.GetConnections("quic"));
        Assert.Null(profile.GetConnections("unknown-proto"));
    }

    [Fact]
    public void GetStreamsPerConnection_returns_protocol_specific_values()
    {
        var profile = LoadProfile("local-comparison.yaml");

        Assert.Null(profile.GetStreamsPerConnection("h1"));
        Assert.Equal(100, profile.GetStreamsPerConnection("h2"));
        Assert.Equal(100, profile.GetStreamsPerConnection("h3"));
        Assert.Equal(16, profile.GetStreamsPerConnection("quic"));
    }

    [Fact]
    public void GetConcurrency_returns_protocol_specific_values()
    {
        var profile = LoadProfile("local-comparison.yaml");

        Assert.Equal(128, profile.GetConcurrency("h1"));
        Assert.Equal(128, profile.GetConcurrency("h2"));
        Assert.Equal(128, profile.GetConcurrency("h3"));
        Assert.Equal(128, profile.GetConcurrency("unknown"));
    }

    [Fact]
    public void Profile_status_detection_works()
    {
        var stable = LoadProfile("smoke.yaml");
        Assert.True(stable.IsStable());
        Assert.False(stable.IsExperimental());
    }

    // ── Matrix Expansion with Load Profile ───────────────────────────────────

    [Fact]
    public void Matrix_expansion_includes_load_profile_id()
    {
        var implementation = new ImplementationManifest
        {
            Id = "kestrel-http3",
            Name = "Kestrel",
            Roles = ["server"],
            SupportedProtocols = ["h3"],
            SupportedWorkloadFamilies = ["http.application"]
        };
        var scenario = new ScenarioDefinition
        {
            Id = "http.core.plaintext",
            Family = "http.application",
            Protocol = "h3",
            TrafficShape = "request-response",
            ImplementationRole = "server",
            Benchmark = new BenchmarkLoadShape
            {
                DurationSeconds = 10,
                WarmupSeconds = 2,
                Repetitions = 1,
                Connections = [1],
                StreamsPerConnection = [1]
            }
        };

        var options = new MatrixOptions(LoadProfileId: "local-comparison");
        var cells = ScenarioMatrix.Expand([implementation], [scenario], options);

        Assert.Single(cells);
        Assert.Equal("local-comparison", cells[0].LoadProfileId);
    }

    [Fact]
    public void Matrix_expansion_without_profile_has_null_id()
    {
        var implementation = new ImplementationManifest
        {
            Id = "kestrel-http3",
            Name = "Kestrel",
            Roles = ["server"],
            SupportedProtocols = ["h3"],
            SupportedWorkloadFamilies = ["http.application"]
        };
        var scenario = new ScenarioDefinition
        {
            Id = "http.core.plaintext",
            Family = "http.application",
            Protocol = "h3",
            TrafficShape = "request-response",
            ImplementationRole = "server",
            Benchmark = new BenchmarkLoadShape
            {
                DurationSeconds = 10,
                WarmupSeconds = 2,
                Connections = [1],
                StreamsPerConnection = [1]
            }
        };

        var options = new MatrixOptions();
        var cells = ScenarioMatrix.Expand([implementation], [scenario], options);

        Assert.Single(cells);
        Assert.Null(cells[0].LoadProfileId);
    }

    [Fact]
    public void RunPlanBuilder_merges_profile_into_options()
    {
        var profiles = new List<LoadProfileDefinition> { LoadProfile("local-comparison.yaml") };
        var profile = profiles[0];

        var options = new MatrixOptions(
            ImplementationIds: ["kestrel-http3"],
            ScenarioIds: ["http.core.plaintext"],
            Protocols: ["h3"],
            Connections: null,
            StreamsPerConnection: null,
            DurationSeconds: null,
            WarmupSeconds: null,
            Repetitions: null,
            NetworkProfiles: null,
            LoadProfileId: null);

        var merged = RunPlanBuilder.MergeProfileIntoOptions(options, profile);

        Assert.Equal("local-comparison", merged.LoadProfileId);
        Assert.Equal(30, merged.DurationSeconds);
        Assert.Equal(10, merged.WarmupSeconds);
        Assert.Equal(3, merged.Repetitions);
    }

    [Fact]
    public void RunPlanBuilder_cli_options_override_profile()
    {
        var profiles = new List<LoadProfileDefinition> { LoadProfile("local-comparison.yaml") };
        var profile = profiles[0];

        var options = new MatrixOptions(
            DurationSeconds: 5,
            WarmupSeconds: 1,
            Repetitions: 1);

        var merged = RunPlanBuilder.MergeProfileIntoOptions(options, profile);

        Assert.Equal("local-comparison", merged.LoadProfileId);
        Assert.Equal(5, merged.DurationSeconds);
        Assert.Equal(1, merged.WarmupSeconds);
        Assert.Equal(1, merged.Repetitions);
    }

    // ── Compatibility Classification ─────────────────────────────────────────

    [Fact]
    public void Compatibility_classifier_rejects_missing_load_profile()
    {
        var cell = NewCell(loadProfileId: "definitely-missing-profile");
        var loadProfiles = new List<LoadProfileDefinition>
        {
            LoadProfile("smoke.yaml")
        };

        var compat = CompatibilityClassifier.Classify(cell, loadProfiles: loadProfiles);

        Assert.False(compat.CanRun);
        Assert.Equal("incompatible-load-profile", compat.Status);
        Assert.Contains("was not found", compat.Reason);
    }

    [Fact]
    public void Compatibility_classifier_accepts_known_profile()
    {
        var cell = NewCell(loadProfileId: "smoke");
        var loadProfiles = new List<LoadProfileDefinition>
        {
            LoadProfile("smoke.yaml")
        };

        var compat = CompatibilityClassifier.Classify(cell, loadProfiles: loadProfiles);

        Assert.True(compat.CanRun);
    }

    [Fact]
    public void Compatibility_classifier_accepts_cells_without_profile()
    {
        var cell = NewCell();

        var compat = CompatibilityClassifier.Classify(cell);

        Assert.True(compat.CanRun);
    }

    [Fact]
    public void Compatibility_classifier_skips_profile_check_when_no_profiles_loaded()
    {
        var cell = NewCell(loadProfileId: "experimental-profile");

        var compat = CompatibilityClassifier.Classify(cell, loadProfiles: []);

        Assert.True(compat.CanRun);
    }

    // ── RunCell backward compat ──────────────────────────────────────────────

    [Fact]
    public void RunCell_without_profile_id_has_null_default()
    {
        var cell = new RunCell(
            new ImplementationManifest { Id = "test" },
            new ScenarioDefinition { Id = "test" },
            "h3", 1, 1, 1, 30, 5, "clean");

        Assert.Null(cell.LoadProfileId);
    }

    [Fact]
    public void RunCell_with_profile_id_stores_it()
    {
        var cell = new RunCell(
            new ImplementationManifest { Id = "test" },
            new ScenarioDefinition { Id = "test" },
            "h3", 1, 1, 1, 30, 5, "clean",
            "local-comparison");

        Assert.Equal("local-comparison", cell.LoadProfileId);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LoadProfileDefinition LoadProfile(string fileName)
    {
        return YamlFile.Load<LoadProfileDefinition>(
            Path.Combine(TestPaths.RepoRoot, "load-profiles", fileName));
    }

    private static RunCell NewCell(string loadProfileId = null)
    {
        return new RunCell(
            new ImplementationManifest
            {
                Id = "kestrel-http3",
                Name = "Kestrel",
                Roles = ["server"],
                SupportedProtocols = ["h3"],
                SupportedWorkloadFamilies = ["http.application"]
            },
            new ScenarioDefinition
            {
                Id = "http.core.plaintext",
                Family = "http.application",
                Protocol = "h3",
                TrafficShape = "request-response",
                ImplementationRole = "server",
                Benchmark = new BenchmarkLoadShape
                {
                    DurationSeconds = 10,
                    WarmupSeconds = 2,
                    Connections = [1],
                    StreamsPerConnection = [1]
                }
            },
            "h3",
            1,
            1,
            1,
            10,
            2,
            "clean",
            loadProfileId);
    }
}
