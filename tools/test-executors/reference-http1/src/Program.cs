// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ReferenceHttp1Executor>();
var app = builder.Build();

app.MapGet(TestExecutorRoutes.Health, (ReferenceHttp1Executor executor) => Json(executor.Health()));
app.MapGet(TestExecutorRoutes.Manifest, (ReferenceHttp1Executor executor) => Json(executor.Manifest()));

app.MapPost(TestExecutorRoutes.Sessions, async (HttpContext context, ReferenceHttp1Executor executor) =>
{
    var request = await ReadJsonAsync<TestExecutorSessionCreateRequest>(context);
    var resource = executor.CreateSession(request ?? new TestExecutorSessionCreateRequest());
    return Results.Json(resource, ProtocolLabAdapterJson.Options, statusCode: StatusCodes.Status201Created);
});

app.MapGet(TestExecutorRoutes.Sessions + "/{sessionId}", (string sessionId, ReferenceHttp1Executor executor) =>
    executor.TryGetSession(sessionId, out var resource) ? Json(resource) : Problem(StatusCodes.Status404NotFound, "session-not-found", "Session not found.", sessionId));

app.MapPost(TestExecutorRoutes.Sessions + "/{sessionId}/prepare", async (string sessionId, HttpContext context, ReferenceHttp1Executor executor) =>
{
    var request = await ReadJsonAsync<TestExecutorPrepareRequest>(context);
    return request is null
        ? Problem(StatusCodes.Status400BadRequest, "invalid-prepare-request", "Prepare request body is required.", sessionId)
        : Json(executor.Prepare(sessionId, request));
});

app.MapPost(TestExecutorRoutes.Sessions + "/{sessionId}/start", async (string sessionId, ReferenceHttp1Executor executor, CancellationToken cancellationToken) =>
    Json(await executor.StartAsync(sessionId, cancellationToken)));

app.MapGet(TestExecutorRoutes.Sessions + "/{sessionId}/status", (string sessionId, ReferenceHttp1Executor executor) =>
    executor.TryGetStatus(sessionId, out var status) ? Json(status) : Problem(StatusCodes.Status404NotFound, "session-not-found", "Session not found.", sessionId));

app.MapGet(TestExecutorRoutes.Sessions + "/{sessionId}/metrics", (string sessionId, ReferenceHttp1Executor executor) =>
    executor.TryGetMetrics(sessionId, out var metrics) ? Json(metrics) : Problem(StatusCodes.Status404NotFound, "session-not-found", "Session not found.", sessionId));

app.MapGet(TestExecutorRoutes.Sessions + "/{sessionId}/artifacts", (string sessionId, ReferenceHttp1Executor executor) =>
    executor.TryGetArtifacts(sessionId, out var artifacts) ? Json(artifacts) : Problem(StatusCodes.Status404NotFound, "session-not-found", "Session not found.", sessionId));

app.MapPost(TestExecutorRoutes.Sessions + "/{sessionId}/stop", (string sessionId, ReferenceHttp1Executor executor) =>
    Json(executor.Stop(sessionId)));

app.MapDelete(TestExecutorRoutes.Sessions + "/{sessionId}", (string sessionId, ReferenceHttp1Executor executor) =>
{
    executor.Delete(sessionId);
    return Results.NoContent();
});

app.Run();

static IResult Json<T>(T value)
{
    return Results.Json(value, ProtocolLabAdapterJson.Options);
}

static IResult Problem(int status, string code, string title, string? sessionId = null)
{
    return Results.Json(
        new TestExecutorProblemDetails
        {
            Type = "https://incursa.com/protocol-lab/problems/reference-http1-test-executor",
            Title = title,
            Status = status,
            Code = code,
            Operation = "reference-http1-test-executor",
            SessionId = sessionId,
            Retryable = false
        },
        ProtocolLabAdapterJson.Options,
        contentType: "application/problem+json",
        statusCode: status);
}

static async Task<T?> ReadJsonAsync<T>(HttpContext context)
{
    return await JsonSerializer.DeserializeAsync<T>(context.Request.Body, ProtocolLabAdapterJson.Options, context.RequestAborted);
}

internal sealed class ReferenceHttp1Executor
{
    private const string ExecutorId = "protocol-lab-reference-http1-test-executor";
    private const string ExecutorName = "ProtocolLab Reference HTTP/1 Test Executor";
    private const string ExecutorVersion = "0.1.0";
    private const string ContractVersion = "test-executor-v1";
    private static readonly JsonSerializerOptions ArtifactJsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly IReadOnlyDictionary<string, HttpScenarioExpectation> SupportedScenarios =
        new Dictionary<string, HttpScenarioExpectation>(StringComparer.OrdinalIgnoreCase)
        {
            ["http.core.plaintext"] = new("GET", "/plaintext", 200, "text/plain", "exact", "Hello, World!", 13),
            ["http.core.json"] = new("GET", "/json", 200, "application/json", "jsonEquivalent", "{\"message\":\"Hello, World!\"}", null),
            ["http.payload.bytes.1kb"] = new("GET", "/bytes/1024", 200, "application/octet-stream", "deterministicBytes", null, 1024)
        };

    private readonly Dictionary<string, ExecutorSession> sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate = new();
    private readonly string artifactRoot;

    public ReferenceHttp1Executor()
    {
        artifactRoot = Environment.GetEnvironmentVariable("PROTOCOL_LAB_ARTIFACTS_DIR")
            ?? Path.Combine(Environment.CurrentDirectory, "artifacts", "reference-http1-test-executor");
    }

    public TestExecutorHealthResponse Health()
    {
        return new TestExecutorHealthResponse
        {
            ExecutorIdentity = Identity(),
            Status = TestExecutorHealthStatus.Ready,
            VersionCompatibility = VersionCompatibility(),
            ObservedAt = DateTimeOffset.UtcNow,
            Capabilities = Capabilities()
        };
    }

    public TestExecutorManifestResponse Manifest()
    {
        return new TestExecutorManifestResponse
        {
            ExecutorIdentity = Identity(),
            VersionCompatibility = VersionCompatibility(),
            ClaimedCapabilities = Capabilities(),
            SupportedTestSelectors = SupportedScenarios.Keys.Select(id => new TestExecutorSelector
            {
                SelectorType = "test-id",
                Expression = id
            }).ToArray(),
            SupportedScenarioSelectors = SupportedScenarios.Keys.Select(id => new TestExecutorSelector
            {
                SelectorType = "scenario-id",
                Expression = id
            }).ToArray(),
            SupportedProtocolFamilies = ["h1"],
            SupportedExecutionModes = ["single-cell", "process"],
            RequiredTargetEndpointBindings =
            [
                new TestExecutorEndpointBinding
                {
                    BindingId = "primary",
                    Purpose = "test-endpoint",
                    EndpointType = "http",
                    Protocols = ["h1"],
                    Required = true
                }
            ],
            SupportedArtifactTypes =
            [
                new TestExecutorArtifactType { Type = "validation.json", ProducedByStates = ["stopped", "failed"] },
                new TestExecutorArtifactType { Type = "result.json", ProducedByStates = ["stopped", "failed"] },
                new TestExecutorArtifactType { Type = "load-tool.stdout.txt", ProducedByStates = ["stopped", "failed"] },
                new TestExecutorArtifactType { Type = "load-tool.stderr.txt", ProducedByStates = ["stopped", "failed"] },
                new TestExecutorArtifactType { Type = "load-tool-execution.json", ProducedByStates = ["stopped", "failed"] }
            ],
            MetricsAvailability = new TestExecutorMetricsAvailability
            {
                Available = true,
                AvailableKinds = ["session", "validation"]
            }
        };
    }

    public TestExecutorSessionResource CreateSession(TestExecutorSessionCreateRequest request)
    {
        var sessionId = string.IsNullOrWhiteSpace(request.RequestedSessionId)
            ? $"reference-http1-{Guid.NewGuid():N}"
            : request.RequestedSessionId;

        var now = DateTimeOffset.UtcNow;
        var session = new ExecutorSession
        {
            SessionId = sessionId,
            State = TestExecutorSessionState.Created,
            RunId = request.RunId,
            CellId = request.CellId,
            CreatedAt = now,
            UpdatedAt = now
        };

        lock (gate)
        {
            sessions[sessionId] = session;
        }

        return Resource(session, Operation(TestExecutorOperationResultCategory.Succeeded, "Session created."));
    }

    public bool TryGetSession(string sessionId, out TestExecutorSessionResource resource)
    {
        if (TryGet(sessionId, out var session))
        {
            resource = Resource(session, Operation(TestExecutorOperationResultCategory.Succeeded, "Session found."));
            return true;
        }

        resource = new TestExecutorSessionResource();
        return false;
    }

    public TestExecutorOperationResult Prepare(string sessionId, TestExecutorPrepareRequest request)
    {
        if (!TryGet(sessionId, out var session))
        {
            return Operation(TestExecutorOperationResultCategory.Rejected, "Session not found.", "session-not-found");
        }

        session.TestId = request.TestId;
        session.ScenarioId = request.ScenarioId;
        session.Protocol = request.Protocol;
        session.RunId = request.RunId;
        session.CellId = request.CellId;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        if (!IsSupportedSelection(request, out var unsupportedReason))
        {
            session.State = TestExecutorSessionState.Unsupported;
            session.LastOperation = Operation(TestExecutorOperationResultCategory.Unsupported, unsupportedReason, "unsupported");
            return session.LastOperation;
        }

        var endpoint = request.TargetEndpoints.FirstOrDefault(endpoint =>
            string.Equals(endpoint.BindingId, "primary", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(endpoint.Purpose, "test-endpoint", StringComparison.OrdinalIgnoreCase));
        if (endpoint is null)
        {
            session.State = TestExecutorSessionState.Unsupported;
            session.LastOperation = Operation(TestExecutorOperationResultCategory.Unsupported, "The primary HTTP target endpoint binding was not supplied.", "unsupported-endpoint-binding");
            return session.LastOperation;
        }

        session.TargetEndpoint = endpoint;
        session.Expectation = SupportedScenarios[request.ScenarioId];
        session.State = TestExecutorSessionState.Prepared;
        session.LastOperation = Operation(TestExecutorOperationResultCategory.Succeeded, "Prepared HTTP/1 scenario.");
        return session.LastOperation;
    }

    public async Task<TestExecutorOperationResult> StartAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (!TryGet(sessionId, out var session))
        {
            return Operation(TestExecutorOperationResultCategory.Rejected, "Session not found.", "session-not-found");
        }

        if (session.State != TestExecutorSessionState.Prepared || session.TargetEndpoint is null || session.Expectation is null)
        {
            return Operation(TestExecutorOperationResultCategory.Rejected, "Session must be prepared before start.", "invalid-transition");
        }

        session.State = TestExecutorSessionState.Running;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            session.Execution = await ExecuteAsync(session, cancellationToken);
            session.State = session.Execution.ValidationErrors.Count == 0 ? TestExecutorSessionState.Stopped : TestExecutorSessionState.Failed;
            session.LastOperation = session.Execution.ValidationErrors.Count == 0
                ? Operation(TestExecutorOperationResultCategory.Succeeded, "HTTP/1 scenario passed.")
                : Operation(TestExecutorOperationResultCategory.Failed, "HTTP/1 scenario validation failed.", "validation-failed");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UriFormatException)
        {
            session.State = TestExecutorSessionState.Failed;
            session.Execution = ReferenceHttp1Execution.Failed(ex.Message);
            session.LastOperation = Operation(TestExecutorOperationResultCategory.Failed, ex.Message, "http-request-failed");
            WriteArtifacts(session);
        }

        session.UpdatedAt = DateTimeOffset.UtcNow;
        return session.LastOperation ?? Operation(TestExecutorOperationResultCategory.Succeeded, "Started.");
    }

    public bool TryGetStatus(string sessionId, out TestExecutorStatusResponse status)
    {
        if (TryGet(sessionId, out var session))
        {
            status = new TestExecutorStatusResponse
            {
                Session = Summary(session),
                Operation = session.LastOperation ?? Operation(TestExecutorOperationResultCategory.Succeeded, "Session status.")
            };
            return true;
        }

        status = new TestExecutorStatusResponse();
        return false;
    }

    public bool TryGetMetrics(string sessionId, out TestExecutorMetricsResponse metrics)
    {
        if (!TryGet(sessionId, out var session))
        {
            metrics = new TestExecutorMetricsResponse();
            return false;
        }

        var execution = session.Execution;
        metrics = new TestExecutorMetricsResponse
        {
            Session = Summary(session),
            Availability = execution is null ? TestExecutorResourceAvailability.Unavailable : TestExecutorResourceAvailability.Available,
            CapturedAt = DateTimeOffset.UtcNow,
            Metrics = execution is null
                ? []
                :
                [
                    Metric("requests.total", "session", "count", 1),
                    Metric("requests.failed", "session", "count", execution.ValidationErrors.Count == 0 ? 0 : 1),
                    Metric("latency.elapsed_ms", "session", "milliseconds", execution.Elapsed.TotalMilliseconds),
                    Metric("response.bytes", "session", "bytes", execution.BodyLength)
                ]
        };
        return true;
    }

    public bool TryGetArtifacts(string sessionId, out TestExecutorArtifactsResponse artifacts)
    {
        if (!TryGet(sessionId, out var session))
        {
            artifacts = new TestExecutorArtifactsResponse();
            return false;
        }

        var available = session.Execution is not null;
        artifacts = new TestExecutorArtifactsResponse
        {
            Session = Summary(session),
            Availability = available ? TestExecutorResourceAvailability.Available : TestExecutorResourceAvailability.Unavailable,
            Artifacts = available
                ?
                [
                    Artifact("validation", "validation.json", session.ArtifactPath("validation.json"), "application/json"),
                    Artifact("result", "result.json", session.ArtifactPath("result.json"), "application/json"),
                    Artifact("stdout", "load-tool.stdout.txt", session.ArtifactPath("load-tool.stdout.txt"), "text/plain"),
                    Artifact("stderr", "load-tool.stderr.txt", session.ArtifactPath("load-tool.stderr.txt"), "text/plain"),
                    Artifact("execution", "load-tool-execution.json", session.ArtifactPath("load-tool-execution.json"), "application/json")
                ]
                : []
        };
        return true;
    }

    public TestExecutorOperationResult Stop(string sessionId)
    {
        if (!TryGet(sessionId, out var session))
        {
            return Operation(TestExecutorOperationResultCategory.Rejected, "Session not found.", "session-not-found");
        }

        if (session.State is TestExecutorSessionState.Running or TestExecutorSessionState.Prepared)
        {
            session.State = TestExecutorSessionState.Stopped;
            session.UpdatedAt = DateTimeOffset.UtcNow;
        }

        return Operation(TestExecutorOperationResultCategory.Succeeded, "Session stopped.");
    }

    public void Delete(string sessionId)
    {
        lock (gate)
        {
            sessions.Remove(sessionId);
        }
    }

    private async Task<ReferenceHttp1Execution> ExecuteAsync(ExecutorSession session, CancellationToken cancellationToken)
    {
        var expectation = session.Expectation ?? throw new InvalidOperationException("Session has no scenario expectation.");
        var endpoint = session.TargetEndpoint ?? throw new InvalidOperationException("Session has no target endpoint.");
        var uri = BuildUri(endpoint, expectation.Path);
        var stopwatch = Stopwatch.StartNew();

        using var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        using var request = new HttpRequestMessage(new HttpMethod(expectation.Method), uri)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        stopwatch.Stop();

        var errors = Validate(expectation, response, body);
        var execution = new ReferenceHttp1Execution(
            uri,
            (int)response.StatusCode,
            response.Content.Headers.ContentType?.MediaType,
            body.Length,
            stopwatch.Elapsed,
            errors);
        session.Execution = execution;
        WriteArtifacts(session);
        return execution;
    }

    private void WriteArtifacts(ExecutorSession session)
    {
        if (session.Execution is null)
        {
            return;
        }

        var directory = session.ArtifactDirectory(artifactRoot);
        Directory.CreateDirectory(directory);

        var common = new
        {
            executor = new { id = ExecutorId, name = ExecutorName, version = ExecutorVersion, contractVersion = ContractVersion },
            sessionId = session.SessionId,
            runId = session.RunId,
            cellId = session.CellId,
            testId = session.TestId,
            scenarioId = session.ScenarioId,
            protocol = session.Protocol,
            targetUri = session.Execution.TargetUri.ToString(),
            observedAt = DateTimeOffset.UtcNow
        };

        File.WriteAllText(Path.Combine(directory, "validation.json"), JsonSerializer.Serialize(new
        {
            common.executor,
            common.sessionId,
            common.runId,
            common.cellId,
            common.testId,
            common.scenarioId,
            common.protocol,
            common.targetUri,
            passed = session.Execution.ValidationErrors.Count == 0,
            errors = session.Execution.ValidationErrors,
            statusCode = session.Execution.StatusCode,
            contentType = session.Execution.ContentType,
            bodyLength = session.Execution.BodyLength,
            common.observedAt
        }, ArtifactJsonOptions));

        File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new
        {
            common.executor,
            common.sessionId,
            common.runId,
            common.cellId,
            common.testId,
            common.scenarioId,
            common.protocol,
            validationStatus = session.Execution.ValidationErrors.Count == 0 ? "passed" : "failed",
            metrics = new
            {
                requestsTotal = 1,
                requestsFailed = session.Execution.ValidationErrors.Count == 0 ? 0 : 1,
                elapsedMs = session.Execution.Elapsed.TotalMilliseconds,
                responseBytes = session.Execution.BodyLength
            },
            common.observedAt
        }, ArtifactJsonOptions));

        File.WriteAllText(Path.Combine(directory, "load-tool.stdout.txt"),
            $"executor={ExecutorId} version={ExecutorVersion}{Environment.NewLine}scenario={session.ScenarioId}{Environment.NewLine}target={session.Execution.TargetUri}{Environment.NewLine}");
        File.WriteAllText(Path.Combine(directory, "load-tool.stderr.txt"), string.Empty);
        File.WriteAllText(Path.Combine(directory, "load-tool-execution.json"), JsonSerializer.Serialize(common, ArtifactJsonOptions));
    }

    private bool TryGet(string sessionId, out ExecutorSession session)
    {
        lock (gate)
        {
            return sessions.TryGetValue(sessionId, out session!);
        }
    }

    private static bool IsSupportedSelection(TestExecutorPrepareRequest request, out string reason)
    {
        if (!string.Equals(request.Protocol, "h1", StringComparison.OrdinalIgnoreCase))
        {
            reason = $"Protocol '{request.Protocol}' is unsupported. This reference executor supports only h1.";
            return false;
        }

        if (!SupportedScenarios.ContainsKey(request.ScenarioId))
        {
            reason = $"Scenario '{request.ScenarioId}' is unsupported by this HTTP/1 reference executor.";
            return false;
        }

        if (!SupportedScenarios.ContainsKey(request.TestId))
        {
            reason = $"Test '{request.TestId}' is unsupported by this HTTP/1 reference executor.";
            return false;
        }

        reason = "";
        return true;
    }

    private static Uri BuildUri(TestExecutorTargetEndpoint endpoint, string scenarioPath)
    {
        var builder = new UriBuilder(endpoint.Scheme, endpoint.Host, endpoint.Port);
        var basePath = string.IsNullOrWhiteSpace(endpoint.Path) ? "/" : endpoint.Path;
        var baseUri = new Uri(builder.Uri, basePath.TrimEnd('/') + "/");
        return new Uri(baseUri, scenarioPath.TrimStart('/'));
    }

    private static IReadOnlyList<string> Validate(HttpScenarioExpectation expectation, HttpResponseMessage response, byte[] body)
    {
        var errors = new List<string>();
        var bodyText = Encoding.UTF8.GetString(body);

        if ((int)response.StatusCode != expectation.ExpectedStatus)
        {
            errors.Add($"Expected status {expectation.ExpectedStatus}, got {(int)response.StatusCode}.");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is null || !contentType.Contains(expectation.ExpectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Expected content type containing '{expectation.ExpectedContentType}', got '{contentType ?? "<missing>"}'.");
        }

        if (expectation.ExpectedBodySize is not null && body.Length != expectation.ExpectedBodySize.Value)
        {
            errors.Add($"Expected body size {expectation.ExpectedBodySize.Value}, got {body.Length}.");
        }

        if (string.Equals(expectation.ExpectedBodyRule, "exact", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(expectation.ExpectedBody, bodyText, StringComparison.Ordinal))
        {
            errors.Add("Response body did not match the exact expected body.");
        }

        if (string.Equals(expectation.ExpectedBodyRule, "jsonEquivalent", StringComparison.OrdinalIgnoreCase) &&
            !JsonEquivalent(expectation.ExpectedBody, bodyText))
        {
            errors.Add("Response body was not JSON-equivalent to the expected body.");
        }

        if (string.Equals(expectation.ExpectedBodyRule, "deterministicBytes", StringComparison.OrdinalIgnoreCase) &&
            !body.SequenceEqual(CreateDeterministicBytes(body.Length)))
        {
            errors.Add("Response body did not match the deterministic byte pattern.");
        }

        return errors;
    }

    private static bool JsonEquivalent(string? expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return string.IsNullOrWhiteSpace(actual);
        }

        try
        {
            using var expectedJson = JsonDocument.Parse(expected);
            using var actualJson = JsonDocument.Parse(actual);
            return JsonElement.DeepEquals(expectedJson.RootElement, actualJson.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static byte[] CreateDeterministicBytes(int size)
    {
        var bytes = new byte[size];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)(index % 251);
        }

        return bytes;
    }

    private static TestExecutorIdentity Identity()
    {
        return new TestExecutorIdentity
        {
            Id = ExecutorId,
            Name = ExecutorName,
            Version = ExecutorVersion,
            Vendor = "Incursa"
        };
    }

    private static TestExecutorVersionCompatibility VersionCompatibility()
    {
        return new TestExecutorVersionCompatibility
        {
            ContractVersion = ContractVersion,
            CompatibleContractVersions = [ContractVersion],
            ExecutorVersion = ExecutorVersion
        };
    }

    private static IReadOnlyList<TestExecutorCapability> Capabilities()
    {
        return
        [
            new TestExecutorCapability { Id = "http.core.plaintext", Status = TestExecutorCapabilityStatus.Supported },
            new TestExecutorCapability { Id = "http.core.json", Status = TestExecutorCapabilityStatus.Supported },
            new TestExecutorCapability { Id = "http.payload.bytes.1kb", Status = TestExecutorCapabilityStatus.Supported }
        ];
    }

    private static TestExecutorSessionResource Resource(ExecutorSession session, TestExecutorOperationResult operation)
    {
        return new TestExecutorSessionResource
        {
            Session = Summary(session),
            Operation = operation
        };
    }

    private static TestExecutorSessionSummary Summary(ExecutorSession session)
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
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt
        };
    }

    private static TestExecutorOperationResult Operation(TestExecutorOperationResultCategory category, string message, string? code = null)
    {
        return new TestExecutorOperationResult
        {
            Category = category,
            Message = message,
            Code = code,
            Retryable = false
        };
    }

    private static TestExecutorMetric Metric(string id, string scope, string unit, double value)
    {
        return new TestExecutorMetric
        {
            MetricId = id,
            Scope = scope,
            Unit = unit,
            Value = ProtocolLabAdapterJson.SerializeValue(value),
            CapturedAt = DateTimeOffset.UtcNow
        };
    }

    private static TestExecutorArtifact Artifact(string id, string type, string path, string contentType)
    {
        return new TestExecutorArtifact
        {
            ArtifactId = id,
            ArtifactType = type,
            Status = TestExecutorResourceAvailability.Available,
            Path = path,
            ContentType = contentType,
            Final = true
        };
    }

    private sealed class ExecutorSession
    {
        public required string SessionId { get; init; }

        public TestExecutorSessionState State { get; set; }

        public string? TestId { get; set; }

        public string? ScenarioId { get; set; }

        public string? Protocol { get; set; }

        public string? RunId { get; set; }

        public string? CellId { get; set; }

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset UpdatedAt { get; set; }

        public TestExecutorTargetEndpoint? TargetEndpoint { get; set; }

        public HttpScenarioExpectation? Expectation { get; set; }

        public ReferenceHttp1Execution? Execution { get; set; }

        public TestExecutorOperationResult? LastOperation { get; set; }

        public string ArtifactDirectory(string root)
        {
            return Path.Combine(root, Sanitize(SessionId));
        }

        public string ArtifactPath(string fileName)
        {
            return Path.Combine("artifacts", "reference-http1-test-executor", Sanitize(SessionId), fileName).Replace('\\', '/');
        }

        private static string Sanitize(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                builder.Append(char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '-');
            }

            return builder.ToString();
        }
    }
}

internal sealed record HttpScenarioExpectation(
    string Method,
    string Path,
    int ExpectedStatus,
    string ExpectedContentType,
    string ExpectedBodyRule,
    string? ExpectedBody,
    int? ExpectedBodySize);

internal sealed record ReferenceHttp1Execution(
    Uri TargetUri,
    int StatusCode,
    string? ContentType,
    int BodyLength,
    TimeSpan Elapsed,
    IReadOnlyList<string> ValidationErrors)
{
    public static ReferenceHttp1Execution Failed(string error)
    {
        return new ReferenceHttp1Execution(new Uri("http://127.0.0.1/"), 0, null, 0, TimeSpan.Zero, [error]);
    }
}
