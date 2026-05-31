// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Incursa.ProtocolLab.Adapter.Contracts;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Incursa.ProtocolLab.Adapters.Kestrel;

internal static class KestrelProtocolEndpointLauncher
{
    public static async Task<KestrelEndpointProcess> StartAsync(
        string repositoryRoot,
        KestrelSession session,
        AdapterEndpointPlan plan,
        string benchmarkServerProjectPath,
        TimeProvider timeProvider,
        TimeSpan httpTimeout,
        CancellationToken cancellationToken)
    {
        var projectPath = Path.IsPathFullyQualified(benchmarkServerProjectPath)
            ? benchmarkServerProjectPath
            : Path.GetFullPath(Path.Combine(repositoryRoot, benchmarkServerProjectPath));

        var stdoutPath = session.StdoutPath;
        var stderrPath = session.StderrPath;
        var selectedPort = plan.Port;

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);

        startInfo.Environment["PROTOCOL_LAB_ENABLE_EXPLICIT_ENDPOINTS"] = "true";
        startInfo.Environment["PROTOCOL_LAB_ENDPOINTS"] = plan.Protocol == "h1" ? "http" : "https";
        startInfo.Environment["PROTOCOL_LAB_H1_URL"] = plan.Protocol == "h1"
            ? plan.BaseUrl
            : $"http://127.0.0.1:{AllocateFreePort().ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        startInfo.Environment["PROTOCOL_LAB_HTTPS_URL"] = plan.Protocol == "h1"
            ? $"https://127.0.0.1:{AllocateFreePort().ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            : plan.BaseUrl;
        startInfo.Environment["PROTOCOL_LAB_HTTPS_PROTOCOLS"] = plan.Protocol == "h2"
            ? "http2"
            : "http1andhttp2andhttp3";
        if (!string.Equals(plan.Protocol, "h1", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.Environment["PROTOCOL_LAB_GENERATE_LOCAL_CERT"] = "true";
        }
        startInfo.Environment["PROTOCOL_LAB_IMPLEMENTATION"] = "kestrel-http3";

        var commandLine = BuildCommandLine(startInfo.FileName, startInfo.ArgumentList.ToArray());
        await File.WriteAllTextAsync(session.CommandLinePath, commandLine, cancellationToken);

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the Kestrel benchmark server.");

        var stdoutTask = CopyToFileAsync(process.StandardOutput, stdoutPath, cancellationToken);
        var stderrTask = CopyToFileAsync(process.StandardError, stderrPath, cancellationToken);

        return new KestrelEndpointProcess(
            process,
            stdoutTask,
            stderrTask,
            new AdapterEndpoint
            {
                EndpointId = "endpoint-001",
                Purpose = "server",
                Scheme = plan.Scheme,
                Protocol = plan.Protocol,
                Host = "127.0.0.1",
                Port = selectedPort,
                Path = "/",
                Authority = $"127.0.0.1:{selectedPort.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                SocketAddress = $"127.0.0.1:{selectedPort.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                NetworkMode = "process-local",
                BindMode = "loopback",
                Tls = plan.Scheme == "https"
                    ? new AdapterTlsNotes
                    {
                        CertificateMode = "generated-local-development-certificate",
                        CertificateNotes = "The Kestrel benchmark server generates a local development certificate for loopback HTTPS/HTTP/3 sessions.",
                        Sni = "localhost",
                        VerificationNotes = "Loopback certificate validation is bypassed by the adapter control plane for local testing."
                    }
                    : null
            },
            commandLine,
            stdoutPath,
            stderrPath);
    }

    private static async Task CopyToFileAsync(TextReader reader, string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        while (!cancellationToken.IsCancellationRequested)
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

    private static string BuildCommandLine(string executable, string[] arguments)
    {
        var parts = new List<string> { Quote(executable) };
        parts.AddRange(arguments.Select(Quote));
        return string.Join(" ", parts);
    }

    private static string Quote(string value)
    {
        return value.Contains(' ') || value.Contains('"')
            ? "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
            : value;
    }

    private static int AllocateFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

internal sealed class KestrelEndpointProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly Task stdoutTask;
    private readonly Task stderrTask;

    public KestrelEndpointProcess(
        Process process,
        Task stdoutTask,
        Task stderrTask,
        AdapterEndpoint endpoint,
        string commandLine,
        string stdoutPath,
        string stderrPath)
    {
        this.process = process;
        this.stdoutTask = stdoutTask;
        this.stderrTask = stderrTask;
        Endpoint = endpoint;
        CommandLine = commandLine;
        StdoutPath = stdoutPath;
        StderrPath = stderrPath;
        StartTimeUtc = DateTimeOffset.UtcNow;
    }

    public AdapterEndpoint Endpoint { get; }

    public string CommandLine { get; }

    public string StdoutPath { get; }

    public string StderrPath { get; }

    public int ProcessId => process.Id;

    public bool HasExited
    {
        get
        {
            try
            {
                return process.HasExited;
            }
            catch
            {
                return true;
            }
        }
    }

    public int? ExitCode => HasExited ? process.ExitCode : null;

    public DateTimeOffset StartTimeUtc { get; }

    public long? WorkingSetBytes
    {
        get
        {
            try
            {
                process.Refresh();
                return process.WorkingSet64;
            }
            catch
            {
                return null;
            }
        }
    }

    public double? CpuSeconds
    {
        get
        {
            try
            {
                process.Refresh();
                return process.TotalProcessorTime.TotalSeconds;
            }
            catch
            {
                return null;
            }
        }
    }

    public void Refresh()
    {
        try
        {
            process.Refresh();
        }
        catch
        {
        }
    }

    public async Task StopAsync()
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync();
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
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

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
