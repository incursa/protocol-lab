// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Runner;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Tests;

public sealed class ValidationModelTests
{
    [Fact]
    public void Only_passed_validation_allows_benchmark()
    {
        Assert.True(new ScenarioValidationResult
        {
            ScenarioId = "test-scenario",
            TargetId = "test-target",
            AdapterId = "",
            Protocol = "h3",
            Status = ValidationStatus.Passed,
            Summary = "ok"
        }.AllowsBenchmark);
        Assert.False(new ScenarioValidationResult
        {
            ScenarioId = "test-scenario",
            TargetId = "test-target",
            AdapterId = "",
            Protocol = "h3",
            Status = ValidationStatus.Failed,
            Summary = "failed"
        }.AllowsBenchmark);
        Assert.False(new ScenarioValidationResult
        {
            ScenarioId = "test-scenario",
            TargetId = "test-target",
            AdapterId = "",
            Protocol = "h3",
            Status = ValidationStatus.Unsupported,
            Summary = "unsupported"
        }.AllowsBenchmark);
        Assert.False(new ScenarioValidationResult
        {
            ScenarioId = "test-scenario",
            TargetId = "test-target",
            AdapterId = "",
            Protocol = "h3",
            Status = ValidationStatus.NotApplicable,
            Summary = "not applicable"
        }.AllowsBenchmark);
    }

    [Fact]
    public void Reports_unsupported_scenario_with_reason()
    {
        var manifest = new ImplementationManifest
        {
            Id = "limited",
            Roles = ["server"],
            SupportedProtocols = ["h1"],
            SupportedWorkloadFamilies = ["http.application"],
            Capabilities = ["httpPlaintext"]
        };
        var scenario = new ScenarioDefinition
        {
            Id = "http.core.json",
            Family = "http.application",
            Protocol = "h3",
            ImplementationRole = "server",
            RequiredCapabilities = ["httpJson"]
        };

        var support = ScenarioSupport.Evaluate(manifest, scenario, "h3");

        Assert.False(support.IsSupported);
        Assert.Contains("protocol 'h3'", support.Reason);
        Assert.Contains("capability 'httpJson'", support.Reason);
    }

    [Fact]
    public void Incursa_placeholder_manifest_stays_constrained_to_http_core()
    {
        var manifest = new ImplementationManifest
        {
            Id = "incursa-http3",
            Roles = ["server"],
            SupportedProtocols = ["h3"],
            SupportedWorkloadFamilies = ["http.application"],
            Capabilities = ["httpPlaintext", "httpJson"]
        };
        var scenario = new ScenarioDefinition
        {
            Id = "http.core.bytes",
            Family = "http.application",
            Protocol = "h3",
            ImplementationRole = "server",
            RequiredCapabilities = ["httpBytes"]
        };

        var support = ScenarioSupport.Evaluate(manifest, scenario, "h3");

        Assert.False(support.IsSupported);
        Assert.Contains("capability 'httpBytes'", support.Reason);
        Assert.DoesNotContain("capability 'httpPlaintext'", support.Reason);
    }

    [Fact]
    public void Deferred_placeholder_manifest_reports_unsupported_instead_of_claiming_support()
    {
        var manifest = new ImplementationManifest
        {
            Id = "nginx-http3",
            Roles = ["server"],
            SupportedProtocols = [],
            SupportedWorkloadFamilies = [],
            Capabilities = []
        };
        var scenario = new ScenarioDefinition
        {
            Id = "http.core.plaintext",
            Family = "http.application",
            Protocol = "h3",
            ImplementationRole = "server",
            RequiredCapabilities = ["httpPlaintext"]
        };

        var support = ScenarioSupport.Evaluate(manifest, scenario, "h3");

        Assert.False(support.IsSupported);
        Assert.Contains("protocol 'h3'", support.Reason);
        Assert.Contains("workload family 'http.application'", support.Reason);
        Assert.Contains("capability 'httpPlaintext'", support.Reason);
        Assert.DoesNotContain("role 'server'", support.Reason);
    }

    [Fact]
    public async Task H3_protocol_scenario_remains_unsupported_until_validator_exists()
    {
        var cell = new RunCell(
            new ImplementationManifest
            {
                Id = "future-h3",
                Name = "Future H3",
                Roles = ["server"],
                SupportedProtocols = ["h3"],
                SupportedWorkloadFamilies = ["h3.protocol"],
                Capabilities = ["h3Protocol", "h3Qpack"]
            },
            new ScenarioDefinition
            {
                Id = "h3.protocol.qpack-repeated-headers",
                Family = "h3.protocol",
                Protocol = "h3",
                ImplementationRole = "server",
                RequiredCapabilities = ["h3Protocol", "h3Qpack"],
                H3Protocol = new H3ProtocolSpec { Behavior = "qpack-repeated-headers" }
            },
            "h3",
            1,
            1,
            1,
            30,
            5,
            "clean");

        var validation = await HttpScenarioValidator.ValidateAsync(cell, baseUrl: null);

        Assert.Equal(ValidationStatus.Unsupported, validation.Status);
        Assert.Contains("no H3 protocol validator", validation.Summary);
        Assert.False(validation.AllowsBenchmark);
    }

    [Fact]
    public async Task Quic_transport_scenario_remains_unsupported_until_validator_exists()
    {
        var cell = new RunCell(
            new ImplementationManifest
            {
                Id = "future-quic",
                Name = "Future QUIC",
                Roles = ["server"],
                SupportedProtocols = ["quic"],
                SupportedWorkloadFamilies = ["quic.transport"],
                Capabilities = ["quicTransport", "quicStreams"]
            },
            new ScenarioDefinition
            {
                Id = "quic.transport.stream-throughput.1mb",
                Family = "quic.transport",
                Protocol = "quic",
                ImplementationRole = "server",
                RequiredCapabilities = ["quicTransport", "quicStreams"],
                QuicTransport = new QuicTransportSpec { Behavior = "stream-throughput" }
            },
            "quic",
            1,
            1,
            1,
            30,
            5,
            "clean");

        var validation = await HttpScenarioValidator.ValidateAsync(cell, baseUrl: null);

        Assert.Equal(ValidationStatus.Unsupported, validation.Status);
        Assert.Contains("no raw QUIC validator", validation.Summary);
        Assert.False(validation.AllowsBenchmark);
    }

    [Fact]
    public async Task Future_family_scenarios_remain_unsupported_until_validators_exist()
    {
        var webTransportCell = new RunCell(
            new ImplementationManifest
            {
                Id = "future-webtransport",
                Name = "Future WebTransport",
                Roles = ["server"],
                SupportedProtocols = ["h3"],
                SupportedWorkloadFamilies = ["webtransport"],
                Capabilities = ["webtransport", "webtransportBidiStreams"]
            },
            new ScenarioDefinition
            {
                Id = "webtransport.session-bidi-echo",
                Family = "webtransport",
                Protocol = "h3",
                ImplementationRole = "server",
                RequiredCapabilities = ["webtransport", "webtransportBidiStreams"],
                WebTransport = new WebTransportSpec { Behavior = "session-bidi-echo" }
            },
            "h3",
            1,
            1,
            1,
            30,
            5,
            "clean");
        var masqueCell = new RunCell(
            new ImplementationManifest
            {
                Id = "future-masque",
                Name = "Future MASQUE",
                Roles = ["server"],
                SupportedProtocols = ["h3"],
                SupportedWorkloadFamilies = ["masque"],
                Capabilities = ["masque", "masqueConnectUdp"]
            },
            new ScenarioDefinition
            {
                Id = "masque.connect-udp-tunnel",
                Family = "masque",
                Protocol = "h3",
                ImplementationRole = "server",
                RequiredCapabilities = ["masque", "masqueConnectUdp"],
                Masque = new MasqueSpec { Behavior = "connect-udp-tunnel" }
            },
            "h3",
            1,
            1,
            1,
            30,
            5,
            "clean");

        var webTransportValidation = await HttpScenarioValidator.ValidateAsync(webTransportCell, baseUrl: null);
        var masqueValidation = await HttpScenarioValidator.ValidateAsync(masqueCell, baseUrl: null);

        Assert.Equal(ValidationStatus.Unsupported, webTransportValidation.Status);
        Assert.Contains("WebTransport validation is modeled", webTransportValidation.Summary);
        Assert.Equal(ValidationStatus.Unsupported, masqueValidation.Status);
        Assert.Contains("MASQUE validation is modeled", masqueValidation.Summary);
    }

    [Fact]
    public void Validation_status_passed_has_correct_enum_value()
    {
        var result = new ScenarioValidationResult
        {
            ScenarioId = "s1",
            TargetId = "t1",
            AdapterId = "",
            Protocol = "h3",
            Status = ValidationStatus.Passed,
            Summary = "ok"
        };

        Assert.Equal(ValidationStatus.Passed, result.Status);
        Assert.True(result.AllowsBenchmark);
    }

    [Fact]
    public void Validation_status_failed_has_correct_enum_value()
    {
        var result = new ScenarioValidationResult
        {
            ScenarioId = "s1",
            TargetId = "t1",
            AdapterId = "",
            Protocol = "h3",
            Status = ValidationStatus.Failed,
            Summary = "failed"
        };

        Assert.Equal(ValidationStatus.Failed, result.Status);
        Assert.False(result.AllowsBenchmark);
    }

    [Fact]
    public void Validation_status_unsupported_has_correct_enum_value()
    {
        var result = new ScenarioValidationResult
        {
            ScenarioId = "s1",
            TargetId = "t1",
            AdapterId = "",
            Protocol = "h3",
            Status = ValidationStatus.Unsupported,
            Summary = "unsupported"
        };

        Assert.Equal(ValidationStatus.Unsupported, result.Status);
        Assert.False(result.AllowsBenchmark);
    }

    [Fact]
    public void Validation_status_not_applicable_has_correct_enum_value()
    {
        var result = new ScenarioValidationResult
        {
            ScenarioId = "s1",
            TargetId = "t1",
            AdapterId = "",
            Protocol = "h3",
            Status = ValidationStatus.NotApplicable,
            Summary = "not applicable"
        };

        Assert.Equal(ValidationStatus.NotApplicable, result.Status);
        Assert.False(result.AllowsBenchmark);
    }

    [Fact]
    public void Validation_status_inconclusive_has_correct_enum_value()
    {
        var result = new ScenarioValidationResult
        {
            ScenarioId = "s1",
            TargetId = "t1",
            AdapterId = "",
            Protocol = "h3",
            Status = ValidationStatus.Inconclusive,
            Summary = "inconclusive"
        };

        Assert.Equal(ValidationStatus.Inconclusive, result.Status);
        Assert.False(result.AllowsBenchmark);
    }

    [Fact]
    public void Validation_status_infrastructure_failure_has_correct_enum_value()
    {
        var result = new ScenarioValidationResult
        {
            ScenarioId = "s1",
            TargetId = "t1",
            AdapterId = "",
            Protocol = "h3",
            Status = ValidationStatus.InfrastructureFailure,
            Summary = "infrastructure failure"
        };

        Assert.Equal(ValidationStatus.InfrastructureFailure, result.Status);
        Assert.False(result.AllowsBenchmark);
    }

    [Fact]
    public void Validation_result_carries_observations()
    {
        var observations = new List<ValidationObservation>
        {
            new() { Category = "response", Description = "status code matched", ExpectedValue = "200", ActualValue = "200" }
        };
        var result = new ScenarioValidationResult
        {
            ScenarioId = "s1",
            TargetId = "t1",
            AdapterId = "",
            Protocol = "h3",
            Status = ValidationStatus.Passed,
            Summary = "ok",
            Observations = observations
        };

        Assert.NotEmpty(result.Observations);
        Assert.Equal("response", result.Observations[0].Category);
        Assert.Equal("200", result.Observations[0].ExpectedValue);
    }

    [Fact]
    public void Validation_result_carries_proof_artifacts()
    {
        var artifacts = new List<ValidationProofArtifact>
        {
            new() { Name = "response.json", Path = "/tmp/response.json", Category = "proof" }
        };
        var result = new ScenarioValidationResult
        {
            ScenarioId = "s1",
            TargetId = "t1",
            AdapterId = "",
            Protocol = "h3",
            Status = ValidationStatus.Passed,
            Summary = "ok",
            ProofArtifacts = artifacts
        };

        Assert.NotEmpty(result.ProofArtifacts);
        Assert.Equal("response.json", result.ProofArtifacts[0].Name);
        Assert.Equal("proof", result.ProofArtifacts[0].Category);
    }
}
