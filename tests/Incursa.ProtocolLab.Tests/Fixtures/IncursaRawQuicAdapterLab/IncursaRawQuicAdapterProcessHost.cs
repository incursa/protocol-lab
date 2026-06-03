// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Text;
using Incursa.ProtocolLab.Adapter.Contracts;

namespace Incursa.ProtocolLab.Tests.Fixtures.IncursaRawQuicAdapterLab;

internal sealed record IncursaRawQuicAdapterProcessOptions
{
    public int ControlPlanePort { get; init; } = 53591;
    public string? WorkingDirectory { get; init; }
    public string QuicAlpn { get; init; } = "plab-raw-quic";
    public int QuicPort { get; init; }
    public int ReadinessTimeoutSeconds { get; init; } = 10;
    public int HttpTimeoutSeconds { get; init; } = 5;
}

internal sealed class IncursaRawQuicAdapterProcessHost : IAsyncDisposable
{
    private readonly Process process;
    private readonly Task stdoutTask;
    private readonly Task stderrTask;

    private IncursaRawQuicAdapterProcessHost(Process p, Task so, Task se, HttpClient c, string sop, string sep)
    { process = p; stdoutTask = so; stderrTask = se; Client = c; StdoutPath = sop; StderrPath = sep; }

    public HttpClient Client { get; }
    public string StdoutPath { get; }
    public string StderrPath { get; }

    public static async Task<IncursaRawQuicAdapterProcessHost> StartAsync(IncursaRawQuicAdapterProcessOptions? opts = null)
    {
        opts ??= new();
        var root = TestPaths.RepoRoot;
        var proj = Path.Combine(root, "src", "Incursa.ProtocolLab.Adapters.IncursaRawQuic", "Incursa.ProtocolLab.Adapters.IncursaRawQuic.csproj");
        var workingDirectory = string.IsNullOrWhiteSpace(opts.WorkingDirectory) ? root : opts.WorkingDirectory!;
        var addr = new Uri($"http://127.0.0.1:{opts.ControlPlanePort}");
        var sop = Path.Combine(Path.GetTempPath(), $"incursa-raw-quic-adapter-{Guid.NewGuid():N}.stdout.txt");
        var sep = Path.Combine(Path.GetTempPath(), $"incursa-raw-quic-adapter-{Guid.NewGuid():N}.stderr.txt");
        var si = new ProcessStartInfo("dotnet") { WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        si.ArgumentList.Add("run"); si.ArgumentList.Add("--no-build"); si.ArgumentList.Add("--no-restore"); si.ArgumentList.Add("--no-launch-profile"); si.ArgumentList.Add("--project"); si.ArgumentList.Add(proj);
        si.Environment["ASPNETCORE_URLS"] = addr.ToString();
        si.Environment["PROTOCOL_LAB_INCURSA_RAW_QUIC_ALPN"] = opts.QuicAlpn;
        si.Environment["PROTOCOL_LAB_INCURSA_RAW_QUIC_READINESS_TIMEOUT_SECONDS"] = opts.ReadinessTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        si.Environment["PROTOCOL_LAB_INCURSA_RAW_QUIC_HTTP_TIMEOUT_SECONDS"] = opts.HttpTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        si.Environment["DOTNET_NOLOGO"] = "1"; si.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        if (opts.QuicPort > 0) si.Environment["PROTOCOL_LAB_INCURSA_RAW_QUIC_PORT"] = opts.QuicPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var p = Process.Start(si) ?? throw new InvalidOperationException("Failed to start Incursa raw QUIC adapter.");
        var sto = CopyToFileAsync(p.StandardOutput, sop);
        var ste = CopyToFileAsync(p.StandardError, sep);
        var c = new HttpClient { BaseAddress = addr, Timeout = TimeSpan.FromSeconds(60) };
        var host = new IncursaRawQuicAdapterProcessHost(p, sto, ste, c, sop, sep);
        try { await host.WaitForHealthAsync(TimeSpan.FromSeconds(30)); return host; }
        catch { await host.DisposeAsync(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } catch { }
        try { await Task.WhenAll(stdoutTask, stderrTask); } catch { }
        process.Dispose();
    }

    private async Task WaitForHealthAsync(TimeSpan timeout)
    {
        using var pc = new HttpClient { BaseAddress = Client.BaseAddress, Timeout = TimeSpan.FromSeconds(2) };
        var cl = new ProtocolLabAdapterClient(pc);
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (process.HasExited) { var so = await File.ReadAllTextAsync(StdoutPath); var se = await File.ReadAllTextAsync(StderrPath); throw new InvalidOperationException($"Adapter exited. Stdout: {so} Stderr: {se}"); }
            try { var h = await cl.GetHealthAsync(); if (h.Status is AdapterHealthStatus.Ready or AdapterHealthStatus.Degraded) return; } catch { }
            await Task.Delay(250);
        }
        throw new TimeoutException("Incursa raw QUIC adapter did not become healthy.");
    }

    private static async Task CopyToFileAsync(TextReader r, string path)
    { await using var s = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read); await using var w = new StreamWriter(s, Encoding.UTF8); while (true) { var line = await r.ReadLineAsync(); if (line is null) break; await w.WriteLineAsync(line); await w.FlushAsync(); } }
}
