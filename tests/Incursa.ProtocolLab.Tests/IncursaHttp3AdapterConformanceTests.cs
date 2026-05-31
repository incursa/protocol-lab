// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Tests.Fixtures.IncursaHttp3AdapterLab;

namespace Incursa.ProtocolLab.Tests;

[Collection(RunnerContractFixtureLabCollection.Name)]
public sealed class IncursaHttp3AdapterConformanceTests
{
    [Fact]
    public async Task Adapter_reports_health_and_manifest()
    {
        await using var host = await IncursaHttp3AdapterProcessHost.StartAsync();

        var client = new ProtocolLabAdapterClient(host.Client);
        var health = await client.GetHealthAsync();
        var manifest = await client.GetManifestAsync();

        Assert.Equal("incursa-http3-adapter-v1", health.AdapterIdentity.Id);
        Assert.Equal(AdapterHealthStatus.Ready, health.Status);
        Assert.Equal("incursa-http3-adapter-v1", manifest.AdapterIdentity.Id);
        Assert.Equal("incursa-http3", manifest.ImplementationIdentity.Id);
        Assert.Contains(manifest.ClaimedCapabilities, capability => capability.Id == "adapter-control-plane");
        Assert.Contains(manifest.ClaimedCapabilities, capability => capability.Id == "http3.server");
        Assert.Contains(manifest.ClaimedCapabilities, capability => capability.Id == "quic.server");
        Assert.Contains(manifest.ClaimedCapabilities, capability => capability.Id == "httpPlaintext");
        Assert.Contains(manifest.ClaimedCapabilities, capability => capability.Id == "httpJson");
        Assert.Contains(manifest.ClaimedCapabilities, capability => capability.Id == "httpStatus");
        Assert.Contains(manifest.ClaimedCapabilities, capability => capability.Id == "httpBytes");
        Assert.Contains(manifest.ClaimedCapabilities, capability => capability.Id == "httpStreaming");
        Assert.Contains(manifest.ClaimedCapabilities, capability => capability.Id == "httpUpload");
        Assert.Contains(manifest.ClaimedCapabilities, capability => capability.Id == "httpHeaders");
        Assert.Contains(manifest.SupportedScenarioSelectors, selector => selector.Expression.Contains("http.core.*", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(manifest.SupportedScenarioSelectors, selector => selector.Expression.Contains("fixture.incursa-http3.*", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(manifest.SupportedEndpointTypes, endpointType => endpointType.Type == "https");
        Assert.Contains(manifest.SupportedEndpointTypes, endpointType => endpointType.Protocols.Contains("h3"));
    }

    [Fact]
    public async Task Adapter_session_lifecycle_creates_discovers_serves_and_cleans_up_the_protocol_endpoint()
    {
        await using var host = await IncursaHttp3AdapterProcessHost.StartAsync();
        var client = new ProtocolLabAdapterClient(host.Client);

        var session = await client.CreateSessionAsync(new AdapterSessionCreateRequest
        {
            RequestedSessionId = "incursa-adapter-lifecycle",
            RunId = "incursa-adapter-test",
            CellId = "lifecycle"
        });

        try
        {
            var prepare = await client.PrepareAsync(session.Session.SessionId, CreatePrepareRequest(
                scenarioId: "http.core.plaintext",
                protocol: "h3",
                method: HttpMethod.Get.Method,
                path: "/plaintext",
                requiredCapability: "httpPlaintext"));

            Assert.Equal(AdapterOperationResultCategory.Succeeded, prepare.Category);

            var start = await client.StartAsync(session.Session.SessionId);
            Assert.Equal(AdapterOperationResultCategory.Succeeded, start.Category);

            var status = await client.GetStatusAsync(session.Session.SessionId);
            Assert.Equal(AdapterReadinessStatus.Ready, status.Readiness.Status);
            Assert.Equal(AdapterSessionState.Ready, status.Session.State);

            var endpoints = await client.GetEndpointsAsync(session.Session.SessionId);
            var endpoint = Assert.Single(endpoints.Endpoints);
            var controlPlaneUrl = host.Client.BaseAddress!.AbsoluteUri.TrimEnd('/');
            var protocolEndpointUrl = $"{endpoint.Scheme}://{endpoint.Authority}";

            Assert.NotEqual(controlPlaneUrl, protocolEndpointUrl);
            Assert.Equal("https", endpoint.Scheme);
            Assert.Equal("h3", endpoint.Protocol);
            Assert.Equal("127.0.0.1", endpoint.Host);
            Assert.Equal("/", endpoint.Path);

            using var httpClient = CreateHttp3Client();
            var response = await SendRequestAsync(httpClient, endpoint, HttpMethod.Get, "/plaintext");
            Assert.Equal("Hello, World!", await response.Content.ReadAsStringAsync());
            Assert.Equal("text/plain; charset=utf-8", response.Content.Headers.ContentType?.ToString());

            var metrics = await client.GetMetricsAsync(session.Session.SessionId);
            Assert.Equal(AdapterResourceAvailability.Available, metrics.Availability);
            Assert.Contains(metrics.Metrics, metric => metric.MetricId == "session.state");
            Assert.Contains(metrics.Metrics, metric => metric.MetricId == "endpoint.port");
            Assert.Contains(metrics.Metrics, metric => metric.MetricId == "endpoint.active-connections");
            Assert.Contains(metrics.Metrics, metric => metric.MetricId == "endpoint.active-requests");

            var artifacts = await client.GetArtifactsAsync(session.Session.SessionId);
            Assert.Equal(AdapterResourceAvailability.Available, artifacts.Availability);
            Assert.Contains(artifacts.Artifacts, artifact => artifact.ArtifactId == "server.stdout");
            Assert.Contains(artifacts.Artifacts, artifact => artifact.ArtifactId == "server.stderr");
            Assert.Contains(artifacts.Artifacts, artifact => artifact.ArtifactId == "session.snapshot");
            Assert.Contains(artifacts.Artifacts, artifact => artifact.ArtifactId == "endpoint.snapshot");

            var stop = await client.StopAsync(session.Session.SessionId);
            Assert.Equal(AdapterOperationResultCategory.Succeeded, stop.Category);

            var delete = await client.DeleteSessionAsync(session.Session.SessionId);
            Assert.Equal(AdapterOperationResultCategory.Succeeded, delete.Category);

            var deleteAgain = await client.DeleteSessionAsync(session.Session.SessionId);
            Assert.Equal(AdapterOperationResultCategory.Succeeded, deleteAgain.Category);

            var exception = await Assert.ThrowsAsync<ProtocolLabAdapterProblemException>(() => client.GetSessionAsync(session.Session.SessionId));
            Assert.Equal(System.Net.HttpStatusCode.NotFound, exception.StatusCode);
            Assert.Equal("session-not-found", exception.Problem.Code);
        }
        finally
        {
            await CleanupSessionAsync(client, session.Session.SessionId);
        }
    }

    [Fact]
    public async Task Adapter_reports_unsupported_scenario_structurally()
    {
        await using var host = await IncursaHttp3AdapterProcessHost.StartAsync();
        var client = new ProtocolLabAdapterClient(host.Client);

        var session = await client.CreateSessionAsync(new AdapterSessionCreateRequest
        {
            RequestedSessionId = "incursa-adapter-unsupported",
            RunId = "incursa-adapter-test",
            CellId = "unsupported"
        });

        try
        {
            var prepare = await client.PrepareAsync(session.Session.SessionId, CreatePrepareRequest(
                scenarioId: "fixture.incursa-http3.unsupported",
                protocol: "h3",
                method: HttpMethod.Get.Method,
                path: "/unsupported",
                requiredCapability: "httpPlaintext",
                family: "fixture.incursa-http3"));

            Assert.Equal(AdapterOperationResultCategory.Unsupported, prepare.Category);
            Assert.Equal("unsupported", prepare.Code);

            var status = await client.GetStatusAsync(session.Session.SessionId);
            Assert.Equal(AdapterReadinessStatus.Unsupported, status.Readiness.Status);

            var endpoints = await client.GetEndpointsAsync(session.Session.SessionId);
            Assert.Empty(endpoints.Endpoints);

            var start = await client.StartAsync(session.Session.SessionId);
            Assert.Equal(AdapterOperationResultCategory.Unsupported, start.Category);

            var stop = await client.StopAsync(session.Session.SessionId);
            Assert.Equal(AdapterOperationResultCategory.Unsupported, stop.Category);
        }
        finally
        {
            await CleanupSessionAsync(client, session.Session.SessionId);
        }
    }

    [Fact]
    public async Task Adapter_rejects_h1_or_h2_protocol_requests_explicitly()
    {
        await using var host = await IncursaHttp3AdapterProcessHost.StartAsync();
        var client = new ProtocolLabAdapterClient(host.Client);

        var session = await client.CreateSessionAsync(new AdapterSessionCreateRequest
        {
            RequestedSessionId = "incursa-adapter-protocol-mismatch",
            RunId = "incursa-adapter-test",
            CellId = "protocol-mismatch"
        });

        try
        {
            var prepare = await client.PrepareAsync(session.Session.SessionId, CreatePrepareRequest(
                scenarioId: "http.core.plaintext",
                protocol: "h1",
                method: HttpMethod.Get.Method,
                path: "/plaintext",
                requiredCapability: "httpPlaintext"));

            Assert.Equal(AdapterOperationResultCategory.Unsupported, prepare.Category);
            Assert.Equal("unsupported", prepare.Code);
            Assert.Contains("HTTP/3", prepare.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await CleanupSessionAsync(client, session.Session.SessionId);
        }
    }

    [Fact]
    public async Task Adapter_supports_public_http_application_scenarios()
    {
        await using var host = await IncursaHttp3AdapterProcessHost.StartAsync();
        var client = new ProtocolLabAdapterClient(host.Client);

        var scenarios = new[]
        {
            new ScenarioExercise(
                ScenarioId: "http.core.plaintext",
                Method: HttpMethod.Get,
                Path: "/plaintext",
                RequiredCapability: "httpPlaintext",
                ValidateResponse: ValidatePlaintextAsync),
            new ScenarioExercise(
                ScenarioId: "http.core.json",
                Method: HttpMethod.Get,
                Path: "/json",
                RequiredCapability: "httpJson",
                ValidateResponse: ValidateJsonAsync),
            new ScenarioExercise(
                ScenarioId: "http.core.status",
                Method: HttpMethod.Get,
                Path: "/status",
                RequiredCapability: "httpStatus",
                ValidateResponse: ValidateStatusAsync),
            new ScenarioExercise(
                ScenarioId: "http.payload.bytes.1kb",
                Method: HttpMethod.Get,
                Path: "/bytes/1024",
                RequiredCapability: "httpBytes",
                ValidateResponse: response => ValidateBytesAsync(response, 1024)),
            new ScenarioExercise(
                ScenarioId: "http.streaming.bytes.2x8",
                Method: HttpMethod.Get,
                Path: "/stream/bytes",
                RequiredCapability: "httpStreaming",
                Query: new Dictionary<string, string>
                {
                    ["chunks"] = "2",
                    ["size"] = "8",
                    ["delayMs"] = "0"
                },
                ValidateResponse: response => ValidateBytesAsync(response, 16)),
            new ScenarioExercise(
                ScenarioId: "http.headers.response.3x4",
                Method: HttpMethod.Get,
                Path: "/headers/response",
                RequiredCapability: "httpHeaders",
                Query: new Dictionary<string, string>
                {
                    ["count"] = "3",
                    ["size"] = "4"
                },
                ValidateResponse: response => ValidateResponseHeadersAsync(response, 3, 4)),
            new ScenarioExercise(
                ScenarioId: "http.headers.inspect-request",
                Method: HttpMethod.Get,
                Path: "/inspect/headers",
                RequiredCapability: "httpHeaders",
                RequestHeaders: new Dictionary<string, string>
                {
                    ["X-Protocol-Bench-Test"] = "fixture-kestrel"
                },
                ValidateResponse: ValidateInspectHeadersAsync),
            new ScenarioExercise(
                ScenarioId: "http.upload.echo.16b",
                Method: HttpMethod.Post,
                Path: "/echo",
                RequiredCapability: "httpUpload",
                RequestHeaders: new Dictionary<string, string>
                {
                    ["Content-Type"] = "application/octet-stream"
                },
                RequestBody: CreateDeterministicBytes(16),
                ValidateResponse: response => ValidateEchoAsync(response, CreateDeterministicBytes(16))),
            new ScenarioExercise(
                ScenarioId: "http.upload.hash.16b",
                Method: HttpMethod.Post,
                Path: "/hash",
                RequiredCapability: "httpUpload",
                RequestHeaders: new Dictionary<string, string>
                {
                    ["Content-Type"] = "application/octet-stream"
                },
                RequestBody: CreateDeterministicBytes(16),
                ValidateResponse: response => ValidateHashAsync(response, CreateDeterministicBytes(16))),
            new ScenarioExercise(
                ScenarioId: "http.upload.sink.16b",
                Method: HttpMethod.Post,
                Path: "/sink",
                RequiredCapability: "httpUpload",
                RequestHeaders: new Dictionary<string, string>
                {
                    ["Content-Type"] = "application/octet-stream"
                },
                RequestBody: CreateDeterministicBytes(16),
                ValidateResponse: response => ValidateSinkAsync(response, 16))
        };

        foreach (var scenario in scenarios)
        {
            await ExerciseScenarioAsync(client, scenario);
        }
    }

    private static AdapterPrepareRequest CreatePrepareRequest(
        string scenarioId,
        string protocol,
        string method,
        string path,
        string requiredCapability,
        string family = "http.application",
        Dictionary<string, string>? query = null,
        Dictionary<string, string>? requestHeaders = null)
    {
        return new AdapterPrepareRequest
        {
            ScenarioId = scenarioId,
            ScenarioVersion = "1.0",
            Role = "server",
            ScenarioDocument = CreateScenarioDocument(
                scenarioId,
                protocol,
                method,
                path,
                requiredCapability,
                family,
                query,
                requestHeaders),
            RequestedEndpointBindings =
            [
                new AdapterEndpointBinding
                {
                    BindingId = "primary",
                    Purpose = "test-endpoint",
                    EndpointType = protocol
                }
            ]
        };
    }

    private static JsonElement CreateScenarioDocument(
        string scenarioId,
        string protocol,
        string method,
        string path,
        string requiredCapability,
        string family,
        Dictionary<string, string>? query,
        Dictionary<string, string>? requestHeaders)
    {
        var endpoint = new Dictionary<string, object?>
        {
            ["method"] = method,
            ["path"] = path,
            ["query"] = query ?? new Dictionary<string, string>(),
            ["requestHeaders"] = requestHeaders ?? new Dictionary<string, string>(),
            ["expectedStatus"] = 200
        };

        return JsonSerializer.SerializeToElement(new
        {
            id = scenarioId,
            name = scenarioId,
            family,
            version = "1.0",
            protocol,
            implementationRole = "server",
            requiredCapabilities = new[] { requiredCapability },
            endpoint
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static async Task ExerciseScenarioAsync(ProtocolLabAdapterClient client, ScenarioExercise scenario)
    {
        var sessionId = $"incursa-http3-{scenario.ScenarioId.Replace('.', '-')}";
        var session = await client.CreateSessionAsync(new AdapterSessionCreateRequest
        {
            RequestedSessionId = sessionId,
            RunId = "incursa-adapter-scenarios",
            CellId = scenario.ScenarioId
        });

        try
        {
            var prepare = await client.PrepareAsync(session.Session.SessionId, CreatePrepareRequest(
                scenario.ScenarioId,
                "h3",
                scenario.Method.Method,
                scenario.Path,
                scenario.RequiredCapability,
                query: scenario.Query,
                requestHeaders: scenario.RequestHeaders));

            Assert.Equal(AdapterOperationResultCategory.Succeeded, prepare.Category);

            var start = await client.StartAsync(session.Session.SessionId);
            Assert.Equal(AdapterOperationResultCategory.Succeeded, start.Category);

            var endpoints = await client.GetEndpointsAsync(session.Session.SessionId);
            var endpoint = Assert.Single(endpoints.Endpoints);

            using var httpClient = CreateHttp3Client();
            HttpResponseMessage response;
            try
            {
                response = await SendRequestAsync(httpClient, endpoint, scenario);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Incursa HTTP/3 scenario '{scenario.ScenarioId}' failed while sending {scenario.Method} {scenario.Path}.",
                    exception);
            }

            try
            {
                await scenario.ValidateResponse(response);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Incursa HTTP/3 scenario '{scenario.ScenarioId}' failed while validating the response from {scenario.Method} {scenario.Path}.",
                    exception);
            }
            finally
            {
                response.Dispose();
            }
        }
        finally
        {
            await CleanupSessionAsync(client, session.Session.SessionId);
        }
    }

    private static async Task CleanupSessionAsync(ProtocolLabAdapterClient client, string sessionId)
    {
        try
        {
            await client.StopAsync(sessionId);
        }
        catch
        {
        }

        try
        {
            await client.DeleteSessionAsync(sessionId);
        }
        catch
        {
        }
    }

    private static HttpClient CreateHttp3Client()
    {
        var handler = new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = static (_, _, _, _) => true
            }
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        return client;
    }

    private static Task<HttpResponseMessage> SendRequestAsync(
        HttpClient client,
        AdapterEndpoint endpoint,
        HttpMethod method,
        string path,
        Dictionary<string, string>? query = null,
        Dictionary<string, string>? requestHeaders = null,
        byte[]? requestBody = null)
    {
        return SendRequestAsync(
            client,
            endpoint,
            new ScenarioExercise(
                "inline",
                method,
                path,
                RequiredCapability: string.Empty,
                ValidateResponse: static _ => Task.CompletedTask,
                Query: query,
                RequestHeaders: requestHeaders,
                RequestBody: requestBody));
    }

    private static async Task<HttpResponseMessage> SendRequestAsync(
        HttpClient client,
        AdapterEndpoint endpoint,
        ScenarioExercise scenario)
    {
        var requestUri = BuildRequestUri(endpoint, scenario.Path, scenario.Query);
        var request = new HttpRequestMessage(scenario.Method, requestUri)
        {
            Version = System.Net.HttpVersion.Version30,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        foreach (var header in scenario.RequestHeaders ?? [])
        {
            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                request.Content ??= new ByteArrayContent(Array.Empty<byte>());
                request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (scenario.RequestBody is { Length: > 0 })
        {
            request.Content = new ByteArrayContent(scenario.RequestBody);
            if (scenario.RequestHeaders is not null && scenario.RequestHeaders.TryGetValue("Content-Type", out var contentType))
            {
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }
            else
            {
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            }
        }

        var response = await client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, response.Version.Major);
        return response;
    }

    private static Uri BuildRequestUri(AdapterEndpoint endpoint, string path, Dictionary<string, string>? query)
    {
        var builder = new UriBuilder(endpoint.Scheme, endpoint.Host, endpoint.Port, path);
        if (query is { Count: > 0 })
        {
            builder.Query = string.Join("&", query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        }

        return builder.Uri;
    }

    private static async Task ValidatePlaintextAsync(HttpResponseMessage response)
    {
        Assert.Equal("text/plain; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Equal("Hello, World!", await response.Content.ReadAsStringAsync());
    }

    private static async Task ValidateJsonAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Hello, World!", document.RootElement.GetProperty("message").GetString());
    }

    private static async Task ValidateStatusAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("KestrelBenchServer", document.RootElement.GetProperty("server").GetString());
        Assert.Equal("kestrel-http3", document.RootElement.GetProperty("implementation").GetString());
        Assert.Equal("h3", document.RootElement.GetProperty("protocol").GetString());
        Assert.Equal("Incursa HTTP/3", document.RootElement.GetProperty("adapterServer").GetString());
        Assert.Equal("incursa-http3", document.RootElement.GetProperty("adapterImplementation").GetString());
        Assert.True(DateTimeOffset.TryParse(document.RootElement.GetProperty("utc").GetString(), out _));
    }

    private static async Task ValidateBytesAsync(HttpResponseMessage response, int expectedSize)
    {
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(expectedSize, body.Length);
        Assert.Equal(CreateDeterministicBytes(expectedSize), body);
    }

    private static async Task ValidateResponseHeadersAsync(HttpResponseMessage response, int expectedCount, int expectedSize)
    {
        Assert.Equal("text/plain; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Equal("headers", await response.Content.ReadAsStringAsync());

        var syntheticHeaders = response.Headers
            .Where(header => header.Key.StartsWith("X-Protocol-Bench-Header-", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(expectedCount, syntheticHeaders.Length);
        Assert.All(syntheticHeaders, header => Assert.All(header.Value, value => Assert.Equal(expectedSize, value.Length)));
    }

    private static async Task ValidateInspectHeadersAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("GET", document.RootElement.GetProperty("method").GetString());
        Assert.Equal("/inspect/headers", document.RootElement.GetProperty("path").GetString());
        Assert.Equal("fixture-kestrel", document.RootElement.GetProperty("X-Protocol-Bench-Test").GetString());
    }

    private static async Task ValidateEchoAsync(HttpResponseMessage response, byte[] expectedBody)
    {
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(expectedBody, body);
    }

    private static async Task ValidateHashAsync(HttpResponseMessage response, byte[] expectedBody)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedBody.Length, document.RootElement.GetProperty("bytesRead").GetInt32());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(expectedBody)).ToLowerInvariant(), document.RootElement.GetProperty("sha256").GetString());
    }

    private static async Task ValidateSinkAsync(HttpResponseMessage response, int expectedBytesRead)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedBytesRead, document.RootElement.GetProperty("bytesRead").GetInt32());
    }

    private static byte[] CreateDeterministicBytes(int size)
    {
        var bytes = new byte[size];
        for (var index = 0; index < size; index++)
        {
            bytes[index] = (byte)(index % 251);
        }

        return bytes;
    }

    private sealed record ScenarioExercise(
        string ScenarioId,
        HttpMethod Method,
        string Path,
        string RequiredCapability,
        Func<HttpResponseMessage, Task> ValidateResponse,
        Dictionary<string, string>? Query = null,
        Dictionary<string, string>? RequestHeaders = null,
        byte[]? RequestBody = null);
}
