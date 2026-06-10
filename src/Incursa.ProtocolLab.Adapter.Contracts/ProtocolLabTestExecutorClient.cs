// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Incursa.ProtocolLab.Adapter.Contracts;

public interface IProtocolLabTestExecutorClient
{
    Task<TestExecutorHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default);

    Task<TestExecutorManifestResponse> GetManifestAsync(CancellationToken cancellationToken = default);

    Task<TestExecutorSessionResource> CreateSessionAsync(TestExecutorSessionCreateRequest? request = null, CancellationToken cancellationToken = default);

    Task<TestExecutorSessionResource> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<TestExecutorOperationResult> PrepareAsync(string sessionId, TestExecutorPrepareRequest request, CancellationToken cancellationToken = default);

    Task<TestExecutorOperationResult> StartAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<TestExecutorStatusResponse> GetStatusAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<TestExecutorMetricsResponse> GetMetricsAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<TestExecutorArtifactsResponse> GetArtifactsAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<TestExecutorOperationResult> StopAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<TestExecutorOperationResult> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}

public sealed class ProtocolLabTestExecutorClient : IProtocolLabTestExecutorClient
{
    private const string ProblemJsonMediaType = "application/problem+json";

    private readonly HttpClient httpClient;
    private readonly JsonSerializerOptions jsonOptions;

    public ProtocolLabTestExecutorClient(HttpClient httpClient, JsonSerializerOptions? jsonOptions = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.jsonOptions = jsonOptions ?? ProtocolLabAdapterJson.Options;
    }

    public Task<TestExecutorHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<TestExecutorHealthResponse>(new HttpRequestMessage(HttpMethod.Get, TestExecutorRoutes.Health), cancellationToken);
    }

    public Task<TestExecutorManifestResponse> GetManifestAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<TestExecutorManifestResponse>(new HttpRequestMessage(HttpMethod.Get, TestExecutorRoutes.Manifest), cancellationToken);
    }

    public Task<TestExecutorSessionResource> CreateSessionAsync(TestExecutorSessionCreateRequest? request = null, CancellationToken cancellationToken = default)
    {
        return SendAsync<TestExecutorSessionResource>(CreateJsonRequest(HttpMethod.Post, TestExecutorRoutes.Sessions, request ?? new TestExecutorSessionCreateRequest()), cancellationToken);
    }

    public Task<TestExecutorSessionResource> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return SendAsync<TestExecutorSessionResource>(new HttpRequestMessage(HttpMethod.Get, TestExecutorRoutes.Session(sessionId)), cancellationToken);
    }

    public Task<TestExecutorOperationResult> PrepareAsync(string sessionId, TestExecutorPrepareRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<TestExecutorOperationResult>(CreateJsonRequest(HttpMethod.Post, TestExecutorRoutes.Prepare(sessionId), request), cancellationToken);
    }

    public Task<TestExecutorOperationResult> StartAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return SendAsync<TestExecutorOperationResult>(new HttpRequestMessage(HttpMethod.Post, TestExecutorRoutes.Start(sessionId)), cancellationToken);
    }

    public Task<TestExecutorStatusResponse> GetStatusAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return SendAsync<TestExecutorStatusResponse>(new HttpRequestMessage(HttpMethod.Get, TestExecutorRoutes.Status(sessionId)), cancellationToken);
    }

    public Task<TestExecutorMetricsResponse> GetMetricsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return SendAsync<TestExecutorMetricsResponse>(new HttpRequestMessage(HttpMethod.Get, TestExecutorRoutes.Metrics(sessionId)), cancellationToken);
    }

    public Task<TestExecutorArtifactsResponse> GetArtifactsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return SendAsync<TestExecutorArtifactsResponse>(new HttpRequestMessage(HttpMethod.Get, TestExecutorRoutes.Artifacts(sessionId)), cancellationToken);
    }

    public Task<TestExecutorOperationResult> StopAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return SendAsync<TestExecutorOperationResult>(new HttpRequestMessage(HttpMethod.Post, TestExecutorRoutes.Stop(sessionId)), cancellationToken);
    }

    public async Task<TestExecutorOperationResult> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, TestExecutorRoutes.Session(sessionId));
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var rawContent = await ReadRawContentAsync(response, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            if (string.IsNullOrWhiteSpace(rawContent))
            {
                return new TestExecutorOperationResult
                {
                    Category = TestExecutorOperationResultCategory.Succeeded,
                    Message = "Session deleted.",
                    Details = new Dictionary<string, JsonElement>
                    {
                        ["sessionId"] = JsonSerializer.SerializeToElement(sessionId, jsonOptions)
                    }
                };
            }

            var resource = Deserialize<TestExecutorSessionResource>(rawContent, nameof(DeleteSessionAsync), response.StatusCode);
            return resource.Operation ?? new TestExecutorOperationResult
            {
                Category = TestExecutorOperationResultCategory.Succeeded,
                Message = "Session deleted."
            };
        }

        ThrowForFailureResponse(response, rawContent, nameof(DeleteSessionAsync));
        throw new TestExecutorUnreachableException();
    }

    private async Task<TResponse> SendAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        {
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var rawContent = await ReadRawContentAsync(response, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                if (string.IsNullOrWhiteSpace(rawContent))
                {
                    throw new ProtocolLabTestExecutorProtocolException(
                        $"The test executor returned a successful response without a body for {request.Method} {request.RequestUri}.",
                        request.Method.Method,
                        response.StatusCode,
                        rawContent);
                }

                return Deserialize<TResponse>(rawContent, request.Method.Method, response.StatusCode);
            }

            ThrowForFailureResponse(response, rawContent, request.Method.Method);
            throw new TestExecutorUnreachableException();
        }
    }

    private async Task<string> ReadRawContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            return string.Empty;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private TResponse Deserialize<TResponse>(string rawContent, string operation, HttpStatusCode? statusCode)
    {
        try
        {
            return JsonSerializer.Deserialize<TResponse>(rawContent, jsonOptions)
                ?? throw new ProtocolLabTestExecutorProtocolException(
                    $"The test executor returned an empty JSON document for {operation}.",
                    operation,
                    statusCode,
                    rawContent);
        }
        catch (JsonException exception)
        {
            throw new ProtocolLabTestExecutorProtocolException(
                $"The test executor returned malformed JSON for {operation}.",
                operation,
                statusCode,
                rawContent,
                exception);
        }
    }

    private void ThrowForFailureResponse(HttpResponseMessage response, string rawContent, string operation)
    {
        TestExecutorProblemDetails? problem = null;

        if (!string.IsNullOrWhiteSpace(rawContent) && TryDeserializeProblem(rawContent, out var parsedProblem))
        {
            problem = parsedProblem;
        }

        if (problem is not null)
        {
            throw new ProtocolLabTestExecutorProblemException(
                $"The test executor returned a problem response for {operation}.",
                operation,
                response.StatusCode,
                problem,
                rawContent);
        }

        throw new ProtocolLabTestExecutorProtocolException(
            $"The test executor returned HTTP {(int)response.StatusCode} for {operation} without a parseable problem document.",
            operation,
            response.StatusCode,
            rawContent);
    }

    private bool TryDeserializeProblem(string rawContent, out TestExecutorProblemDetails? problem)
    {
        try
        {
            problem = JsonSerializer.Deserialize<TestExecutorProblemDetails>(rawContent, jsonOptions);
            return problem is not null;
        }
        catch (JsonException)
        {
            problem = null;
            return false;
        }
    }

    private static HttpRequestMessage CreateJsonRequest<TBody>(HttpMethod method, string path, TBody body)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body, options: ProtocolLabAdapterJson.Options)
        };

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ProblemJsonMediaType));
        return request;
    }

    private sealed class TestExecutorUnreachableException : Exception
    {
    }
}

public static class TestExecutorRoutes
{
    public const string Prefix = "/protocol-lab/test-executor/v1";
    public const string Health = Prefix + "/health";
    public const string Manifest = Prefix + "/manifest";
    public const string Sessions = Prefix + "/sessions";

    public static string Session(string sessionId) => $"{Sessions}/{sessionId}";

    public static string Prepare(string sessionId) => $"{Session(sessionId)}/prepare";

    public static string Start(string sessionId) => $"{Session(sessionId)}/start";

    public static string Status(string sessionId) => $"{Session(sessionId)}/status";

    public static string Metrics(string sessionId) => $"{Session(sessionId)}/metrics";

    public static string Artifacts(string sessionId) => $"{Session(sessionId)}/artifacts";

    public static string Stop(string sessionId) => $"{Session(sessionId)}/stop";
}

public abstract class ProtocolLabTestExecutorClientException : Exception
{
    protected ProtocolLabTestExecutorClientException(string message, string operation, HttpStatusCode? statusCode, string rawContent, Exception? innerException = null)
        : base(message, innerException)
    {
        Operation = operation;
        StatusCode = statusCode;
        RawContent = rawContent;
    }

    public string Operation { get; }

    public HttpStatusCode? StatusCode { get; }

    public string RawContent { get; }
}

public sealed class ProtocolLabTestExecutorProtocolException : ProtocolLabTestExecutorClientException
{
    public ProtocolLabTestExecutorProtocolException(string message, string operation, HttpStatusCode? statusCode, string rawContent, Exception? innerException = null)
        : base(message, operation, statusCode, rawContent, innerException)
    {
    }
}

public sealed class ProtocolLabTestExecutorProblemException : ProtocolLabTestExecutorClientException
{
    public ProtocolLabTestExecutorProblemException(string message, string operation, HttpStatusCode statusCode, TestExecutorProblemDetails problem, string rawContent)
        : base(message, operation, statusCode, rawContent)
    {
        Problem = problem;
    }

    public TestExecutorProblemDetails Problem { get; }
}
