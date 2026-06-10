// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net.Http;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Adapter.Conformance;

public sealed class TestExecutorConformanceSuite
{
    public async Task<TestExecutorConformanceReport> RunAsync(
        Uri controlPlaneBaseUrl,
        TestExecutorConformanceScenario scenario,
        TestExecutorConformanceOptions options,
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

        var client = new ProtocolLabTestExecutorClient(httpClient);
        var schemaValidator = new TestExecutorSchemaValidator(options.SchemaRootPath);
        var steps = new List<TestExecutorConformanceStepResult>();
        var warnings = new List<string>();
        string? sessionId = null;
        var overall = TestExecutorConformanceOutcome.Passed;

        async Task<T?> InvokeAsync<T>(
            string step,
            Func<CancellationToken, Task<T>> action,
            string schemaName,
            Func<T, TestExecutorConformanceOutcome>? classify = null)
        {
            try
            {
                var value = await action(cancellationToken).ConfigureAwait(false);
                var schemaValid = true;
                if (options.ValidateSchemas)
                {
                    var schemaResult = await schemaValidator.ValidateObjectAsync(schemaName, value, cancellationToken).ConfigureAwait(false);
                    if (!schemaResult.IsValid)
                    {
                        steps.Add(new TestExecutorConformanceStepResult(step, TestExecutorConformanceOutcome.ContractFailure, "Schema validation failed.", schemaName, schemaResult.SchemaPath, Diagnostics: schemaResult.Errors));
                        overall = Worst(overall, TestExecutorConformanceOutcome.ContractFailure);
                        schemaValid = false;
                    }
                }

                var outcome = classify?.Invoke(value) ?? TestExecutorConformanceOutcome.Passed;
                if (outcome == TestExecutorConformanceOutcome.Passed)
                {
                    if (schemaValid)
                    {
                        steps.Add(new TestExecutorConformanceStepResult(step, TestExecutorConformanceOutcome.Passed, "Step passed.", schemaName));
                    }
                }
                else
                {
                    steps.Add(new TestExecutorConformanceStepResult(step, outcome, outcome == TestExecutorConformanceOutcome.Unsupported ? "Test executor reported unsupported." : "Step failed contract validation.", schemaName));
                    overall = Worst(overall, outcome);
                }

                return value;
            }
            catch (ProtocolLabTestExecutorProblemException ex)
            {
                var outcome = ex.Problem.CategoryIsUnsupported() ? TestExecutorConformanceOutcome.Unsupported : TestExecutorConformanceOutcome.ContractFailure;
                steps.Add(new TestExecutorConformanceStepResult(step, outcome, ex.Message, schemaName, StatusCode: ex.StatusCode, Diagnostics: [ex.Problem.ToString()]));
                overall = Worst(overall, outcome);
                return default;
            }
            catch (ProtocolLabTestExecutorProtocolException ex)
            {
                steps.Add(new TestExecutorConformanceStepResult(step, TestExecutorConformanceOutcome.MalformedResponse, ex.Message, schemaName, StatusCode: ex.StatusCode, Diagnostics: [ex.RawContent]));
                overall = Worst(overall, TestExecutorConformanceOutcome.MalformedResponse);
                return default;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                steps.Add(new TestExecutorConformanceStepResult(step, TestExecutorConformanceOutcome.Timeout, ex.Message, schemaName));
                overall = Worst(overall, TestExecutorConformanceOutcome.Timeout);
                return default;
            }
            catch (HttpRequestException ex)
            {
                steps.Add(new TestExecutorConformanceStepResult(step, TestExecutorConformanceOutcome.InfrastructureFailure, ex.Message, schemaName));
                overall = Worst(overall, TestExecutorConformanceOutcome.InfrastructureFailure);
                return default;
            }
        }

        var health = await InvokeAsync("health", token => client.GetHealthAsync(token), "health").ConfigureAwait(false);
        if (health is null)
        {
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, null, warnings);
        }

        if (health.Status == TestExecutorHealthStatus.Unsupported)
        {
            steps.Add(new TestExecutorConformanceStepResult("health-unsupported", TestExecutorConformanceOutcome.Unsupported, health.Message ?? "Test executor health reported unsupported.", "health"));
            overall = Worst(overall, TestExecutorConformanceOutcome.Unsupported);
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, null, warnings);
        }

        if (health.Status is TestExecutorHealthStatus.NotReady or TestExecutorHealthStatus.Unavailable)
        {
            steps.Add(new TestExecutorConformanceStepResult("health-not-ready", TestExecutorConformanceOutcome.Timeout, health.Message ?? "Test executor is not ready.", "health"));
            overall = Worst(overall, TestExecutorConformanceOutcome.Timeout);
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, null, warnings);
        }

        var manifest = await InvokeAsync("manifest", token => client.GetManifestAsync(token), "manifest").ConfigureAwait(false);
        if (manifest is null)
        {
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, null, warnings);
        }

        if (!string.Equals(manifest.VersionCompatibility.ContractVersion, options.SupportedContractVersion, StringComparison.OrdinalIgnoreCase) &&
            !manifest.VersionCompatibility.CompatibleContractVersions.Contains(options.SupportedContractVersion, StringComparer.OrdinalIgnoreCase))
        {
            steps.Add(new TestExecutorConformanceStepResult(
                "manifest-version",
                TestExecutorConformanceOutcome.Unsupported,
                $"Test executor contract version '{manifest.VersionCompatibility.ContractVersion}' is not compatible with supported version '{options.SupportedContractVersion}'.",
                "manifest"));
            overall = Worst(overall, TestExecutorConformanceOutcome.Unsupported);
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, null, warnings);
        }

        var sessionCreate = await InvokeAsync("session-create", token => client.CreateSessionAsync(new TestExecutorSessionCreateRequest
        {
            RequestedSessionId = BuildSessionId(scenario),
            RunId = scenario.RunId,
            CellId = scenario.CellId,
            SessionLabel = scenario.SessionLabel
        }, token), "session-resource").ConfigureAwait(false);

        if (sessionCreate is null)
        {
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        sessionId = sessionCreate.Session.SessionId;

        var sessionRead = await InvokeAsync("session-get", token => client.GetSessionAsync(sessionId, token), "session-resource").ConfigureAwait(false);
        if (sessionRead is null)
        {
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken).ConfigureAwait(false));
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        var prepare = await InvokeAsync("prepare", token => client.PrepareAsync(sessionId, new TestExecutorPrepareRequest
        {
            TestId = scenario.TestId,
            ScenarioId = scenario.ScenarioId,
            ScenarioVersion = scenario.ScenarioVersion,
            Protocol = scenario.Protocol,
            TestDocument = scenario.TestDocument,
            ScenarioDocument = scenario.ScenarioDocument,
            TargetEndpoints = scenario.TargetEndpoints,
            RunId = scenario.RunId,
            CellId = scenario.CellId,
            ArtifactOutputExpectations = scenario.ArtifactOutputExpectations,
            Extensions = scenario.Extensions
        }, token), "operation", ClassifyOperation).ConfigureAwait(false);

        if (prepare is null)
        {
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken).ConfigureAwait(false));
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        if (prepare.Category == TestExecutorOperationResultCategory.Unsupported)
        {
            steps.Add(new TestExecutorConformanceStepResult("prepare-unsupported", TestExecutorConformanceOutcome.Unsupported, prepare.Message ?? "Test executor reported unsupported test or scenario.", "operation"));
            overall = Worst(overall, TestExecutorConformanceOutcome.Unsupported);
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken).ConfigureAwait(false));
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        if (prepare.Category is TestExecutorOperationResultCategory.Failed or TestExecutorOperationResultCategory.Rejected)
        {
            steps.Add(new TestExecutorConformanceStepResult("prepare-failed", TestExecutorConformanceOutcome.ContractFailure, prepare.Message ?? "Test executor prepare failed.", "operation"));
            overall = Worst(overall, TestExecutorConformanceOutcome.ContractFailure);
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken).ConfigureAwait(false));
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        var start = await InvokeAsync("start", token => client.StartAsync(sessionId, token), "operation", ClassifyOperation).ConfigureAwait(false);
        if (start is null)
        {
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken).ConfigureAwait(false));
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        if (start.Category == TestExecutorOperationResultCategory.Unsupported)
        {
            steps.Add(new TestExecutorConformanceStepResult("start-unsupported", TestExecutorConformanceOutcome.Unsupported, start.Message ?? "Test executor reported unsupported start.", "operation"));
            overall = Worst(overall, TestExecutorConformanceOutcome.Unsupported);
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken).ConfigureAwait(false));
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        if (start.Category is TestExecutorOperationResultCategory.Failed or TestExecutorOperationResultCategory.Rejected)
        {
            steps.Add(new TestExecutorConformanceStepResult("start-failed", TestExecutorConformanceOutcome.ContractFailure, start.Message ?? "Test executor start failed.", "operation"));
            overall = Worst(overall, TestExecutorConformanceOutcome.ContractFailure);
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken).ConfigureAwait(false));
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        var status = await InvokeAsync("status", token => client.GetStatusAsync(sessionId, token), "status", ClassifyStatus).ConfigureAwait(false);
        if (status is null)
        {
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken).ConfigureAwait(false));
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        if (status.Session.State == TestExecutorSessionState.Unsupported)
        {
            steps.Add(new TestExecutorConformanceStepResult("status-unsupported", TestExecutorConformanceOutcome.Unsupported, status.Operation?.Message ?? "Test executor session reported unsupported.", "status"));
            overall = Worst(overall, TestExecutorConformanceOutcome.Unsupported);
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken).ConfigureAwait(false));
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        if (status.Session.State == TestExecutorSessionState.Failed)
        {
            steps.Add(new TestExecutorConformanceStepResult("status-failed", TestExecutorConformanceOutcome.ContractFailure, status.Operation?.Message ?? "Test executor session failed.", "status"));
            overall = Worst(overall, TestExecutorConformanceOutcome.ContractFailure);
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken).ConfigureAwait(false));
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        var metrics = await InvokeAsync("metrics", token => client.GetMetricsAsync(sessionId, token), "metrics", value => ClassifyResourceAvailability(value.Availability, options.RequireAvailableMetrics)).ConfigureAwait(false);
        if (metrics is null)
        {
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken).ConfigureAwait(false));
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        if (options.RequireAvailableMetrics && metrics.Availability != TestExecutorResourceAvailability.Available)
        {
            steps.Add(new TestExecutorConformanceStepResult("metrics-unavailable", TestExecutorConformanceOutcome.ContractFailure, "Metrics are required for this conformance run but were not available.", "metrics"));
            overall = Worst(overall, TestExecutorConformanceOutcome.ContractFailure);
        }

        var artifacts = await InvokeAsync("artifacts", token => client.GetArtifactsAsync(sessionId, token), "artifacts", value => ClassifyResourceAvailability(value.Availability, options.RequireAvailableArtifacts)).ConfigureAwait(false);
        if (artifacts is null)
        {
            overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken).ConfigureAwait(false));
            return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
        }

        if (options.RequireAvailableArtifacts && artifacts.Availability != TestExecutorResourceAvailability.Available)
        {
            steps.Add(new TestExecutorConformanceStepResult("artifacts-unavailable", TestExecutorConformanceOutcome.ContractFailure, "Artifacts are required for this conformance run but were not available.", "artifacts"));
            overall = Worst(overall, TestExecutorConformanceOutcome.ContractFailure);
        }

        overall = Worst(overall, await CleanupAsync(client, sessionId, steps, cancellationToken).ConfigureAwait(false));

        if (options.ValidateDeleteIdempotency)
        {
            var deleteAgain = await InvokeAsync("delete-idempotent", token => client.DeleteSessionAsync(sessionId, token), "operation").ConfigureAwait(false);
            if (deleteAgain is null)
            {
                steps.Add(new TestExecutorConformanceStepResult("delete-idempotency", TestExecutorConformanceOutcome.ContractFailure, "Delete idempotency probe did not complete.", "operation"));
                overall = Worst(overall, TestExecutorConformanceOutcome.ContractFailure);
            }
        }

        return new TestExecutorConformanceReport(controlPlaneBaseUrl.ToString(), overall, steps, sessionId, warnings);
    }

    private static async Task<TestExecutorConformanceOutcome> CleanupAsync(
        ProtocolLabTestExecutorClient client,
        string sessionId,
        ICollection<TestExecutorConformanceStepResult> steps,
        CancellationToken cancellationToken)
    {
        var overall = TestExecutorConformanceOutcome.Passed;

        try
        {
            var stop = await client.StopAsync(sessionId, cancellationToken).ConfigureAwait(false);
            var stopOutcome = stop.Category == TestExecutorOperationResultCategory.Unsupported ? TestExecutorConformanceOutcome.Unsupported : TestExecutorConformanceOutcome.Passed;
            steps.Add(new TestExecutorConformanceStepResult("stop", stopOutcome, stop.Message ?? "Session stopped.", "operation"));
            overall = Worst(overall, stopOutcome);
        }
        catch (ProtocolLabTestExecutorProblemException ex)
        {
            steps.Add(new TestExecutorConformanceStepResult("stop", TestExecutorConformanceOutcome.ContractFailure, ex.Message, "operation", StatusCode: ex.StatusCode, Diagnostics: [ex.Problem.ToString()]));
            overall = Worst(overall, TestExecutorConformanceOutcome.ContractFailure);
        }
        catch (Exception ex) when (ex is ProtocolLabTestExecutorProtocolException or HttpRequestException or TaskCanceledException)
        {
            var outcome = Classify(ex);
            steps.Add(new TestExecutorConformanceStepResult("stop", outcome, ex.Message, "operation"));
            overall = Worst(overall, outcome);
        }

        try
        {
            var delete = await client.DeleteSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
            var deleteOutcome = delete.Category == TestExecutorOperationResultCategory.Unsupported ? TestExecutorConformanceOutcome.Unsupported : TestExecutorConformanceOutcome.Passed;
            steps.Add(new TestExecutorConformanceStepResult("delete", deleteOutcome, delete.Message ?? "Session deleted.", "operation"));
            overall = Worst(overall, deleteOutcome);
        }
        catch (ProtocolLabTestExecutorProblemException ex)
        {
            steps.Add(new TestExecutorConformanceStepResult("delete", TestExecutorConformanceOutcome.ContractFailure, ex.Message, "operation", StatusCode: ex.StatusCode, Diagnostics: [ex.Problem.ToString()]));
            overall = Worst(overall, TestExecutorConformanceOutcome.ContractFailure);
        }
        catch (Exception ex) when (ex is ProtocolLabTestExecutorProtocolException or HttpRequestException or TaskCanceledException)
        {
            var outcome = Classify(ex);
            steps.Add(new TestExecutorConformanceStepResult("delete", outcome, ex.Message, "operation"));
            overall = Worst(overall, outcome);
        }

        return overall;
    }

    private static TestExecutorConformanceOutcome ClassifyOperation(TestExecutorOperationResult operation)
    {
        return operation.Category switch
        {
            TestExecutorOperationResultCategory.Unsupported => TestExecutorConformanceOutcome.Unsupported,
            TestExecutorOperationResultCategory.Rejected or TestExecutorOperationResultCategory.Failed => TestExecutorConformanceOutcome.ContractFailure,
            _ => TestExecutorConformanceOutcome.Passed,
        };
    }

    private static TestExecutorConformanceOutcome ClassifyStatus(TestExecutorStatusResponse status)
    {
        if (status.Session.State == TestExecutorSessionState.Unsupported ||
            status.Operation?.Category == TestExecutorOperationResultCategory.Unsupported)
        {
            return TestExecutorConformanceOutcome.Unsupported;
        }

        if (status.Session.State == TestExecutorSessionState.Failed ||
            status.Operation?.Category is TestExecutorOperationResultCategory.Rejected or TestExecutorOperationResultCategory.Failed)
        {
            return TestExecutorConformanceOutcome.ContractFailure;
        }

        return TestExecutorConformanceOutcome.Passed;
    }

    private static TestExecutorConformanceOutcome ClassifyResourceAvailability(TestExecutorResourceAvailability availability, bool required)
    {
        if (availability == TestExecutorResourceAvailability.Unsupported)
        {
            return required ? TestExecutorConformanceOutcome.ContractFailure : TestExecutorConformanceOutcome.Unsupported;
        }

        if (required && availability != TestExecutorResourceAvailability.Available)
        {
            return TestExecutorConformanceOutcome.ContractFailure;
        }

        return TestExecutorConformanceOutcome.Passed;
    }

    private static TestExecutorConformanceOutcome Classify(Exception exception)
    {
        return exception switch
        {
            ProtocolLabTestExecutorProtocolException => TestExecutorConformanceOutcome.MalformedResponse,
            TaskCanceledException => TestExecutorConformanceOutcome.Timeout,
            HttpRequestException => TestExecutorConformanceOutcome.InfrastructureFailure,
            _ => TestExecutorConformanceOutcome.ContractFailure,
        };
    }

    private static TestExecutorConformanceOutcome Worst(TestExecutorConformanceOutcome current, TestExecutorConformanceOutcome candidate)
    {
        return Severity(candidate) > Severity(current) ? candidate : current;
    }

    private static int Severity(TestExecutorConformanceOutcome outcome)
    {
        return outcome switch
        {
            TestExecutorConformanceOutcome.Passed => 0,
            TestExecutorConformanceOutcome.Unsupported => 1,
            TestExecutorConformanceOutcome.Timeout => 2,
            TestExecutorConformanceOutcome.InfrastructureFailure => 3,
            TestExecutorConformanceOutcome.MalformedResponse => 4,
            TestExecutorConformanceOutcome.ContractFailure => 5,
            _ => 0,
        };
    }

    private static string BuildSessionId(TestExecutorConformanceScenario scenario)
    {
        var segments = new List<string>
        {
            ArtifactLayout.SanitizeSegment(scenario.RunId),
            ArtifactLayout.SanitizeSegment(scenario.CellId),
            ArtifactLayout.SanitizeSegment(scenario.ScenarioId),
            ArtifactLayout.SanitizeSegment(scenario.TestId)
        };

        return string.Join("-", segments);
    }
}

internal static class TestExecutorProblemDetailsExtensions
{
    public static bool CategoryIsUnsupported(this TestExecutorProblemDetails problem)
    {
        return problem.ExecutorStatus == TestExecutorHealthStatus.Unsupported ||
            string.Equals(problem.Code, "unsupported", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(problem.UnsupportedReason, "unsupported", StringComparison.OrdinalIgnoreCase);
    }
}
