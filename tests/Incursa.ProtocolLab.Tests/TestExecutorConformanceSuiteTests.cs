// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Conformance;
using Incursa.ProtocolLab.Adapter.Contracts;

namespace Incursa.ProtocolLab.Tests;

public sealed class TestExecutorConformanceSuiteTests
{
    private static string SchemaRoot => Path.Combine(TestPaths.RepoRoot, "schemas", "test-executor", "v1");

    [Fact]
    public async Task Fake_test_executor_passes_the_full_conformance_suite()
    {
        var report = await RunSuiteAsync(new FakeTestExecutorHandler(), scenarioId: "success");

        Assert.Equal(TestExecutorConformanceOutcome.Passed, report.Outcome);
        Assert.Contains(report.Steps, step => step.Step == "health" && step.Outcome == TestExecutorConformanceOutcome.Passed);
        Assert.Contains(report.Steps, step => step.Step == "manifest" && step.Outcome == TestExecutorConformanceOutcome.Passed);
        Assert.Contains(report.Steps, step => step.Step == "session-create" && step.Outcome == TestExecutorConformanceOutcome.Passed);
        Assert.Contains(report.Steps, step => step.Step == "session-get" && step.Outcome == TestExecutorConformanceOutcome.Passed);
        Assert.Contains(report.Steps, step => step.Step == "prepare" && step.Outcome == TestExecutorConformanceOutcome.Passed);
        Assert.Contains(report.Steps, step => step.Step == "start" && step.Outcome == TestExecutorConformanceOutcome.Passed);
        Assert.Contains(report.Steps, step => step.Step == "status" && step.Outcome == TestExecutorConformanceOutcome.Passed);
        Assert.Contains(report.Steps, step => step.Step == "metrics" && step.Outcome == TestExecutorConformanceOutcome.Passed);
        Assert.Contains(report.Steps, step => step.Step == "artifacts" && step.Outcome == TestExecutorConformanceOutcome.Passed);
        Assert.Contains(report.Steps, step => step.Step == "delete-idempotent" && step.Outcome == TestExecutorConformanceOutcome.Passed);
    }

    [Fact]
    public async Task Unsupported_tests_remain_structured_results_not_harness_failures()
    {
        var report = await RunSuiteAsync(new FakeTestExecutorHandler(), scenarioId: "unsupported");

        Assert.Equal(TestExecutorConformanceOutcome.Unsupported, report.Outcome);
        Assert.Contains(report.Steps, step => step.Step == "prepare-unsupported" && step.Outcome == TestExecutorConformanceOutcome.Unsupported);
    }

    [Theory]
    [InlineData("prepare-failure")]
    [InlineData("start-failure")]
    public async Task Executor_contract_failures_are_reported_as_contract_failures(string scenarioId)
    {
        var report = await RunSuiteAsync(new FakeTestExecutorHandler(), scenarioId);

        Assert.Equal(TestExecutorConformanceOutcome.ContractFailure, report.Outcome);
        Assert.Contains(report.Steps, step => step.Step is "prepare-failed" or "start-failed");
    }

    [Fact]
    public async Task Timeout_probes_are_reported_as_timeouts_not_contract_failures()
    {
        var report = await RunSuiteAsync(
            new FakeTestExecutorHandler(responseDelay: TimeSpan.FromMilliseconds(250)),
            scenarioId: "success",
            timeout: TimeSpan.FromMilliseconds(50));

        Assert.Equal(TestExecutorConformanceOutcome.Timeout, report.Outcome);
        Assert.Contains(report.Steps, step => step.Outcome == TestExecutorConformanceOutcome.Timeout);
    }

    [Fact]
    public async Task Problem_and_malformed_control_plane_responses_are_reported_explicitly()
    {
        var problemReport = await RunSuiteAsync(new FakeTestExecutorHandler(manifestBehavior: FakeManifestBehavior.Problem), scenarioId: "success");

        Assert.Equal(TestExecutorConformanceOutcome.ContractFailure, problemReport.Outcome);
        Assert.Contains(problemReport.Steps, step => step.Step == "manifest" && step.Outcome == TestExecutorConformanceOutcome.ContractFailure);
        Assert.Contains(problemReport.Steps, step => step.StatusCode == HttpStatusCode.ServiceUnavailable);

        var malformedReport = await RunSuiteAsync(new FakeTestExecutorHandler(manifestBehavior: FakeManifestBehavior.Malformed), scenarioId: "success");

        Assert.Equal(TestExecutorConformanceOutcome.MalformedResponse, malformedReport.Outcome);
        Assert.Contains(malformedReport.Steps, step => step.Step == "manifest" && step.Outcome == TestExecutorConformanceOutcome.MalformedResponse);
    }

    [Fact]
    public async Task Unsupported_contract_versions_short_circuit_as_structured_unsupported_results()
    {
        var report = await RunSuiteAsync(new FakeTestExecutorHandler(contractVersion: "test-executor-v2"), scenarioId: "success");

        Assert.Equal(TestExecutorConformanceOutcome.Unsupported, report.Outcome);
        Assert.Contains(report.Steps, step => step.Step == "manifest-version" && step.Outcome == TestExecutorConformanceOutcome.Unsupported);
    }

    [Fact]
    public async Task Unreachable_control_planes_are_distinguished_as_infrastructure_failures()
    {
        var report = await RunSuiteAsync(new ThrowingHandler(), scenarioId: "success");

        Assert.Equal(TestExecutorConformanceOutcome.InfrastructureFailure, report.Outcome);
        Assert.Contains(report.Steps, step => step.Outcome == TestExecutorConformanceOutcome.InfrastructureFailure);
    }

    private static Task<TestExecutorConformanceReport> RunSuiteAsync(
        HttpMessageHandler handler,
        string scenarioId,
        TimeSpan? timeout = null)
    {
        var suite = new TestExecutorConformanceSuite();
        var options = new TestExecutorConformanceOptions
        {
            SchemaRootPath = SchemaRoot,
            SupportedContractVersion = "test-executor-v1",
            Timeout = timeout ?? TimeSpan.FromSeconds(2),
            HttpMessageHandlerFactory = () => handler
        };

        return suite.RunAsync(
            new Uri("http://fixture.local"),
            new TestExecutorConformanceScenario
            {
                TestId = scenarioId == "unsupported" ? "fixture.unsupported" : "fixture.success",
                ScenarioId = scenarioId,
                ScenarioVersion = "1.0",
                Protocol = "h3",
                RunId = "test-executor-conformance-test",
                CellId = "test-executor-conformance-test",
                SessionLabel = "test-executor-conformance-test"
            },
            options);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("fixture infrastructure failure");
        }
    }

    private enum FakeManifestBehavior
    {
        Success,
        Problem,
        Malformed,
    }

    private sealed class FakeTestExecutorHandler(
        string contractVersion = "test-executor-v1",
        FakeManifestBehavior manifestBehavior = FakeManifestBehavior.Success,
        TimeSpan? responseDelay = null) : HttpMessageHandler
    {
        private readonly Dictionary<string, FakeSession> sessions = new(StringComparer.OrdinalIgnoreCase);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (responseDelay is { } delay)
            {
                await Task.Delay(delay, cancellationToken);
            }

            if (request.RequestUri is null)
            {
                return CreateProblemResponse(HttpStatusCode.BadRequest, "missing-uri", "The request URI is required.");
            }

            var path = request.RequestUri.AbsolutePath;

            if (request.Method == HttpMethod.Get && PathEquals(path, TestExecutorRoutes.Health))
            {
                return CreateJsonResponse(HttpStatusCode.OK, CreateHealthResponse());
            }

            if (request.Method == HttpMethod.Get && PathEquals(path, TestExecutorRoutes.Manifest))
            {
                return manifestBehavior switch
                {
                    FakeManifestBehavior.Problem => CreateProblemResponse(HttpStatusCode.ServiceUnavailable, "manifest-unavailable", "Fixture manifest unavailable."),
                    FakeManifestBehavior.Malformed => CreateMalformedResponse(),
                    _ => CreateJsonResponse(HttpStatusCode.OK, CreateManifestResponse())
                };
            }

            if (request.Method == HttpMethod.Post && PathEquals(path, TestExecutorRoutes.Sessions))
            {
                var createRequest = await ReadJsonAsync<TestExecutorSessionCreateRequest>(request, cancellationToken);
                var sessionId = string.IsNullOrWhiteSpace(createRequest?.RequestedSessionId)
                    ? $"session-{Guid.NewGuid():N}"
                    : createRequest.RequestedSessionId;

                var session = new FakeSession
                {
                    SessionId = sessionId,
                    State = TestExecutorSessionState.Created,
                    RunId = createRequest?.RunId,
                    CellId = createRequest?.CellId
                };

                sessions[sessionId] = session;
                return CreateJsonResponse(HttpStatusCode.Created, CreateSessionResource(session));
            }

            if (request.Method == HttpMethod.Get && TryGetSessionId(path, out var getSessionId))
            {
                return sessions.TryGetValue(getSessionId, out var session)
                    ? CreateJsonResponse(HttpStatusCode.OK, CreateSessionResource(session))
                    : CreateProblemResponse(HttpStatusCode.NotFound, "session-not-found", "Session not found.", getSessionId);
            }

            if (request.Method == HttpMethod.Post && TryGetSessionId(path, "/prepare", out var prepareSessionId))
            {
                return await HandlePrepareAsync(request, prepareSessionId, cancellationToken);
            }

            if (request.Method == HttpMethod.Post && TryGetSessionId(path, "/start", out var startSessionId))
            {
                return HandleStart(startSessionId);
            }

            if (request.Method == HttpMethod.Get && TryGetSessionId(path, "/status", out var statusSessionId))
            {
                return sessions.TryGetValue(statusSessionId, out var session)
                    ? CreateJsonResponse(HttpStatusCode.OK, new TestExecutorStatusResponse
                    {
                        Session = CreateSummary(session),
                        Operation = new TestExecutorOperationResult { Category = TestExecutorOperationResultCategory.Succeeded, Message = "Session status." }
                    })
                    : CreateProblemResponse(HttpStatusCode.NotFound, "session-not-found", "Session not found.", statusSessionId);
            }

            if (request.Method == HttpMethod.Get && TryGetSessionId(path, "/metrics", out var metricsSessionId))
            {
                return sessions.TryGetValue(metricsSessionId, out var session)
                    ? CreateJsonResponse(HttpStatusCode.OK, CreateMetricsResponse(session))
                    : CreateProblemResponse(HttpStatusCode.NotFound, "session-not-found", "Session not found.", metricsSessionId);
            }

            if (request.Method == HttpMethod.Get && TryGetSessionId(path, "/artifacts", out var artifactsSessionId))
            {
                return sessions.TryGetValue(artifactsSessionId, out var session)
                    ? CreateJsonResponse(HttpStatusCode.OK, CreateArtifactsResponse(session))
                    : CreateProblemResponse(HttpStatusCode.NotFound, "session-not-found", "Session not found.", artifactsSessionId);
            }

            if (request.Method == HttpMethod.Post && TryGetSessionId(path, "/stop", out var stopSessionId))
            {
                return sessions.TryGetValue(stopSessionId, out var session)
                    ? HandleStop(session)
                    : CreateProblemResponse(HttpStatusCode.NotFound, "session-not-found", "Session not found.", stopSessionId);
            }

            if (request.Method == HttpMethod.Delete && TryGetSessionId(path, out var deleteSessionId))
            {
                sessions.Remove(deleteSessionId);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return CreateProblemResponse(HttpStatusCode.NotFound, "unknown-route", "Unknown test executor route.");
        }

        private async Task<HttpResponseMessage> HandlePrepareAsync(HttpRequestMessage request, string sessionId, CancellationToken cancellationToken)
        {
            if (!sessions.TryGetValue(sessionId, out var session))
            {
                return CreateProblemResponse(HttpStatusCode.NotFound, "session-not-found", "Session not found.", sessionId);
            }

            var prepareRequest = await ReadJsonAsync<TestExecutorPrepareRequest>(request, cancellationToken);
            session.TestId = prepareRequest?.TestId;
            session.ScenarioId = prepareRequest?.ScenarioId;
            session.Protocol = prepareRequest?.Protocol;

            if (string.Equals(session.ScenarioId, "unsupported", StringComparison.OrdinalIgnoreCase))
            {
                session.State = TestExecutorSessionState.Unsupported;
                return CreateJsonResponse(HttpStatusCode.OK, new TestExecutorOperationResult
                {
                    Category = TestExecutorOperationResultCategory.Unsupported,
                    Message = "Fixture scenario is unsupported.",
                    Code = "unsupported"
                });
            }

            if (string.Equals(session.ScenarioId, "prepare-failure", StringComparison.OrdinalIgnoreCase))
            {
                session.State = TestExecutorSessionState.Failed;
                return CreateJsonResponse(HttpStatusCode.OK, new TestExecutorOperationResult
                {
                    Category = TestExecutorOperationResultCategory.Failed,
                    Message = "Fixture prepare failure.",
                    Code = "fixture-prepare-failure"
                });
            }

            session.State = TestExecutorSessionState.Prepared;
            return CreateJsonResponse(HttpStatusCode.OK, new TestExecutorOperationResult
            {
                Category = TestExecutorOperationResultCategory.Succeeded,
                Message = "Prepared."
            });
        }

        private HttpResponseMessage HandleStart(string sessionId)
        {
            if (!sessions.TryGetValue(sessionId, out var session))
            {
                return CreateProblemResponse(HttpStatusCode.NotFound, "session-not-found", "Session not found.", sessionId);
            }

            if (session.State != TestExecutorSessionState.Prepared)
            {
                return CreateJsonResponse(HttpStatusCode.OK, new TestExecutorOperationResult
                {
                    Category = TestExecutorOperationResultCategory.Rejected,
                    Message = "Session must be prepared before start.",
                    Code = "invalid-transition"
                });
            }

            if (string.Equals(session.ScenarioId, "start-failure", StringComparison.OrdinalIgnoreCase))
            {
                session.State = TestExecutorSessionState.Failed;
                return CreateJsonResponse(HttpStatusCode.OK, new TestExecutorOperationResult
                {
                    Category = TestExecutorOperationResultCategory.Failed,
                    Message = "Fixture start failure.",
                    Code = "fixture-start-failure"
                });
            }

            session.State = TestExecutorSessionState.Running;
            return CreateJsonResponse(HttpStatusCode.OK, new TestExecutorOperationResult
            {
                Category = TestExecutorOperationResultCategory.Succeeded,
                Message = "Started."
            });
        }

        private HttpResponseMessage HandleStop(FakeSession session)
        {
            session.State = TestExecutorSessionState.Stopped;
            return CreateJsonResponse(HttpStatusCode.OK, new TestExecutorOperationResult
            {
                Category = TestExecutorOperationResultCategory.Succeeded,
                Message = "Stopped."
            });
        }

        private TestExecutorHealthResponse CreateHealthResponse()
        {
            return new TestExecutorHealthResponse
            {
                ExecutorIdentity = CreateIdentity(),
                Status = TestExecutorHealthStatus.Ready,
                VersionCompatibility = CreateVersionCompatibility(),
                Capabilities =
                [
                    new TestExecutorCapability
                    {
                        Id = "executor.lifecycle",
                        Status = TestExecutorCapabilityStatus.Supported
                    }
                ]
            };
        }

        private TestExecutorManifestResponse CreateManifestResponse()
        {
            return new TestExecutorManifestResponse
            {
                ExecutorIdentity = CreateIdentity(),
                VersionCompatibility = CreateVersionCompatibility(),
                ClaimedCapabilities =
                [
                    new TestExecutorCapability
                    {
                        Id = "traffic.h3.basic",
                        Status = TestExecutorCapabilityStatus.Supported
                    }
                ],
                SupportedTestSelectors =
                [
                    new TestExecutorSelector
                    {
                        SelectorType = "test-id",
                        Expression = "fixture.*"
                    }
                ],
                SupportedScenarioSelectors =
                [
                    new TestExecutorSelector
                    {
                        SelectorType = "scenario-id",
                        Expression = "*"
                    }
                ],
                SupportedProtocolFamilies = ["h3", "quic"],
                SupportedExecutionModes = ["single-cell"],
                RequiredTargetEndpointBindings =
                [
                    new TestExecutorEndpointBinding
                    {
                        BindingId = "primary",
                        Purpose = "test-endpoint",
                        EndpointType = "h3",
                        Protocols = ["h3", "quic"],
                        Required = true
                    }
                ],
                SupportedArtifactTypes =
                [
                    new TestExecutorArtifactType
                    {
                        Type = "log",
                        ProducedByStates = ["stopped"]
                    }
                ],
                MetricsAvailability = new TestExecutorMetricsAvailability
                {
                    Available = true,
                    AvailableKinds = ["session"]
                }
            };
        }

        private TestExecutorIdentity CreateIdentity()
        {
            return new TestExecutorIdentity
            {
                Id = "fixture-test-executor",
                Name = "Fixture Test Executor",
                Version = "1.0.0"
            };
        }

        private TestExecutorVersionCompatibility CreateVersionCompatibility()
        {
            return new TestExecutorVersionCompatibility
            {
                ContractVersion = contractVersion,
                CompatibleContractVersions = string.Equals(contractVersion, "test-executor-v1", StringComparison.OrdinalIgnoreCase) ? ["test-executor-v1"] : []
            };
        }

        private static TestExecutorSessionResource CreateSessionResource(FakeSession session)
        {
            return new TestExecutorSessionResource
            {
                Session = CreateSummary(session),
                Operation = new TestExecutorOperationResult
                {
                    Category = TestExecutorOperationResultCategory.Succeeded,
                    Message = "Session resource."
                }
            };
        }

        private static TestExecutorSessionSummary CreateSummary(FakeSession session)
        {
            return new TestExecutorSessionSummary
            {
                SessionId = session.SessionId,
                State = session.State,
                TestId = session.TestId,
                ScenarioId = session.ScenarioId,
                Protocol = session.Protocol,
                RunId = session.RunId,
                CellId = session.CellId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        private static TestExecutorMetricsResponse CreateMetricsResponse(FakeSession session)
        {
            return new TestExecutorMetricsResponse
            {
                Session = CreateSummary(session),
                Availability = TestExecutorResourceAvailability.Available,
                CapturedAt = DateTimeOffset.UtcNow,
                Metrics =
                [
                    new TestExecutorMetric
                    {
                        MetricId = "requests.total",
                        Scope = "session",
                        Unit = "count",
                        Value = ProtocolLabAdapterJson.SerializeValue(1)
                    }
                ]
            };
        }

        private static TestExecutorArtifactsResponse CreateArtifactsResponse(FakeSession session)
        {
            return new TestExecutorArtifactsResponse
            {
                Session = CreateSummary(session),
                Availability = TestExecutorResourceAvailability.Available,
                Artifacts =
                [
                    new TestExecutorArtifact
                    {
                        ArtifactId = "executor-log",
                        ArtifactType = "log",
                        Status = TestExecutorResourceAvailability.Available,
                        Path = "artifacts/executor.log",
                        ContentType = "text/plain",
                        Final = true
                    }
                ]
            };
        }

        private static bool PathEquals(string actual, string expected)
        {
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetSessionId(string path, out string sessionId)
        {
            const string prefix = TestExecutorRoutes.Sessions + "/";
            sessionId = "";

            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var remainder = path[prefix.Length..];
            if (remainder.Length == 0 || remainder.Contains('/', StringComparison.Ordinal))
            {
                return false;
            }

            sessionId = Uri.UnescapeDataString(remainder);
            return true;
        }

        private static bool TryGetSessionId(string path, string suffix, out string sessionId)
        {
            const string prefix = TestExecutorRoutes.Sessions + "/";
            sessionId = "";

            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var idSegment = path[prefix.Length..^suffix.Length];
            if (idSegment.EndsWith("/", StringComparison.Ordinal))
            {
                idSegment = idSegment[..^1];
            }

            if (idSegment.Length == 0 || idSegment.Contains('/', StringComparison.Ordinal))
            {
                return false;
            }

            sessionId = Uri.UnescapeDataString(idSegment);
            return true;
        }

        private static async Task<T?> ReadJsonAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is null)
            {
                return default;
            }

            var payload = await request.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<T>(payload, ProtocolLabAdapterJson.Options);
        }

        private static HttpResponseMessage CreateJsonResponse<T>(HttpStatusCode statusCode, T payload)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, ProtocolLabAdapterJson.Options), System.Text.Encoding.UTF8)
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("application/json")
                    }
                }
            };
        }

        private static HttpResponseMessage CreateMalformedResponse()
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"manifest\":", System.Text.Encoding.UTF8, "application/json")
            };
        }

        private static HttpResponseMessage CreateProblemResponse(HttpStatusCode statusCode, string code, string title, string? sessionId = null)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(JsonSerializer.Serialize(new TestExecutorProblemDetails
                {
                    Type = "https://incursa.example/problems/fixture-test-executor",
                    Title = title,
                    Status = (int)statusCode,
                    Code = code,
                    Operation = "fixture",
                    SessionId = sessionId,
                    Retryable = false
                }, ProtocolLabAdapterJson.Options), System.Text.Encoding.UTF8)
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("application/problem+json")
                    }
                }
            };
        }
    }

    private sealed class FakeSession
    {
        public string SessionId { get; init; } = "";

        public TestExecutorSessionState State { get; set; }

        public string? TestId { get; set; }

        public string? ScenarioId { get; set; }

        public string? Protocol { get; set; }

        public string? RunId { get; init; }

        public string? CellId { get; init; }
    }
}
