// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using Incursa.ProtocolLab.Runner;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Tests;

public sealed class HttpValidationModelTests
{
    [Fact]
    public void Generates_deterministic_request_body()
    {
        var body = HttpScenarioValidator.CreateRequestBody("deterministic-bytes:260");

        Assert.Equal(260, body.Length);
        Assert.Equal(0, body[0]);
        Assert.Equal(250, body[250]);
        Assert.Equal(0, body[251]);
    }

    [Fact]
    public void Leaves_empty_request_body_for_missing_generation_rule()
    {
        Assert.Empty(HttpScenarioValidator.CreateRequestBody(null));
    }

    [Fact]
    public void Validates_plaintext_response_success_and_failure()
    {
        var endpoint = new HttpEndpointSpec
        {
            ExpectedStatus = 200,
            ExpectedHeaders = new Dictionary<string, string> { ["content-type"] = "text/plain" },
            ExpectedBodyRule = "exact",
            ExpectedBody = "Hello, World!",
            ExpectedBodySize = 13
        };
        using var success = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Hello, World!")
        };
        success.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        using var failure = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Wrong")
        };
        failure.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

        Assert.Empty(HttpScenarioValidator.ValidateResponse(endpoint, success.StatusCode, success.Headers, success.Content.Headers, "Hello, World!"u8.ToArray(), []));
        var errors = HttpScenarioValidator.ValidateResponse(endpoint, failure.StatusCode, failure.Headers, failure.Content.Headers, "Wrong"u8.ToArray(), []);

        Assert.Contains(errors, error => error.Contains("body size", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("exact expected body", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validates_json_response_success_and_failure()
    {
        var endpoint = new HttpEndpointSpec
        {
            ExpectedStatus = 200,
            ExpectedHeaders = new Dictionary<string, string> { ["content-type"] = "application/json" },
            ExpectedBodyRule = "jsonEquivalent",
            ExpectedBody = "{\"message\":\"Hello, World!\"}"
        };
        using var success = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"message\":\"Hello, World!\"}")
        };
        success.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        using var failure = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"message\":\"Wrong\"}")
        };
        failure.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        Assert.Empty(HttpScenarioValidator.ValidateResponse(endpoint, success.StatusCode, success.Headers, success.Content.Headers, "{\"message\":\"Hello, World!\"}"u8.ToArray(), []));
        var errors = HttpScenarioValidator.ValidateResponse(endpoint, failure.StatusCode, failure.Headers, failure.Content.Headers, "{\"message\":\"Wrong\"}"u8.ToArray(), []);

        Assert.Contains(errors, error => error.Contains("JSON-equivalent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parses_curl_http3_protocol_proof_markers()
    {
        var output = """
        Hello, World!
        __PROTOCOL_LAB_HTTP_VERSION__:3
        __PROTOCOL_LAB_STATUS__:200
        __PROTOCOL_LAB_CONTENT_TYPE__:text/plain; charset=utf-8
        """;

        var parsed = ProtocolProofValidator.ParseCurlProof(System.Text.Encoding.UTF8.GetBytes(output));

        Assert.Equal("3", parsed.HttpVersion);
        Assert.Equal(200, parsed.StatusCode);
        Assert.Equal("text/plain; charset=utf-8", parsed.ContentType);
        Assert.Equal("Hello, World!", System.Text.Encoding.UTF8.GetString(parsed.Body));
        Assert.Empty(parsed.Errors);
    }

    [Fact]
    public void Protocol_proof_response_validation_rejects_http_fallback_version()
    {
        var output = """
        Hello, World!
        __PROTOCOL_LAB_HTTP_VERSION__:2
        __PROTOCOL_LAB_STATUS__:200
        __PROTOCOL_LAB_CONTENT_TYPE__:text/plain
        """;

        var parsed = ProtocolProofValidator.ParseCurlProof(System.Text.Encoding.UTF8.GetBytes(output));

        Assert.Equal("2", parsed.HttpVersion);
        Assert.NotEqual("3", parsed.HttpVersion);
    }

    [Fact]
    public void Validates_protocol_proof_body_and_content_type()
    {
        var endpoint = new HttpEndpointSpec
        {
            ExpectedStatus = 200,
            ExpectedHeaders = new Dictionary<string, string> { ["content-type"] = "application/json" },
            ExpectedBodyRule = "jsonEquivalent",
            ExpectedBody = "{\"message\":\"Hello, World!\"}"
        };

        var errors = HttpScenarioValidator.ValidateResponse(
            endpoint,
            HttpStatusCode.OK,
            "application/json; charset=utf-8",
            "{\"message\":\"Hello, World!\"}"u8.ToArray(),
            []);

        Assert.Empty(errors);
    }

    [Fact]
    public void Managed_h3_proof_validation_passes_with_http3_response()
    {
        var cell = NewHttpCell("h3");
        var paths = ArtifactLayout.GetCellPaths(Path.GetTempPath(), $"protocol-lab-{Guid.NewGuid():N}", cell);
        var response = new ManagedProofResponse(
            new Version(3, 0),
            HttpStatusCode.OK,
            "text/plain",
            "Hello, World!"u8.ToArray(),
            ["Loopback certificate validation bypass was used for managed QUIC HTTP/3 proof."]);

        var result = ProtocolProofValidator.BuildManagedProofValidationResult(
            cell,
            new Uri("https://127.0.0.1:5443/plaintext"),
            paths,
            "aspnetcore-development-certificate-or-explicit-pfx",
            response,
            "unavailable",
            certificateBypassUsed: true);

        Assert.Equal(ValidationStatus.Passed, result.Status);
        Assert.Equal("managed-quic-http3-exact", result.ProtocolProof!.Method);
        Assert.Equal("managed-incursa-quic-http3", result.ProtocolProof.ProofClient);
        Assert.Equal("h3", result.ProtocolProof.ProvenProtocol);
        Assert.Equal("3.0", result.ProtocolProof.ResponseVersion);
        Assert.Contains("loopback-certificate-validation-bypass", result.ProtocolProof.CertificateMode);
    }

    [Fact]
    public void Managed_h3_proof_validation_fails_on_fallback_response_version()
    {
        var cell = NewHttpCell("h3");
        var paths = ArtifactLayout.GetCellPaths(Path.GetTempPath(), $"protocol-lab-{Guid.NewGuid():N}", cell);
        var response = new ManagedProofResponse(
            new Version(2, 0),
            HttpStatusCode.OK,
            "text/plain",
            "Hello, World!"u8.ToArray(),
            []);

        var result = ProtocolProofValidator.BuildManagedProofValidationResult(
            cell,
            new Uri("https://127.0.0.1:5443/plaintext"),
            paths,
            "aspnetcore-development-certificate-or-explicit-pfx",
            response,
            "http3-unsupported",
            certificateBypassUsed: false);

        Assert.Equal(ValidationStatus.Failed, result.Status);
        Assert.True(result.ProtocolProof!.FallbackDetected);
        Assert.Equal("2.0", result.ProtocolProof.ResponseVersion);
        Assert.Contains(result.Errors!, error => error.Contains("exact version policy", StringComparison.OrdinalIgnoreCase));
    }

    private static RunCell NewHttpCell(string protocol)
    {
        return new RunCell(
            new ImplementationManifest { Id = "kestrel-http3", Name = "Kestrel" },
            new ScenarioDefinition
            {
                Id = "http.core.plaintext",
                Name = "HTTP Plaintext",
                Family = "http.application",
                Protocol = protocol,
                ImplementationRole = "server",
                Endpoint = new HttpEndpointSpec
                {
                    Method = "GET",
                    Path = "/plaintext",
                    ExpectedStatus = 200,
                    ExpectedHeaders = new Dictionary<string, string> { ["content-type"] = "text/plain" },
                    ExpectedBodyRule = "exact",
                    ExpectedBody = "Hello, World!",
                    ExpectedBodySize = 13
                }
            },
            protocol,
            1,
            1,
            1,
            30,
            5,
            "clean");
    }
}
