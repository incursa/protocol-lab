// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Incursa.ProtocolLab.Adapter.Contracts;

public interface IProtocolLabAdapterClient
{
    Task<AdapterHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default);

    Task<AdapterManifestResponse> GetManifestAsync(CancellationToken cancellationToken = default);

    Task<AdapterSessionResource> CreateSessionAsync(AdapterSessionCreateRequest? request = null, CancellationToken cancellationToken = default);

    Task<AdapterSessionResource> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<AdapterOperationResult> PrepareAsync(string sessionId, AdapterPrepareRequest request, CancellationToken cancellationToken = default);

    Task<AdapterOperationResult> StartAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<AdapterStatusResponse> GetStatusAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<AdapterEndpointsResponse> GetEndpointsAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<AdapterMetricsResponse> GetMetricsAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<AdapterArtifactsResponse> GetArtifactsAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<AdapterOperationResult> StopAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<AdapterOperationResult> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}

public sealed class ProtocolLabAdapterClient : IProtocolLabAdapterClient
{
    private const string ProblemJsonMediaType = "application/problem+json";

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public ProtocolLabAdapterClient(HttpClient httpClient, JsonSerializerOptions? jsonOptions = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _jsonOptions = jsonOptions ?? ProtocolLabAdapterJson.Options;
    }

    public Task<AdapterHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<AdapterHealthResponse>(new HttpRequestMessage(HttpMethod.Get, AdapterRoutes.Health), cancellationToken);
    }

    public Task<AdapterManifestResponse> GetManifestAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<AdapterManifestResponse>(new HttpRequestMessage(HttpMethod.Get, AdapterRoutes.Manifest), cancellationToken);
    }

    public Task<AdapterSessionResource> CreateSessionAsync(AdapterSessionCreateRequest? request = null, CancellationToken cancellationToken = default)
    {
        return SendAsync<AdapterSessionResource>(CreateJsonRequest(HttpMethod.Post, AdapterRoutes.Sessions, request ?? new AdapterSessionCreateRequest()), cancellationToken);
    }

    public Task<AdapterSessionResource> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return SendAsync<AdapterSessionResource>(new HttpRequestMessage(HttpMethod.Get, AdapterRoutes.Session(sessionId)), cancellationToken);
    }

    public Task<AdapterOperationResult> PrepareAsync(string sessionId, AdapterPrepareRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<AdapterOperationResult>(CreateJsonRequest(HttpMethod.Post, AdapterRoutes.Prepare(sessionId), request), cancellationToken);
    }

    public Task<AdapterOperationResult> StartAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return SendAsync<AdapterOperationResult>(new HttpRequestMessage(HttpMethod.Post, AdapterRoutes.Start(sessionId)), cancellationToken);
    }

    public Task<AdapterStatusResponse> GetStatusAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return SendAsync<AdapterStatusResponse>(new HttpRequestMessage(HttpMethod.Get, AdapterRoutes.Status(sessionId)), cancellationToken);
    }

    public Task<AdapterEndpointsResponse> GetEndpointsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return SendAsync<AdapterEndpointsResponse>(new HttpRequestMessage(HttpMethod.Get, AdapterRoutes.Endpoints(sessionId)), cancellationToken);
    }

    public Task<AdapterMetricsResponse> GetMetricsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return SendAsync<AdapterMetricsResponse>(new HttpRequestMessage(HttpMethod.Get, AdapterRoutes.Metrics(sessionId)), cancellationToken);
    }

    public Task<AdapterArtifactsResponse> GetArtifactsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return SendAsync<AdapterArtifactsResponse>(new HttpRequestMessage(HttpMethod.Get, AdapterRoutes.Artifacts(sessionId)), cancellationToken);
    }

    public Task<AdapterOperationResult> StopAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return SendAsync<AdapterOperationResult>(new HttpRequestMessage(HttpMethod.Post, AdapterRoutes.Stop(sessionId)), cancellationToken);
    }

    public Task<AdapterOperationResult> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return SendDeleteAsync(sessionId, cancellationToken);
    }

    private async Task<AdapterOperationResult> SendDeleteAsync(string sessionId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, AdapterRoutes.Session(sessionId));
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var rawContent = await ReadRawContentAsync(response, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            if (string.IsNullOrWhiteSpace(rawContent))
            {
                return new AdapterOperationResult
                {
                    Category = AdapterOperationResultCategory.Succeeded,
                    Message = "Session deleted.",
                    Details = new Dictionary<string, JsonElement>
                    {
                        ["sessionId"] = JsonSerializer.SerializeToElement(sessionId, _jsonOptions)
                    }
                };
            }

            var resource = Deserialize<AdapterSessionResource>(rawContent, nameof(DeleteSessionAsync), response.StatusCode);
            return resource.Operation ?? new AdapterOperationResult
            {
                Category = AdapterOperationResultCategory.Succeeded,
                Message = "Session deleted."
            };
        }

        ThrowForFailureResponse(response, rawContent, nameof(DeleteSessionAsync));
        throw new UnreachableException();
    }

    private async Task<TResponse> SendAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var rawContent = await ReadRawContentAsync(response, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                if (string.IsNullOrWhiteSpace(rawContent))
                {
                    throw new ProtocolLabAdapterProtocolException(
                        $"The adapter returned a successful response without a body for {request.Method} {request.RequestUri}.",
                        request.Method.Method,
                        response.StatusCode,
                        rawContent);
                }

                return Deserialize<TResponse>(rawContent, request.Method.Method, response.StatusCode);
            }

            ThrowForFailureResponse(response, rawContent, request.Method.Method);
            throw new UnreachableException();
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
            return JsonSerializer.Deserialize<TResponse>(rawContent, _jsonOptions)
                ?? throw new ProtocolLabAdapterProtocolException(
                    $"The adapter returned an empty JSON document for {operation}.",
                    operation,
                    statusCode,
                    rawContent);
        }
        catch (JsonException exception)
        {
            throw new ProtocolLabAdapterProtocolException(
                $"The adapter returned malformed JSON for {operation}.",
                operation,
                statusCode,
                rawContent,
                exception);
        }
    }

    private void ThrowForFailureResponse(HttpResponseMessage response, string rawContent, string operation)
    {
        AdapterProblemDetails? problem = null;

        if (!string.IsNullOrWhiteSpace(rawContent) &&
            TryDeserializeProblem(rawContent, out var parsedProblem))
        {
            problem = parsedProblem;
        }

        if (problem is not null)
        {
            throw new ProtocolLabAdapterProblemException(
                $"The adapter returned a problem response for {operation}.",
                operation,
                response.StatusCode,
                problem,
                rawContent);
        }

        throw new ProtocolLabAdapterProtocolException(
            $"The adapter returned HTTP {(int)response.StatusCode} for {operation} without a parseable problem document.",
            operation,
            response.StatusCode,
            rawContent);
    }

    private bool TryDeserializeProblem(string rawContent, out AdapterProblemDetails? problem)
    {
        try
        {
            problem = JsonSerializer.Deserialize<AdapterProblemDetails>(rawContent, _jsonOptions);
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

    private sealed class UnreachableException : Exception
    {
    }
}

public static class AdapterRoutes
{
    public const string Prefix = "/protocol-lab/adapter/v1";
    public const string Health = Prefix + "/health";
    public const string Manifest = Prefix + "/manifest";
    public const string Sessions = Prefix + "/sessions";

    public static string Session(string sessionId) => $"{Sessions}/{sessionId}";

    public static string Prepare(string sessionId) => $"{Session(sessionId)}/prepare";

    public static string Start(string sessionId) => $"{Session(sessionId)}/start";

    public static string Status(string sessionId) => $"{Session(sessionId)}/status";

    public static string Endpoints(string sessionId) => $"{Session(sessionId)}/endpoints";

    public static string Metrics(string sessionId) => $"{Session(sessionId)}/metrics";

    public static string Artifacts(string sessionId) => $"{Session(sessionId)}/artifacts";

    public static string Stop(string sessionId) => $"{Session(sessionId)}/stop";
}

public abstract class ProtocolLabAdapterClientException : Exception
{
    protected ProtocolLabAdapterClientException(string message, string operation, HttpStatusCode? statusCode, string rawContent, Exception? innerException = null)
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

public sealed class ProtocolLabAdapterProtocolException : ProtocolLabAdapterClientException
{
    public ProtocolLabAdapterProtocolException(string message, string operation, HttpStatusCode? statusCode, string rawContent, Exception? innerException = null)
        : base(message, operation, statusCode, rawContent, innerException)
    {
    }
}

public sealed class ProtocolLabAdapterProblemException : ProtocolLabAdapterClientException
{
    public ProtocolLabAdapterProblemException(string message, string operation, HttpStatusCode statusCode, AdapterProblemDetails problem, string rawContent)
        : base(message, operation, statusCode, rawContent)
    {
        Problem = problem;
    }

    public AdapterProblemDetails Problem { get; }
}
