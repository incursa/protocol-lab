// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Adapter.Conformance;

public sealed class AdapterConformanceSuite
{
    public async Task<AdapterConformanceReport> RunAsync(
        Uri controlPlaneBaseUrl,
        AdapterConformanceScenario scenario,
        AdapterConformanceOptions options,
        CancellationToken cancellationToken = default)
    {
        if (controlPlaneBaseUrl is null)
        {
            throw new ArgumentNullException(nameof(controlPlaneBaseUrl));
        }

        if (scenario is null)
        {
            throw new ArgumentNullException(nameof(scenario));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        using var httpClient = options.HttpMessageHandlerFactory is null
            ? new HttpClient()
            : new HttpClient(options.HttpMessageHandlerFactory());

        httpClient.BaseAddress = controlPlaneBaseUrl;
        httpClient.Timeout = options.Timeout;
        var client = new ProtocolLabAdapterClient(httpClient);
        var schemaValidator = new AdapterSchemaValidator(options.SchemaRootPath);
        var steps = new List<AdapterConformanceStepResult>();
        var warnings = new List<string>();
        string? sessionId = null;
        var overall = AdapterConformanceOutcome.Passed;

        async Task<T?> InvokeAsync<T>(
            string step,
            Func<CancellationToken, Task<T>> action,
            string schemaName,
            Func<T, AdapterConformanceOutcome>? classifyUnsupported = null)
        {
            try
            {
                var value = await action(cancellationToken);
                if (options.ValidateSchemas)
                {
                    var schemaResult = await schemaValidator.ValidateObjectAsync(schemaName, value, cancellationToken);
                    if (!schemaResult.IsValid)
                    {
                        steps.Add(new AdapterConformanceStepResult(step, AdapterConformanceOutcome.ContractFailure, "Schema validation failed.", schemaName, schemaResult.SchemaPath, Diagnostics: schemaResult.Errors));
                        overall = Worst(overall, AdapterConformanceOutcome.ContractFailure);
                    }
                }

                if (classifyUnsupported is not null && classifyUnsupported(value) == AdapterConformanceOutcome.Unsupported)
                {
                    steps.Add(new AdapterConformanceStepResult(step, AdapterConformanceOutcome.Unsupported, "Adapter reported unsupported.", schemaName));
                    overall = Worst(overall, AdapterConformanceOutcome.Unsupported);
                }
                else
                {
                    steps.Add(new AdapterConformanceStepResult(step, AdapterConformanceOutcome.Passed, "Step passed.", schemaName));
                }

                return value;
            }
            catch (ProtocolLabAdapterProblemException ex)
            {
                var outcome = ex.Problem.CategoryIsUnsupported() ? AdapterConformanceOutcome.Unsupported : AdapterConformanceOutcome.ContractFailure;
                steps.Add(new AdapterConformanceStepResult(step, outcome, ex.Message, schemaName, StatusCode: ex.StatusCode, Diagnostics: [ex.Problem.ToString()]));
                overall = Worst(overall, outcome);
                return default;
            }
            catch (ProtocolLabAdapterProtocolException ex)
            {
                steps.Add(new AdapterConformanceStepResult(step, AdapterConformanceOutcome.MalformedResponse, ex.Message, schemaName, StatusCode: ex.StatusCode, Diagnostics: [ex.RawContent]));
                overall = Worst(overall, AdapterConformanceOutcome.MalformedResponse);
                return default;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                steps.Add(new AdapterConformanceStepResult(step, AdapterConformanceOutcome.Timeout, ex.Message, schemaName));
                overall = Worst(overall, AdapterConformanceOutcome.Timeout);
                return default;
            }
            catch (HttpRequestException ex)
            {
                steps.Add(new AdapterConformanceStepResult(step, AdapterConformanceOutcome.InfrastructureFailure, ex.Message, schemaName));
                overall = Worst(overall, AdapterConformanceOutcome.InfrastructureFailure);
                return default;
            }
        }

        var health = await InvokeAsync("health", token => client.GetHealthAsync(token), "health");
        if (health is null)
        {
            return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, null, warnings);
        }

        var manifest = await InvokeAsync("manifest", token => client.GetManifestAsync(token), "manifest");
        if (manifest is null)
        {
            return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, null, warnings);
        }

        if (!string.Equals(manifest.VersionCompatibility.ContractVersion, options.SupportedContractVersion, StringComparison.OrdinalIgnoreCase) &&
            !manifest.VersionCompatibility.CompatibleContractVersions.Contains(options.SupportedContractVersion, StringComparer.OrdinalIgnoreCase))
        {
            steps.Add(new AdapterConformanceStepResult(
                "manifest-version",
                AdapterConformanceOutcome.Unsupported,
                $"Adapter contract version '{manifest.VersionCompatibility.ContractVersion}' is not compatible with supported version '{options.SupportedContractVersion}'.",
                "manifest"));
            overall = Worst(overall, AdapterConformanceOutcome.Unsupported);
            return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, null, warnings);
        }

        if (options.ValidateInvalidLifecycleTransition)
        {
            var transitionSession = await InvokeAsync("invalid-transition-create", token => client.CreateSessionAsync(new AdapterSessionCreateRequest
            {
                RequestedSessionId = BuildSessionId(scenario, suffix: "transition"),
                RunId = scenario.RunId,
                CellId = scenario.CellId,
                SessionLabel = scenario.SessionLabel
            }, token), "session-resource");

            if (transitionSession is not null)
            {
                try
                {
                    var startBeforePrepare = await client.StartAsync(transitionSession.Session.SessionId, cancellationToken);
                    if (startBeforePrepare.Category == AdapterOperationResultCategory.Succeeded ||
                        startBeforePrepare.Category == AdapterOperationResultCategory.Pending)
                    {
                        steps.Add(new AdapterConformanceStepResult(
                            "invalid-transition",
                            AdapterConformanceOutcome.ContractFailure,
                            "Start succeeded before prepare; the adapter must reject invalid lifecycle transitions.",
                            "operation"));
                        overall = Worst(overall, AdapterConformanceOutcome.ContractFailure);
                    }
                    else
                    {
                        steps.Add(new AdapterConformanceStepResult(
                            "invalid-transition",
                            AdapterConformanceOutcome.Passed,
                            "Adapter rejected the invalid lifecycle transition.",
                            "operation"));
                    }
                }
                catch (ProtocolLabAdapterProblemException ex)
                {
                    steps.Add(new AdapterConformanceStepResult(
                        "invalid-transition",
                        AdapterConformanceOutcome.Passed,
                        ex.Message,
                        "operation",
                        StatusCode: ex.StatusCode,
                        Diagnostics: [ex.Problem.ToString()]));
                }
                catch (ProtocolLabAdapterProtocolException ex)
                {
                    steps.Add(new AdapterConformanceStepResult(
                        "invalid-transition",
                        AdapterConformanceOutcome.MalformedResponse,
                        ex.Message,
                        "operation",
                        StatusCode: ex.StatusCode,
                        Diagnostics: [ex.RawContent]));
                    overall = Worst(overall, AdapterConformanceOutcome.MalformedResponse);
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    steps.Add(new AdapterConformanceStepResult("invalid-transition", AdapterConformanceOutcome.Timeout, ex.Message, "operation"));
                    overall = Worst(overall, AdapterConformanceOutcome.Timeout);
                }
                catch (HttpRequestException ex)
                {
                    steps.Add(new AdapterConformanceStepResult("invalid-transition", AdapterConformanceOutcome.InfrastructureFailure, ex.Message, "operation"));
                    overall = Worst(overall, AdapterConformanceOutcome.InfrastructureFailure);
                }

                overall = Worst(overall, await CleanupAsync(client, transitionSession.Session.SessionId, steps, cancellationToken));
            }
        }

        var sessionCreate = await InvokeAsync("session-create", token => client.CreateSessionAsync(new AdapterSessionCreateRequest
        {
            RequestedSessionId = BuildSessionId(scenario),
            RunId = scenario.RunId,
            CellId = scenario.CellId,
            SessionLabel = scenario.SessionLabel
        }, token), "session-resource");
        if (sessionCreate is null)
        {
            return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        sessionId = sessionCreate.Session.SessionId;

        var prepare = await InvokeAsync("prepare", token => client.PrepareAsync(sessionId, new AdapterPrepareRequest
        {
            ScenarioId = scenario.ScenarioId,
            ScenarioVersion = scenario.ScenarioVersion,
            Role = scenario.Role,
            ScenarioDocument = JsonSerializer.SerializeToElement(scenario, JsonSerializerOptions.Web),
            RequestedEndpointBindings = scenario.RequestedEndpointBindings,
            RunId = scenario.RunId,
            CellId = scenario.CellId,
            ArtifactOutputExpectations = scenario.ArtifactOutputExpectations,
            Extensions = scenario.Extensions
        }, token), "operation", value => value.Category == AdapterOperationResultCategory.Unsupported ? AdapterConformanceOutcome.Unsupported : AdapterConformanceOutcome.Passed);

        if (prepare is null)
        {
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken));
            return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        if (prepare.Category == AdapterOperationResultCategory.Unsupported)
        {
            steps.Add(new AdapterConformanceStepResult("prepare-unsupported", AdapterConformanceOutcome.Unsupported, prepare.Message ?? "Adapter reported unsupported scenario.", "operation"));
            overall = Worst(overall, AdapterConformanceOutcome.Unsupported);
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken));
            return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        if (prepare.Category == AdapterOperationResultCategory.Failed || prepare.Category == AdapterOperationResultCategory.Rejected)
        {
            steps.Add(new AdapterConformanceStepResult("prepare-failed", AdapterConformanceOutcome.ContractFailure, prepare.Message ?? "Adapter prepare failed.", "operation"));
            overall = Worst(overall, AdapterConformanceOutcome.ContractFailure);
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken));
            return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        var start = await InvokeAsync("start", token => client.StartAsync(sessionId, token), "operation");
        if (start is null)
        {
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken));
            return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        if (start.Category == AdapterOperationResultCategory.Unsupported)
        {
            steps.Add(new AdapterConformanceStepResult("start-unsupported", AdapterConformanceOutcome.Unsupported, start.Message ?? "Adapter reported unsupported scenario.", "operation"));
            overall = Worst(overall, AdapterConformanceOutcome.Unsupported);
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken));
            return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        if (start.Category == AdapterOperationResultCategory.Failed || start.Category == AdapterOperationResultCategory.Rejected)
        {
            steps.Add(new AdapterConformanceStepResult("start-failed", AdapterConformanceOutcome.ContractFailure, start.Message ?? "Adapter start failed.", "operation"));
            overall = Worst(overall, AdapterConformanceOutcome.ContractFailure);
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken));
            return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        var status = await InvokeAsync("status", token => client.GetStatusAsync(sessionId, token), "status");
        if (status is null)
        {
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken));
            return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        if (status.Readiness.Status == AdapterReadinessStatus.Unsupported)
        {
            steps.Add(new AdapterConformanceStepResult("readiness-unsupported", AdapterConformanceOutcome.Unsupported, status.Readiness.Message ?? "Adapter reported unsupported readiness.", "status"));
            overall = Worst(overall, AdapterConformanceOutcome.Unsupported);
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken));
            return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        if (status.Readiness.Status == AdapterReadinessStatus.NotReady)
        {
            steps.Add(new AdapterConformanceStepResult("readiness-not-ready", AdapterConformanceOutcome.Timeout, status.Readiness.Message ?? "Adapter endpoint is not ready.", "status"));
            overall = Worst(overall, AdapterConformanceOutcome.Timeout);
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken));
            return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        if (status.Readiness.Status == AdapterReadinessStatus.Failed)
        {
            steps.Add(new AdapterConformanceStepResult("readiness-failed", AdapterConformanceOutcome.ContractFailure, status.Readiness.Message ?? "Adapter endpoint readiness failed.", "status"));
            overall = Worst(overall, AdapterConformanceOutcome.ContractFailure);
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken));
            return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        var endpoints = await InvokeAsync("endpoints", token => client.GetEndpointsAsync(sessionId, token), "endpoints");
        if (endpoints is null)
        {
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken));
            return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        if (endpoints.Endpoints.Count == 0)
        {
            steps.Add(new AdapterConformanceStepResult("endpoints-empty", AdapterConformanceOutcome.ContractFailure, "Adapter returned no endpoints.", "endpoints"));
            overall = Worst(overall, AdapterConformanceOutcome.ContractFailure);
        }

        var metrics = await InvokeAsync("metrics", token => client.GetMetricsAsync(sessionId, token), "metrics");
        if (metrics is null)
        {
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken));
            return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        var artifacts = await InvokeAsync("artifacts", token => client.GetArtifactsAsync(sessionId, token), "artifacts");
        if (artifacts is null)
        {
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken));
            return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken));

        if (options.ValidateDeleteIdempotency)
        {
            var deleteAgain = await InvokeAsync("delete-idempotent", token => client.DeleteSessionAsync(sessionId, token), "operation");
            if (deleteAgain is null)
            {
                steps.Add(new AdapterConformanceStepResult("delete-idempotency", AdapterConformanceOutcome.ContractFailure, "Delete idempotency probe did not complete.", "operation"));
                overall = Worst(overall, AdapterConformanceOutcome.ContractFailure);
            }
        }

        return new AdapterConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
    }

    private static async Task<AdapterConformanceOutcome> CleanupAsync(
        ProtocolLabAdapterClient client,
        string sessionId,
        ICollection<AdapterConformanceStepResult> steps,
        CancellationToken cancellationToken)
    {
        var overall = AdapterConformanceOutcome.Passed;

        try
        {
            var stop = await client.StopAsync(sessionId, cancellationToken);
            steps.Add(new AdapterConformanceStepResult("stop", stop.Category == AdapterOperationResultCategory.Unsupported ? AdapterConformanceOutcome.Unsupported : AdapterConformanceOutcome.Passed, stop.Message ?? "Session stopped.", "operation"));
        }
        catch (ProtocolLabAdapterProblemException ex)
        {
            steps.Add(new AdapterConformanceStepResult("stop", AdapterConformanceOutcome.ContractFailure, ex.Message, "operation", StatusCode: ex.StatusCode, Diagnostics: [ex.Problem.ToString()]));
            overall = Worst(overall, AdapterConformanceOutcome.ContractFailure);
        }
        catch (Exception ex) when (ex is ProtocolLabAdapterProtocolException or HttpRequestException or TaskCanceledException)
        {
            steps.Add(new AdapterConformanceStepResult("stop", Classify(ex), ex.Message, "operation"));
            overall = Worst(overall, Classify(ex));
        }

        try
        {
            var delete = await client.DeleteSessionAsync(sessionId, cancellationToken);
            steps.Add(new AdapterConformanceStepResult("delete", delete.Category == AdapterOperationResultCategory.Unsupported ? AdapterConformanceOutcome.Unsupported : AdapterConformanceOutcome.Passed, delete.Message ?? "Session deleted.", "operation"));
        }
        catch (ProtocolLabAdapterProblemException ex)
        {
            steps.Add(new AdapterConformanceStepResult("delete", AdapterConformanceOutcome.ContractFailure, ex.Message, "operation", StatusCode: ex.StatusCode, Diagnostics: [ex.Problem.ToString()]));
            overall = Worst(overall, AdapterConformanceOutcome.ContractFailure);
        }
        catch (Exception ex) when (ex is ProtocolLabAdapterProtocolException or HttpRequestException or TaskCanceledException)
        {
            var outcome = Classify(ex);
            steps.Add(new AdapterConformanceStepResult("delete", outcome, ex.Message, "operation"));
            overall = Worst(overall, outcome);
        }

        return overall;
    }

    private static AdapterConformanceOutcome Classify(Exception exception)
    {
        return exception switch
        {
            ProtocolLabAdapterProtocolException => AdapterConformanceOutcome.MalformedResponse,
            TaskCanceledException => AdapterConformanceOutcome.Timeout,
            HttpRequestException => AdapterConformanceOutcome.InfrastructureFailure,
            _ => AdapterConformanceOutcome.ContractFailure,
        };
    }

    private static AdapterConformanceOutcome Worst(AdapterConformanceOutcome current, AdapterConformanceOutcome candidate)
    {
        return Severity(candidate) > Severity(current) ? candidate : current;
    }

    private static int Severity(AdapterConformanceOutcome outcome)
    {
        return outcome switch
        {
            AdapterConformanceOutcome.Passed => 0,
            AdapterConformanceOutcome.Unsupported => 1,
            AdapterConformanceOutcome.Timeout => 2,
            AdapterConformanceOutcome.InfrastructureFailure => 3,
            AdapterConformanceOutcome.MalformedResponse => 4,
            AdapterConformanceOutcome.ContractFailure => 5,
            _ => 0,
        };
    }

    private static string BuildSessionId(AdapterConformanceScenario scenario, string? suffix = null)
    {
        var segments = new List<string>
        {
            ArtifactLayout.SanitizeSegment(scenario.RunId),
            ArtifactLayout.SanitizeSegment(scenario.CellId),
            ArtifactLayout.SanitizeSegment(scenario.ScenarioId)
        };

        if (!string.IsNullOrWhiteSpace(suffix))
        {
            segments.Add(ArtifactLayout.SanitizeSegment(suffix));
        }

        return string.Join("-", segments);
    }
}

internal static class AdapterProblemDetailsExtensions
{
    public static bool CategoryIsUnsupported(this AdapterProblemDetails problem)
    {
        return string.Equals(problem.Code, "unsupported", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(problem.UnsupportedReason, "unsupported", StringComparison.OrdinalIgnoreCase);
    }
}
