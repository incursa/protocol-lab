// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using Incursa.ProtocolLab.Adapter.Contracts;

namespace Incursa.ProtocolLab.Tests.Fixtures.KestrelAdapterLab;

internal sealed record KestrelAdapterProcessOptions
{
    public int ControlPlanePort { get; init; } = 53171;

    public string BenchmarkServerProjectPath { get; init; } = Path.Combine(
        "servers",
        "KestrelBenchServer",
        "KestrelBenchServer.csproj");

    public string ReadinessProbePath { get; init; } = "/plaintext";

    public int ReadinessTimeoutSeconds { get; init; } = 10;

    public int HttpTimeoutSeconds { get; init; } = 5;
}

internal sealed class KestrelAdapterProcessHost : IAsyncDisposable
{
    private readonly Process process;
    private readonly Task stdoutTask;
    private readonly Task stderrTask;

    private KestrelAdapterProcessHost(
        Process process,
        Task stdoutTask,
        Task stderrTask,
        HttpClient client,
        string stdoutPath,
        string stderrPath)
    {
        this.process = process;
        this.stdoutTask = stdoutTask;
        this.stderrTask = stderrTask;
        Client = client;
        StdoutPath = stdoutPath;
        StderrPath = stderrPath;
    }

    public HttpClient Client { get; }

    public string StdoutPath { get; }

    public string StderrPath { get; }

    public static async Task<KestrelAdapterProcessHost> StartAsync(KestrelAdapterProcessOptions? options = null)
    {
        options ??= new KestrelAdapterProcessOptions();
        var repoRoot = TestPaths.RepoRoot;
        var adapterProjectPath = Path.Combine(
            repoRoot,
            "src",
            "Incursa.ProtocolLab.Adapters.Kestrel",
            "Incursa.ProtocolLab.Adapters.Kestrel.csproj");
        var baseAddress = new Uri($"http://127.0.0.1:{options.ControlPlanePort}", UriKind.Absolute);
        var stdoutPath = Path.Combine(Path.GetTempPath(), $"kestrel-adapter-{Guid.NewGuid():N}.stdout.txt");
        var stderrPath = Path.Combine(Path.GetTempPath(), $"kestrel-adapter-{Guid.NewGuid():N}.stderr.txt");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(adapterProjectPath);
        startInfo.Environment["ASPNETCORE_URLS"] = baseAddress.ToString();
        startInfo.Environment["PROTOCOL_LAB_KESTREL_BENCHMARK_PROJECT_PATH"] = options.BenchmarkServerProjectPath;
        startInfo.Environment["PROTOCOL_LAB_KESTREL_READINESS_PROBE_PATH"] = options.ReadinessProbePath;
        startInfo.Environment["PROTOCOL_LAB_KESTREL_READINESS_TIMEOUT_SECONDS"] = options.ReadinessTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        startInfo.Environment["PROTOCOL_LAB_KESTREL_HTTP_TIMEOUT_SECONDS"] = options.HttpTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the Kestrel adapter process.");

        var stdoutTask = CopyToFileAsync(process.StandardOutput, stdoutPath);
        var stderrTask = CopyToFileAsync(process.StandardError, stderrPath);
        var client = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(60)
        };

        var host = new KestrelAdapterProcessHost(process, stdoutTask, stderrTask, client, stdoutPath, stderrPath);
        try
        {
            await host.WaitForHealthAsync(TimeSpan.FromSeconds(30));
            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync();
        }
        catch
        {
        }

        try
        {
            await Task.WhenAll(stdoutTask, stderrTask);
        }
        catch
        {
        }

        process.Dispose();
    }

    private async Task WaitForHealthAsync(TimeSpan timeout)
    {
        using var probeClient = new HttpClient
        {
            BaseAddress = Client.BaseAddress,
            Timeout = TimeSpan.FromSeconds(2)
        };
        var client = new ProtocolLabAdapterClient(probeClient);
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                var stdout = await File.ReadAllTextAsync(StdoutPath);
                var stderr = await File.ReadAllTextAsync(StderrPath);
                throw new InvalidOperationException(
                    $"The Kestrel adapter exited before it became healthy. Stdout: {stdout} Stderr: {stderr}");
            }

            try
            {
                var health = await client.GetHealthAsync();
                if (health.Status == AdapterHealthStatus.Ready)
                {
                    return;
                }
            }
            catch
            {
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("The Kestrel adapter control plane did not become healthy in time.");
    }

    private static async Task CopyToFileAsync(TextReader reader, string path)
    {
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream, Encoding.UTF8);

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line is null)
            {
                break;
            }

            await writer.WriteLineAsync(line);
            await writer.FlushAsync();
        }
    }
}
