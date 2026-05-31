// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;

namespace Incursa.ProtocolLab.Adapters.MsQuicDotNet;

internal sealed class MsQuicDotNetQuicServer
{
    private readonly QuicListener listener;
    private readonly CancellationTokenSource stopCts = new();
    private readonly Task acceptLoop;
    private readonly string serverLogPath;
    private int acceptedConnections;
    private int openedStreams;
    private long bytesReceived;
    private long bytesSent;
    private bool hasFailed;
    private string? failureReason;

    private MsQuicDotNetQuicServer(
        QuicListener listener,
        AdapterEndpoint endpoint,
        string serverLogPath)
    {
        this.listener = listener;
        Endpoint = endpoint;
        this.serverLogPath = serverLogPath;
        IsListening = true;
        acceptLoop = AcceptConnectionsAsync(stopCts.Token);
    }

    public AdapterEndpoint Endpoint { get; }

    public bool IsListening { get; private set; }

    public bool HasFailed => hasFailed;

    public string? FailureReason => failureReason;

    public static async Task<MsQuicDotNetQuicServer> StartAsync(
        MsQuicDotNetSession session,
        MsQuicDotNetEndpointPlan plan,
        MsQuicDotNetAdapterOptions options)
    {
        if (!QuicListener.IsSupported)
        {
            throw new InvalidOperationException(
                "System.Net.Quic is not supported on this platform. " +
                "Install the msquic package or use a supported OS (Windows 11+, Linux with libmsquic).");
        }

        var certificate = GenerateSelfSignedCertificate(plan.CertificateSubject);
        var alpn = new SslApplicationProtocol(plan.Alpn);
        var port = options.QuicPort > 0 ? options.QuicPort : GetFreePort();
        var endpoint = new IPEndPoint(IPAddress.Loopback, port);

        var listenerOptions = new QuicListenerOptions
        {
            ListenEndPoint = endpoint,
            ApplicationProtocols = [alpn],
            ConnectionOptionsCallback = (connection, clientHello, cancellationToken) =>
            {
                return ValueTask.FromResult(new QuicServerConnectionOptions
                {
                    DefaultStreamErrorCode = 0,
                    DefaultCloseErrorCode = 0,
                    ServerAuthenticationOptions = new SslServerAuthenticationOptions
                    {
                        ServerCertificate = certificate,
                        ApplicationProtocols = [alpn]
                    }
                });
            }
        };

        var listener = await QuicListener.ListenAsync(listenerOptions);
        var alpnValue = plan.Alpn;

        var adapterEndpoint = new AdapterEndpoint
        {
            EndpointId = "endpoint-quic-001",
            Purpose = "server",
            Scheme = "quic",
            Protocol = "quic",
            Host = "127.0.0.1",
            Port = port,
            Authority = $"127.0.0.1:{port}",
            SocketAddress = $"127.0.0.1:{port}",
            NetworkMode = "process-local",
            BindMode = "loopback",
            Tls = new AdapterTlsNotes
            {
                CertificateMode = "self-signed-development",
                CertificateNotes = $"MSQuic/.NET generated self-signed certificate with subject '{plan.CertificateSubject}'.",
                Sni = "localhost",
                VerificationNotes = "Loopback certificate validation is bypassed by the adapter control plane for local testing."
            },
            Extensions = new Dictionary<string, JsonElement>
            {
                ["alpn"] = ProtocolLabAdapterJson.SerializeValue(new[] { alpnValue }),
                ["sni"] = ProtocolLabAdapterJson.SerializeValue("localhost"),
                ["transport"] = ProtocolLabAdapterJson.SerializeValue("udp"),
                ["streamBehavior"] = ProtocolLabAdapterJson.SerializeValue("bidirectional"),
                ["supportedStreamDirections"] = ProtocolLabAdapterJson.SerializeValue(new[] { "bidirectional" }),
                ["datagramSupported"] = ProtocolLabAdapterJson.SerializeValue(false),
                ["zeroRttSupported"] = ProtocolLabAdapterJson.SerializeValue(false)
            }
        };

        await File.AppendAllTextAsync(session.ServerLogPath,
            $"[{DateTimeOffset.UtcNow:O}] QUIC listener started on {endpoint} with ALPN '{plan.Alpn}'{Environment.NewLine}");

        return new MsQuicDotNetQuicServer(listener, adapterEndpoint, session.ServerLogPath);
    }

    public async Task StopAsync()
    {
        IsListening = false;
        stopCts.Cancel();

        try
        {
            await listener.DisposeAsync();
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            await acceptLoop;
        }
        catch (OperationCanceledException)
        {
        }

        await File.AppendAllTextAsync(serverLogPath,
            $"[{DateTimeOffset.UtcNow:O}] QUIC listener stopped.{Environment.NewLine}");
    }

    public IReadOnlyList<(string MetricId, string Scope, object Value, string Notes)> GetMetrics()
    {
        return new List<(string, string, object, string)>
        {
            ("quic.connections.accepted", "endpoint", (long)Volatile.Read(ref acceptedConnections), "Total accepted QUIC connections."),
            ("quic.streams.opened", "endpoint", (long)Volatile.Read(ref openedStreams), "Total accepted QUIC streams."),
            ("quic.bytes.received", "endpoint", (long)Volatile.Read(ref bytesReceived), "Total bytes received."),
            ("quic.bytes.sent", "endpoint", (long)Volatile.Read(ref bytesSent), "Total bytes sent.")
        };
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var connection = await listener.AcceptConnectionAsync(cancellationToken);
                Interlocked.Increment(ref acceptedConnections);
                _ = HandleConnectionAsync(connection, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (QuicException ex) when (ex.HResult == -2147467259 || ex.QuicError == QuicError.OperationAborted)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            hasFailed = true;
            failureReason = ex.Message;
            await File.AppendAllTextAsync(serverLogPath,
                $"[{DateTimeOffset.UtcNow:O}] Accept loop error: {ex}{Environment.NewLine}");
        }
    }

    private async Task HandleConnectionAsync(QuicConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await File.AppendAllTextAsync(serverLogPath,
                $"[{DateTimeOffset.UtcNow:O}] Connection accepted: {connection.RemoteEndPoint}{Environment.NewLine}");

            while (!cancellationToken.IsCancellationRequested)
            {
                var stream = await connection.AcceptInboundStreamAsync(cancellationToken);
                Interlocked.Increment(ref openedStreams);
                _ = HandleStreamAsync(stream, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (QuicException ex) when (ex.QuicError is QuicError.OperationAborted or QuicError.ConnectionTimeout)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            await File.AppendAllTextAsync(serverLogPath,
                $"[{DateTimeOffset.UtcNow:O}] Connection error: {ex.Message}{Environment.NewLine}");
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    private async Task HandleStreamAsync(QuicStream stream, CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new byte[65536];
            long streamBytesRead = 0;

            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                Interlocked.Add(ref bytesReceived, bytesRead);
                streamBytesRead += bytesRead;

                if (stream.CanWrite)
                {
                    await stream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    Interlocked.Add(ref bytesSent, bytesRead);
                }
            }

            if (stream.CanWrite)
            {
                await stream.WriteAsync(ReadOnlyMemory<byte>.Empty, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            await File.AppendAllTextAsync(serverLogPath,
                $"[{DateTimeOffset.UtcNow:O}] Stream completed: {streamBytesRead} bytes{Environment.NewLine}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (QuicException ex) when (ex.QuicError is QuicError.StreamAborted or QuicError.ConnectionTimeout)
        {
        }
        catch (Exception ex)
        {
            await File.AppendAllTextAsync(serverLogPath,
                $"[{DateTimeOffset.UtcNow:O}] Stream error: {ex.Message}{Environment.NewLine}");
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }

    private static X509Certificate2 GenerateSelfSignedCertificate(string subject)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            subject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                false));

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.1")],
                false));

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(5));

        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), (string?)null);
    }

    private static int GetFreePort()
    {
        using var socket = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.InterNetwork,
            System.Net.Sockets.SocketType.Dgram,
            System.Net.Sockets.ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }
}
