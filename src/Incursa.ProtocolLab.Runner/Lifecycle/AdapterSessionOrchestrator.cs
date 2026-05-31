// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net.Http.Json;
using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

internal static class AdapterSessionOrchestrator
{
    private const string AdapterContract = "adapter-v1";

    public static async Task<AdapterSessionHandle> StartAsync(
        string root,
        RunCell cell,
        string controlPlaneBaseUrl,
        ArtifactPaths paths,
        string runId,
        string cellId,
        TargetExecutionResult controlPlaneResult)
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(controlPlaneBaseUrl.TrimEnd('/') + "/", UriKind.Absolute)
        };

        var client = new ProtocolLabAdapterClient(httpClient);
        string? sessionId = null;
        try
        {
            var health = await client.GetHealthAsync();
            await WriteJsonAsync(paths.AdapterHealthJson, health);

            var manifest = await client.GetManifestAsync();
            await WriteJsonAsync(paths.AdapterManifestJson, manifest);

            var requestedSessionId = BuildSessionId(cell);
            var sessionCreate = await client.CreateSessionAsync(new AdapterSessionCreateRequest
            {
                RequestedSessionId = requestedSessionId,
                RunId = runId,
                CellId = cellId,
                SessionLabel = $"{cell.Implementation.Id}/{cell.Scenario.Id}/{cell.Protocol}",
                Extensions = BuildExtensions(
                    ("implementationId", cell.Implementation.Id),
                    ("implementationName", cell.Implementation.Name),
                    ("scenarioId", cell.Scenario.Id),
                    ("scenarioFamily", cell.Scenario.Family),
                    ("scenarioVersion", cell.Scenario.Version),
                    ("protocol", cell.Protocol),
                    ("role", cell.Scenario.ImplementationRole),
                    ("networkProfile", cell.NetworkProfile),
                    ("runId", runId),
                    ("cellId", cellId))
            });
            await WriteJsonAsync(paths.AdapterSessionCreateJson, sessionCreate);

            sessionId = sessionCreate.Session.SessionId;
            var result = new TargetExecutionResult
            {
                Status = TargetExecutionStatuses.Started,
                TargetExecutionMode = controlPlaneResult.TargetExecutionMode,
                TargetContract = AdapterContract,
                Started = true,
                StartTimeUtc = DateTimeOffset.UtcNow,
                TargetEffectiveBaseUrl = controlPlaneBaseUrl,
                AdapterControlPlaneBaseUrl = controlPlaneBaseUrl,
                AdapterSessionId = sessionId,
                AdapterScenarioId = cell.Scenario.Id,
                AdapterScenarioVersion = cell.Scenario.Version,
                Warnings = [.. controlPlaneResult.Warnings]
            };

            var prepareRequest = new AdapterPrepareRequest
            {
                ScenarioId = cell.Scenario.Id,
                ScenarioVersion = cell.Scenario.Version,
                Role = cell.Scenario.ImplementationRole,
                ScenarioDocument = JsonSerializer.SerializeToElement(cell.Scenario, ResultJson.Options),
                RequestedEndpointBindings = BuildEndpointBindings(cell),
                RunId = runId,
                CellId = cellId,
                ArtifactOutputExpectations = BuildArtifactExpectations(cell),
                Extensions = BuildExtensions(
                    ("implementationId", cell.Implementation.Id),
                    ("implementationName", cell.Implementation.Name),
                    ("scenarioId", cell.Scenario.Id),
                    ("scenarioFamily", cell.Scenario.Family),
                    ("scenarioVersion", cell.Scenario.Version),
                    ("protocol", cell.Protocol),
                    ("role", cell.Scenario.ImplementationRole),
                    ("networkProfile", cell.NetworkProfile),
                    ("runId", runId),
                    ("cellId", cellId),
                    ("targetContract", controlPlaneResult.TargetContract ?? AdapterContract))
            };

            var prepare = await client.PrepareAsync(sessionId, prepareRequest);
            await WriteJsonAsync(paths.AdapterPrepareJson, prepare);
            result = MergeWarnings(result, prepare.Warnings);
            await AppendStatusSnapshotAsync(paths.AdapterStatusJsonl, await client.GetStatusAsync(sessionId));

            if (prepare.Category == AdapterOperationResultCategory.Unsupported)
            {
                return FinalizeUnsupported(result, prepare.Message ?? "Adapter reported unsupported scenario.", controlPlaneBaseUrl, sessionId, manifest, paths, httpClient, client, controlPlaneResult);
            }

            if (prepare.Category == AdapterOperationResultCategory.Failed)
            {
                return FinalizeFailure(result, prepare.Message ?? "Adapter prepare failed.", controlPlaneBaseUrl, sessionId, manifest, paths, httpClient, client, controlPlaneResult);
            }

            var start = await client.StartAsync(sessionId);
            await WriteJsonAsync(paths.AdapterStartJson, start);
            result = MergeWarnings(result, start.Warnings);
            await AppendStatusSnapshotAsync(paths.AdapterStatusJsonl, await client.GetStatusAsync(sessionId));

            if (start.Category == AdapterOperationResultCategory.Unsupported)
            {
                return FinalizeUnsupported(result, start.Message ?? "Adapter reported unsupported scenario.", controlPlaneBaseUrl, sessionId, manifest, paths, httpClient, client, controlPlaneResult);
            }

            if (start.Category == AdapterOperationResultCategory.Failed)
            {
                return FinalizeFailure(result, start.Message ?? "Adapter start failed.", controlPlaneBaseUrl, sessionId, manifest, paths, httpClient, client, controlPlaneResult);
            }

            var status = await client.GetStatusAsync(sessionId);
            await AppendStatusSnapshotAsync(paths.AdapterStatusJsonl, status);

            var endpoints = await client.GetEndpointsAsync(sessionId);
            await WriteJsonAsync(paths.AdapterEndpointsJson, endpoints);
            result = MergeWarnings(result, endpoints.Operation?.Warnings);

            var metrics = await client.GetMetricsAsync(sessionId);
            await WriteJsonAsync(paths.AdapterMetricsJson, metrics);
            result = MergeWarnings(result, metrics.Operation?.Warnings);

            var artifacts = await client.GetArtifactsAsync(sessionId);
            await WriteJsonAsync(paths.AdapterArtifactsJson, artifacts);
            result = MergeWarnings(result, artifacts.Operation?.Warnings);

            var finalStatus = await client.GetStatusAsync(sessionId);
            await AppendStatusSnapshotAsync(paths.AdapterStatusJsonl, finalStatus);

            var selectedEndpoint = SelectEndpoint(endpoints.Endpoints);
            var endpointTypes = endpoints.Endpoints
                .Select(endpoint => endpoint.Protocol)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var effectiveBaseUrl = selectedEndpoint is null ? null : ResolveEndpointBaseUrl(selectedEndpoint);
            if (selectedEndpoint is null || effectiveBaseUrl is null)
            {
                return new AdapterSessionHandle(
                    httpClient,
                    client,
                    sessionId,
                    controlPlaneBaseUrl,
                    paths,
                    result with
                    {
                        Status = TargetExecutionStatuses.Unsupported,
                        Unsupported = true,
                        Started = true,
                        Ready = false,
                        TargetEffectiveBaseUrl = null,
                        AdapterEndpointTypes = endpointTypes,
                        Errors = [.. result.Errors, "Adapter did not return a consumable HTTP/HTTPS endpoint."]
                    },
                    null);
            }

            if (!IsConsumableHttpEndpoint(selectedEndpoint))
            {
                return FinalizeUnsupported(
                    result with
                    {
                        Started = true,
                        Ready = false,
                        TargetEffectiveBaseUrl = effectiveBaseUrl,
                        AdapterEndpointTypes = endpointTypes,
                        Warnings = MergeCollections(result.Warnings, finalStatus.Readiness.Warnings)
                    },
                    $"Adapter returned a non-HTTP endpoint scheme '{selectedEndpoint.Scheme}'.",
                    controlPlaneBaseUrl,
                    sessionId,
                    manifest,
                    paths,
                    httpClient,
                    client,
                    controlPlaneResult,
                    effectiveBaseUrl,
                    endpointTypes);
            }

            if (finalStatus.Readiness.Status == AdapterReadinessStatus.Unsupported)
            {
                return FinalizeUnsupported(
                    result with
                    {
                        Started = true,
                        Ready = false,
                        TargetEffectiveBaseUrl = effectiveBaseUrl,
                        AdapterEndpointTypes = endpointTypes,
                        Warnings = MergeCollections(result.Warnings, finalStatus.Readiness.Warnings)
                    },
                    finalStatus.Readiness.Message ?? "Adapter reported unsupported readiness.",
                    controlPlaneBaseUrl,
                    sessionId,
                    manifest,
                    paths,
                    httpClient,
                    client,
                    controlPlaneResult,
                    effectiveBaseUrl,
                    endpointTypes);
            }

            if (finalStatus.Readiness.Status == AdapterReadinessStatus.NotReady ||
                finalStatus.Readiness.Status == AdapterReadinessStatus.Failed)
            {
                return FinalizeFailure(
                    result with
                    {
                        Started = true,
                        Ready = false,
                        TargetEffectiveBaseUrl = effectiveBaseUrl,
                        AdapterEndpointTypes = endpointTypes,
                        Warnings = MergeCollections(result.Warnings, finalStatus.Readiness.Warnings)
                    },
                    finalStatus.Readiness.Message ?? "Adapter endpoint did not become ready.",
                    controlPlaneBaseUrl,
                    sessionId,
                    manifest,
                    paths,
                    httpClient,
                    client,
                    controlPlaneResult,
                    effectiveBaseUrl,
                    endpointTypes);
            }

            return new AdapterSessionHandle(
                httpClient,
                client,
                sessionId,
                controlPlaneBaseUrl,
                paths,
                result with
                {
                    Status = TargetExecutionStatuses.Ready,
                    Ready = true,
                    StartTimeUtc = sessionCreate.Session.CreatedAt ?? DateTimeOffset.UtcNow,
                    ReadyTimeUtc = finalStatus.Readiness.ObservedAt ?? DateTimeOffset.UtcNow,
                    TargetEffectiveBaseUrl = effectiveBaseUrl,
                    AdapterEndpointTypes = endpointTypes,
                    Warnings = MergeCollections(result.Warnings, finalStatus.Readiness.Warnings)
                },
                effectiveBaseUrl);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                await CleanupFailedSessionAsync(httpClient, client, sessionId, paths, controlPlaneBaseUrl);
            }

            httpClient.Dispose();
            throw;
        }
    }

    private static AdapterSessionHandle FinalizeUnsupported(
        TargetExecutionResult result,
        string reason,
        string controlPlaneBaseUrl,
        string sessionId,
        AdapterManifestResponse manifest,
        ArtifactPaths paths,
        HttpClient httpClient,
        ProtocolLabAdapterClient client,
        TargetExecutionResult controlPlaneResult,
        string? effectiveBaseUrl = null,
        IReadOnlyList<string>? endpointTypes = null)
    {
        return new AdapterSessionHandle(
            httpClient,
            client,
            sessionId,
            controlPlaneBaseUrl,
            paths,
            result with
            {
                Status = TargetExecutionStatuses.Unsupported,
                Unsupported = true,
                Ready = false,
                TargetEffectiveBaseUrl = effectiveBaseUrl ?? result.TargetEffectiveBaseUrl,
                AdapterEndpointTypes = endpointTypes ?? result.AdapterEndpointTypes,
                Errors = MergeCollections(result.Errors, [reason]),
                Warnings = MergeCollections(result.Warnings, [reason])
            },
            effectiveBaseUrl ?? result.TargetEffectiveBaseUrl);
    }

    private static AdapterSessionHandle FinalizeFailure(
        TargetExecutionResult result,
        string reason,
        string controlPlaneBaseUrl,
        string sessionId,
        AdapterManifestResponse manifest,
        ArtifactPaths paths,
        HttpClient httpClient,
        ProtocolLabAdapterClient client,
        TargetExecutionResult controlPlaneResult,
        string? effectiveBaseUrl = null,
        IReadOnlyList<string>? endpointTypes = null)
    {
        return new AdapterSessionHandle(
            httpClient,
            client,
            sessionId,
            controlPlaneBaseUrl,
            paths,
            result with
            {
                Status = TargetExecutionStatuses.Failed,
                Failed = true,
                Ready = false,
                TargetEffectiveBaseUrl = effectiveBaseUrl ?? result.TargetEffectiveBaseUrl,
                AdapterEndpointTypes = endpointTypes ?? result.AdapterEndpointTypes,
                Errors = MergeCollections(result.Errors, [reason])
            },
            effectiveBaseUrl ?? result.TargetEffectiveBaseUrl);
    }

    private static IReadOnlyList<AdapterEndpointBinding> BuildEndpointBindings(RunCell cell)
    {
        var bindings = new List<AdapterEndpointBinding>();

        if (cell.Scenario.Endpoint is not null)
        {
            bindings.Add(new AdapterEndpointBinding
            {
                BindingId = "primary",
                Purpose = cell.Scenario.ImplementationRole,
                EndpointType = cell.Protocol,
                Required = true,
                Metadata = BuildExtensions(
                    ("method", cell.Scenario.Endpoint.Method),
                    ("path", cell.Scenario.Endpoint.Path),
                    ("expectedStatus", cell.Scenario.Endpoint.ExpectedStatus.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            });
        }

        return bindings;
    }

    private static IReadOnlyList<AdapterArtifactExpectation> BuildArtifactExpectations(RunCell cell)
    {
        return cell.Scenario.ArtifactRequirements
            .Select(requirement => new AdapterArtifactExpectation
            {
                ArtifactType = requirement,
                Required = true
            })
            .ToArray();
    }

    private static AdapterEndpoint? SelectEndpoint(IReadOnlyList<AdapterEndpoint> endpoints)
    {
        var endpoint = endpoints.FirstOrDefault(candidate =>
            string.Equals(candidate.Scheme, "http", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.Scheme, "https", StringComparison.OrdinalIgnoreCase));

        if (endpoint is null)
        {
            endpoint = endpoints.FirstOrDefault();
        }

        return endpoint;
    }

    private static string? ResolveEndpointBaseUrl(AdapterEndpoint endpoint)
    {
        var baseUrl = $"{endpoint.Scheme}://{endpoint.Host}:{endpoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (string.IsNullOrWhiteSpace(endpoint.Path) || endpoint.Path == "/")
        {
            return baseUrl;
        }

        var path = endpoint.Path.StartsWith("/", StringComparison.Ordinal) ? endpoint.Path : "/" + endpoint.Path;
        return baseUrl + path;
    }

    private static bool IsConsumableHttpEndpoint(AdapterEndpoint endpoint)
    {
        return string.Equals(endpoint.Scheme, "http", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(endpoint.Scheme, "https", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(endpoint.Scheme, "quic", StringComparison.OrdinalIgnoreCase);
    }

    private static TargetExecutionResult MergeWarnings(TargetExecutionResult result, IReadOnlyList<string>? warnings)
    {
        return warnings is null || warnings.Count == 0
            ? result
            : result with { Warnings = MergeCollections(result.Warnings, warnings) };
    }

    private static List<string> MergeCollections(IEnumerable<string> left, IEnumerable<string> right)
    {
        return left
            .Concat(right)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, JsonElement> BuildExtensions(params (string Key, string? Value)[] values)
    {
        var extensions = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            extensions[key] = JsonSerializer.SerializeToElement(value, ResultJson.Options);
        }

        return extensions;
    }

    private static string BuildSessionId(RunCell cell)
    {
        return cell.Identity.ToSlug();
    }

    private static async Task WriteJsonAsync<T>(string path, T value)
    {
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, ProtocolLabAdapterJson.Options));
    }

    private static async Task AppendStatusSnapshotAsync(string path, AdapterStatusResponse response)
    {
        await File.AppendAllTextAsync(path, JsonSerializer.Serialize(response, ProtocolLabAdapterJson.Options) + Environment.NewLine);
    }

    private static async Task CleanupFailedSessionAsync(
        HttpClient httpClient,
        ProtocolLabAdapterClient client,
        string sessionId,
        ArtifactPaths paths,
        string controlPlaneBaseUrl)
    {
        try
        {
            var stop = await client.StopAsync(sessionId);
            await File.WriteAllTextAsync(paths.AdapterStopJson, JsonSerializer.Serialize(stop, ProtocolLabAdapterJson.Options));
        }
        catch (Exception ex) when (ex is ProtocolLabAdapterClientException or HttpRequestException or TaskCanceledException)
        {
            await File.WriteAllTextAsync(paths.AdapterStopJson, ex.ToString());
        }

        try
        {
            var delete = await client.DeleteSessionAsync(sessionId);
            await File.WriteAllTextAsync(paths.AdapterDeleteJson, JsonSerializer.Serialize(delete, ProtocolLabAdapterJson.Options));
        }
        catch (Exception ex) when (ex is ProtocolLabAdapterClientException or HttpRequestException or TaskCanceledException)
        {
            await File.WriteAllTextAsync(paths.AdapterDeleteJson, ex.ToString());
        }

        httpClient.Dispose();
        _ = controlPlaneBaseUrl;
    }
}

internal sealed class AdapterSessionHandle : IAsyncDisposable
{
    private readonly HttpClient httpClient;
    private readonly ProtocolLabAdapterClient client;
    private readonly string sessionId;
    private readonly string controlPlaneBaseUrl;
    private readonly ArtifactPaths paths;
    private bool disposed;

    public AdapterSessionHandle(
        HttpClient httpClient,
        ProtocolLabAdapterClient client,
        string sessionId,
        string controlPlaneBaseUrl,
        ArtifactPaths paths,
        TargetExecutionResult result,
        string? protocolBaseUrl)
    {
        this.httpClient = httpClient;
        this.client = client;
        this.sessionId = sessionId;
        this.controlPlaneBaseUrl = controlPlaneBaseUrl;
        this.paths = paths;
        Result = result;
        ProtocolBaseUrl = protocolBaseUrl ?? result.TargetEffectiveBaseUrl;
    }

    public TargetExecutionResult Result { get; private set; }

    public string? ProtocolBaseUrl { get; }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await CleanupAsync();
    }

    private async Task CleanupAsync()
    {
        try
        {
            var stop = await client.StopAsync(sessionId);
            await File.WriteAllTextAsync(paths.AdapterStopJson, JsonSerializer.Serialize(stop, ProtocolLabAdapterJson.Options));
        }
        catch (Exception ex) when (ex is ProtocolLabAdapterClientException or HttpRequestException or TaskCanceledException)
        {
            await File.WriteAllTextAsync(paths.AdapterStopJson, ex.ToString());
        }

        try
        {
            var delete = await client.DeleteSessionAsync(sessionId);
            await File.WriteAllTextAsync(paths.AdapterDeleteJson, JsonSerializer.Serialize(delete, ProtocolLabAdapterJson.Options));
        }
        catch (Exception ex) when (ex is ProtocolLabAdapterClientException or HttpRequestException or TaskCanceledException)
        {
            await File.WriteAllTextAsync(paths.AdapterDeleteJson, ex.ToString());
        }

        Result = Result with
        {
            Warnings = Result.Warnings.Concat([$"adapter-control-plane-cleanup:{controlPlaneBaseUrl}"]).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };

        httpClient.Dispose();
    }
}
