// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Tests.Fixtures.RunnerContractLab;

namespace Incursa.ProtocolLab.Tests;

public sealed class ReferenceHttp1TestExecutorTests
{
    [Fact]
    public async Task Reference_executor_drives_plaintext_http1_scenario()
    {
        await using var target = await FixtureHttpEndpointHost.StartAsync(0, "Hello, World!");
        await using var executor = await ReferenceHttp1ExecutorProcess.StartAsync();
        using var httpClient = new HttpClient { BaseAddress = executor.BaseUri };
        var client = new ProtocolLabTestExecutorClient(httpClient);
        var targetUri = new Uri(target.BaseUrl);

        var session = await client.CreateSessionAsync(new TestExecutorSessionCreateRequest
        {
            RequestedSessionId = "reference-http1-plaintext",
            RunId = "reference-http1-test",
            CellId = "plaintext"
        });

        var prepare = await client.PrepareAsync(session.Session.SessionId, CreatePrepareRequest(targetUri, "http.core.plaintext", "h1"));
        Assert.Equal(TestExecutorOperationResultCategory.Succeeded, prepare.Category);

        var start = await client.StartAsync(session.Session.SessionId);
        Assert.Equal(TestExecutorOperationResultCategory.Succeeded, start.Category);

        var status = await client.GetStatusAsync(session.Session.SessionId);
        Assert.Equal(TestExecutorSessionState.Stopped, status.Session.State);

        var metrics = await client.GetMetricsAsync(session.Session.SessionId);
        Assert.Equal(TestExecutorResourceAvailability.Available, metrics.Availability);
        Assert.Contains(metrics.Metrics, metric => metric.MetricId == "requests.total");

        var artifacts = await client.GetArtifactsAsync(session.Session.SessionId);
        Assert.Equal(TestExecutorResourceAvailability.Available, artifacts.Availability);
        Assert.Contains(artifacts.Artifacts, artifact => artifact.ArtifactType == "validation.json");

        var validationArtifact = await File.ReadAllTextAsync(Path.Combine(executor.ArtifactRoot, "reference-http1-plaintext", "validation.json"));
        Assert.Contains("protocol-lab-reference-http1-test-executor", validationArtifact, StringComparison.Ordinal);
        Assert.Contains("0.1.0", validationArtifact, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reference_executor_prepare_fails_closed_for_unsupported_scenarios()
    {
        await using var executor = await ReferenceHttp1ExecutorProcess.StartAsync();
        using var httpClient = new HttpClient { BaseAddress = executor.BaseUri };
        var client = new ProtocolLabTestExecutorClient(httpClient);

        var session = await client.CreateSessionAsync(new TestExecutorSessionCreateRequest
        {
            RequestedSessionId = "reference-http1-unsupported",
            RunId = "reference-http1-test",
            CellId = "unsupported"
        });

        var prepare = await client.PrepareAsync(
            session.Session.SessionId,
            CreatePrepareRequest(new Uri("http://127.0.0.1:1"), "http.core.status", "h1"));

        Assert.Equal(TestExecutorOperationResultCategory.Unsupported, prepare.Category);

        var status = await client.GetStatusAsync(session.Session.SessionId);
        Assert.Equal(TestExecutorSessionState.Unsupported, status.Session.State);
    }

    private static TestExecutorPrepareRequest CreatePrepareRequest(Uri targetUri, string scenarioId, string protocol)
    {
        return new TestExecutorPrepareRequest
        {
            TestId = scenarioId,
            ScenarioId = scenarioId,
            ScenarioVersion = "1.0",
            Protocol = protocol,
            TestDocument = ProtocolLabAdapterJson.SerializeValue(new Dictionary<string, string> { ["kind"] = "reference-http1-test" }),
            ScenarioDocument = ProtocolLabAdapterJson.SerializeValue(new Dictionary<string, string> { ["kind"] = "reference-http1-scenario" }),
            TargetEndpoints =
            [
                new TestExecutorTargetEndpoint
                {
                    BindingId = "primary",
                    EndpointId = "target-001",
                    Purpose = "test-endpoint",
                    Scheme = targetUri.Scheme,
                    Protocol = protocol,
                    Host = targetUri.Host,
                    Port = targetUri.Port,
                    Path = "/"
                }
            ],
            RunId = "reference-http1-test",
            CellId = scenarioId,
            ArtifactOutputExpectations =
            [
                new TestExecutorArtifactExpectation
                {
                    ArtifactType = "validation.json",
                    Required = true
                }
            ]
        };
    }

    private sealed class ReferenceHttp1ExecutorProcess : IAsyncDisposable
    {
        private readonly Process process;

        private ReferenceHttp1ExecutorProcess(Process process, Uri baseUri, string artifactRoot)
        {
            this.process = process;
            BaseUri = baseUri;
            ArtifactRoot = artifactRoot;
        }

        public Uri BaseUri { get; }

        public string ArtifactRoot { get; }

        public static async Task<ReferenceHttp1ExecutorProcess> StartAsync()
        {
            var port = GetAvailablePort();
            var baseUri = new Uri($"http://127.0.0.1:{port.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            var artifactRoot = Path.Combine(Path.GetTempPath(), $"protocol-lab-reference-http1-{Guid.NewGuid():N}");
            var projectPath = Path.Combine(TestPaths.RepoRoot, "tools", "test-executors", "reference-http1", "src", "ReferenceHttp1TestExecutor.csproj");
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = TestPaths.RepoRoot,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(projectPath);
            startInfo.ArgumentList.Add("--urls");
            startInfo.ArgumentList.Add(baseUri.ToString());
            startInfo.Environment["PROTOCOL_LAB_ARTIFACTS_DIR"] = artifactRoot;

            var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start reference HTTP/1 test executor.");
            var fixture = new ReferenceHttp1ExecutorProcess(process, baseUri, artifactRoot);
            await fixture.WaitForReadyAsync();
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync();
            process.Dispose();

            if (Directory.Exists(ArtifactRoot))
            {
                Directory.Delete(ArtifactRoot, recursive: true);
            }
        }

        private async Task WaitForReadyAsync()
        {
            using var client = new HttpClient { BaseAddress = BaseUri, Timeout = TimeSpan.FromMilliseconds(500) };
            var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (process.HasExited)
                {
                    var stdout = await process.StandardOutput.ReadToEndAsync();
                    var stderr = await process.StandardError.ReadToEndAsync();
                    throw new InvalidOperationException($"Reference HTTP/1 test executor exited early.{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
                }

                try
                {
                    using var response = await client.GetAsync(TestExecutorRoutes.Health);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return;
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                }

                await Task.Delay(250);
            }

            throw new TimeoutException("Reference HTTP/1 test executor did not become ready.");
        }

        private static int GetAvailablePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
