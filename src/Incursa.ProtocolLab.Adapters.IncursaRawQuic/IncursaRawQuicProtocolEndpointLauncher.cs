// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;

namespace Incursa.ProtocolLab.Adapters.IncursaRawQuic;

internal static class IncursaRawQuicProtocolEndpointLauncher
{
    public static async Task<IncursaRawQuicEndpointProcess> StartAsync(string repositoryRoot, IncursaRawQuicSession session, IncursaRawQuicEndpointPlan plan, string projectPath, CancellationToken ct)
    {
        var resolvedProject = Path.IsPathFullyQualified(projectPath) ? projectPath : Path.GetFullPath(Path.Combine(repositoryRoot, projectPath));
        var projectDirectory = Path.GetDirectoryName(resolvedProject) ?? repositoryRoot;
        var assemblyName = Path.GetFileNameWithoutExtension(resolvedProject);
        var port = plan.Port > 0 ? plan.Port : GetFreePort();

        var si = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        var directExecDll = ResolveBuiltServerDll(projectDirectory, assemblyName);
        if (directExecDll is not null)
        {
            // Prefer the built Release/Debug output so startup does not pay the dotnet-run build cost.
            si.ArgumentList.Add("exec");
            si.ArgumentList.Add(directExecDll);
            si.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        else
        {
            si.ArgumentList.Add("run");
            si.ArgumentList.Add("--configuration");
            si.ArgumentList.Add("Release");
            si.ArgumentList.Add("--no-restore");
            si.ArgumentList.Add("--no-launch-profile");
            si.ArgumentList.Add("--project");
            si.ArgumentList.Add(resolvedProject);
            si.ArgumentList.Add("--");
            si.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        si.Environment["PROTOCOL_LAB_INCURSA_RAW_QUIC_ALPN"] = plan.Alpn;
        si.Environment["PROTOCOL_LAB_INCURSA_RAW_QUIC_CERT_SUBJECT"] = plan.CertificateSubject;
        var payloadDirection = plan.Scenario.QuicTransport?.PayloadDirection;
        if (!string.IsNullOrWhiteSpace(payloadDirection))
        {
            si.Environment["PROTOCOL_LAB_INCURSA_RAW_QUIC_PAYLOAD_DIRECTION"] = payloadDirection;
        }
        var debugLogging = Environment.GetEnvironmentVariable("PROTOCOL_LAB_INCURSA_RAW_QUIC_DEBUG");
        if (!string.IsNullOrWhiteSpace(debugLogging))
        {
            si.Environment["PROTOCOL_LAB_INCURSA_RAW_QUIC_DEBUG"] = debugLogging;
        }

        var qlogPath = Environment.GetEnvironmentVariable("PROTOCOL_LAB_INCURSA_RAW_QUIC_QLOG_PATH");
        if (!string.IsNullOrWhiteSpace(qlogPath))
        {
            si.Environment["PROTOCOL_LAB_INCURSA_RAW_QUIC_QLOG_PATH"] = qlogPath;
        }

        var cl = BuildCommandLine(si.FileName, [.. si.ArgumentList]);
        await File.WriteAllTextAsync(session.CommandLinePath, cl, ct);

        var process = Process.Start(si) ?? throw new InvalidOperationException("Failed to start Incursa raw QUIC server.");
        var stdoutTask = CopyToFileAsync(process.StandardOutput, session.StdoutPath, ct);
        var stderrTask = CopyToFileAsync(process.StandardError, session.StderrPath, ct);

        return new IncursaRawQuicEndpointProcess(process, stdoutTask, stderrTask, new AdapterEndpoint
        {
            EndpointId = "endpoint-quic-001", Purpose = "server", Scheme = "quic", Protocol = "quic",
            Host = "127.0.0.1", Port = port, Authority = $"127.0.0.1:{port}", SocketAddress = $"127.0.0.1:{port}",
            NetworkMode = "process-local", BindMode = "loopback",
            Tls = new AdapterTlsNotes { CertificateMode = "incursa-raw-quic-self-signed", CertificateNotes = $"Incursa raw QUIC server self-signed certificate subject '{plan.CertificateSubject}'.", Sni = "localhost", VerificationNotes = "Loopback certificate validation bypassed." },
            Extensions = new Dictionary<string, JsonElement> { ["alpn"] = ProtocolLabAdapterJson.SerializeValue(new[] { plan.Alpn }), ["sni"] = ProtocolLabAdapterJson.SerializeValue("localhost"), ["transport"] = ProtocolLabAdapterJson.SerializeValue("udp"), ["streamBehavior"] = ProtocolLabAdapterJson.SerializeValue("bidirectional"), ["supportedStreamDirections"] = ProtocolLabAdapterJson.SerializeValue(new[] { "bidirectional" }), ["datagramSupported"] = ProtocolLabAdapterJson.SerializeValue(false), ["zeroRttSupported"] = ProtocolLabAdapterJson.SerializeValue(false) }
        }, cl, session.StdoutPath, session.StderrPath, port);
    }

    private static string? ResolveBuiltServerDll(string projectDirectory, string assemblyName)
    {
        var candidateRoots = new[]
        {
            Path.Combine(projectDirectory, "bin", "Release"),
            Path.Combine(projectDirectory, "bin", "Debug")
        };

        foreach (var candidateRoot in candidateRoots)
        {
            if (!Directory.Exists(candidateRoot))
            {
                continue;
            }

            var match = Directory.EnumerateFiles(candidateRoot, assemblyName + ".dll", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .FirstOrDefault();

            if (match is not null)
            {
                return match.FullName;
            }
        }

        return null;
    }

    private static async Task CopyToFileAsync(TextReader r, string path, CancellationToken ct) { await using var s = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read); await using var w = new StreamWriter(s, Encoding.UTF8); while (!ct.IsCancellationRequested) { var line = await r.ReadLineAsync(); if (line is null) break; await w.WriteLineAsync(line); await w.FlushAsync(); } }
    private static string BuildCommandLine(string exe, string[] args) => string.Join(" ", new[] { exe }.Concat(args).Select(a => a.Contains(' ') ? "\"" + a.Replace("\"", "\\\"") + "\"" : a));
    private static int GetFreePort() { using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp); socket.Bind(new IPEndPoint(IPAddress.Loopback, 0)); return ((IPEndPoint)socket.LocalEndPoint!).Port; }
}

internal sealed class IncursaRawQuicEndpointProcess : IAsyncDisposable
{
    private readonly Process process; private readonly Task stdoutTask; private readonly Task stderrTask; private bool ready;

    public IncursaRawQuicEndpointProcess(Process process, Task stdoutTask, Task stderrTask, AdapterEndpoint endpoint, string commandLine, string stdoutPath, string stderrPath, int quicPort)
    {
        this.process = process; this.stdoutTask = stdoutTask; this.stderrTask = stderrTask; Endpoint = endpoint; CommandLine = commandLine; StdoutPath = stdoutPath; StderrPath = stderrPath; QuicPort = quicPort;
        _ = MonitorStdoutAsync();
    }

    public AdapterEndpoint Endpoint { get; } public string CommandLine { get; } public string StdoutPath { get; } public string StderrPath { get; } public int QuicPort { get; }
    public int ProcessId => process.Id;
    public bool HasExited { get { try { return process.HasExited; } catch { return true; } } }
    public int? ExitCode => HasExited ? process.ExitCode : null;
    public bool IsReady => ready;
    public long? WorkingSetBytes { get { try { process.Refresh(); return process.WorkingSet64; } catch { return null; } } }
    public double? CpuSeconds { get { try { process.Refresh(); return process.TotalProcessorTime.TotalSeconds; } catch { return null; } } }
    public void Refresh() { try { process.Refresh(); } catch { } }

    private async Task MonitorStdoutAsync()
    {
        try
        {
            using var reader = new StreamReader(new FileStream(StdoutPath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite));
            while (!process.HasExited)
            {
                var line = await reader.ReadLineAsync();
                if (line is not null && line.StartsWith("QUIC_PORT=", StringComparison.OrdinalIgnoreCase))
                {
                    ready = true;
                    break;
                }
                if (line is null) await Task.Delay(100);
            }
        }
        catch { }
    }

    public async Task StopAsync()
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } catch { }
        try { await Task.WhenAll(stdoutTask, stderrTask); } catch { }
        process.Dispose();
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
