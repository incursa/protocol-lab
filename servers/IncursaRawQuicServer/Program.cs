// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Incursa.Quic;
using Incursa.Quic.Qlog;

var port = args.Length > 0 && int.TryParse(args[0], out var parsedPort) ? parsedPort : 0;
var listenPort = port > 0 ? port : GetFreePort();
var alpn = Environment.GetEnvironmentVariable("PROTOCOL_LAB_INCURSA_RAW_QUIC_ALPN") ?? "plab-raw-quic";
var certSubject = Environment.GetEnvironmentVariable("PROTOCOL_LAB_INCURSA_RAW_QUIC_CERT_SUBJECT") ?? "CN=Incursa-RawQuic-Local";
var qlogPath = Environment.GetEnvironmentVariable("PROTOCOL_LAB_INCURSA_RAW_QUIC_QLOG_PATH");
var payloadDirection = Environment.GetEnvironmentVariable("PROTOCOL_LAB_INCURSA_RAW_QUIC_PAYLOAD_DIRECTION") ?? "bidirectional";
var echoResponses = !string.Equals(payloadDirection, "client-to-server", StringComparison.OrdinalIgnoreCase);

var certificate = GenerateSelfSignedCertificate(certSubject);
var alpnProtocol = new SslApplicationProtocol(alpn);
var debugLogging = string.Equals(Environment.GetEnvironmentVariable("PROTOCOL_LAB_INCURSA_RAW_QUIC_DEBUG"), "1", StringComparison.Ordinal);
var connectionCount = 0;
var captureQlog = debugLogging || !string.IsNullOrWhiteSpace(qlogPath);
var qlogCapture = captureQlog
    ? new QuicQlogCapture("IncursaRawQuicServer", "Incursa raw QUIC debug capture")
    : null;
CancellationTokenSource? qlogFlushCts = null;
Task? qlogFlushTask = null;

var listenerOptions = new QuicListenerOptions
{
    ListenEndPoint = new IPEndPoint(IPAddress.Loopback, listenPort),
    ApplicationProtocols = [alpnProtocol],
    ConnectionOptionsCallback = (_, _, _) =>
    {
        if (debugLogging)
        {
            Console.Error.WriteLine($"IncursaRawQuicServer accepted handshake proposal for ALPN '{alpn}'");
        }

        return ValueTask.FromResult(new QuicServerConnectionOptions
        {
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0,
            ServerAuthenticationOptions = new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,
                ApplicationProtocols = [alpnProtocol],
                EnabledSslProtocols = SslProtocols.Tls13,
                EncryptionPolicy = EncryptionPolicy.RequireEncryption
            }
        });
    }
};

var listener = qlogCapture is null
    ? await QuicListener.ListenAsync(listenerOptions)
    : await qlogCapture.ListenAsync(listenerOptions);

if (qlogCapture is not null && !string.IsNullOrWhiteSpace(qlogPath))
{
    var snapshotPath = qlogPath;
    var snapshotDirectory = Path.GetDirectoryName(snapshotPath);
    if (!string.IsNullOrWhiteSpace(snapshotDirectory))
    {
        Directory.CreateDirectory(snapshotDirectory);
    }

    qlogFlushCts = new CancellationTokenSource();
    qlogFlushTask = Task.Run(async () =>
    {
        while (!qlogFlushCts.IsCancellationRequested)
        {
            await File.WriteAllTextAsync(snapshotPath, qlogCapture.ToJson(indented: true));
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), qlogFlushCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    });
}

Console.Error.WriteLine($"IncursaRawQuicServer listening on 127.0.0.1:{listenPort} with ALPN '{alpn}'");
Console.WriteLine($"QUIC_ENDPOINT=127.0.0.1:{listenPort}");
Console.WriteLine($"QUIC_PORT={listenPort}");
Console.WriteLine($"QUIC_ALPN={alpn}");
Console.WriteLine($"QUIC_IMPLEMENTATION=incursa-raw-quic");

try
{
    while (true)
    {
        var connection = await listener.AcceptConnectionAsync(default);
        var connectionIndex = Interlocked.Increment(ref connectionCount);
        if (debugLogging)
        {
            Console.Error.WriteLine($"IncursaRawQuicServer accepted connection #{connectionIndex} for ALPN '{alpn}'");
        }

        _ = HandleConnectionAsync(connection, connectionIndex, default, debugLogging, echoResponses);
    }
}
catch (OperationCanceledException)
{
}
catch (ObjectDisposedException)
{
}
catch (QuicException ex)
{
    if (debugLogging)
    {
        Console.Error.WriteLine($"IncursaRawQuicServer listener stopped with QUIC error: {ex.Message}");
    }
}
finally
{
    if (qlogFlushCts is not null)
    {
        qlogFlushCts.Cancel();
        try
        {
            if (qlogFlushTask is not null)
            {
                await qlogFlushTask;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    await listener.DisposeAsync();

    if (qlogCapture is not null && !string.IsNullOrWhiteSpace(qlogPath))
    {
        var directory = Path.GetDirectoryName(qlogPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(qlogPath, qlogCapture.ToJson(indented: true));
        if (debugLogging)
        {
            Console.Error.WriteLine($"IncursaRawQuicServer wrote qlog capture to '{qlogPath}'");
        }
    }
}

static async Task HandleConnectionAsync(QuicConnection connection, int connectionIndex, CancellationToken cancellationToken, bool debugLogging, bool echoResponses)
{
    try
    {
        var streamIndex = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var stream = await connection.AcceptInboundStreamAsync(cancellationToken);
            var acceptedStreamIndex = Interlocked.Increment(ref streamIndex);
            if (debugLogging)
            {
                Console.Error.WriteLine($"IncursaRawQuicServer accepted inbound stream #{acceptedStreamIndex} on connection #{connectionIndex}");
            }

            _ = HandleStreamAsync(stream, connectionIndex, acceptedStreamIndex, cancellationToken, debugLogging, echoResponses);
        }
    }
    catch (OperationCanceledException)
    {
        if (debugLogging)
        {
            Console.Error.WriteLine($"IncursaRawQuicServer connection #{connectionIndex} stopped: cancellation requested");
        }
    }
    catch (QuicException ex)
    {
        if (debugLogging)
        {
            Console.Error.WriteLine($"IncursaRawQuicServer connection #{connectionIndex} stopped with QUIC error: {ex.Message}");
        }
    }
    catch (ObjectDisposedException ex)
    {
        if (debugLogging)
        {
            Console.Error.WriteLine($"IncursaRawQuicServer connection #{connectionIndex} disposed: {ex.Message}");
        }
    }
    finally
    {
        if (debugLogging)
        {
            Console.Error.WriteLine($"IncursaRawQuicServer closing connection #{connectionIndex}");
        }

        await connection.DisposeAsync();
    }
}

static async Task HandleStreamAsync(QuicStream stream, int connectionIndex, int streamIndex, CancellationToken cancellationToken, bool debugLogging, bool echoResponses)
{
    try
    {
        if (debugLogging)
        {
            Console.Error.WriteLine($"IncursaRawQuicServer handling stream #{streamIndex} on connection #{connectionIndex}");
        }

        var buffer = new byte[65536];
        using var responseBuffer = echoResponses ? new MemoryStream() : null;

        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (bytesRead <= 0)
            {
                if (debugLogging)
                {
                Console.Error.WriteLine($"IncursaRawQuicServer stream #{streamIndex} on connection #{connectionIndex} reached EOF after read loop");
                }
                break;
            }

            if (debugLogging)
            {
                Console.Error.WriteLine($"IncursaRawQuicServer stream #{streamIndex} on connection #{connectionIndex} read {bytesRead} byte(s)");
            }

            if (responseBuffer is not null)
            {
                await responseBuffer.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }
        }

        if (stream.CanWrite)
        {
            if (responseBuffer is not null && responseBuffer.Length > 0)
            {
                responseBuffer.Position = 0;
                await responseBuffer.CopyToAsync(stream, cancellationToken);

                if (debugLogging)
                {
                    Console.Error.WriteLine($"IncursaRawQuicServer stream #{streamIndex} on connection #{connectionIndex} wrote {responseBuffer.Length} byte(s)");
                }
            }

            await stream.CompleteWritesAsync(cancellationToken);
            if (debugLogging)
            {
                Console.Error.WriteLine($"IncursaRawQuicServer stream #{streamIndex} on connection #{connectionIndex} completed writes");
            }
        }
    }
    catch (OperationCanceledException)
    {
        if (debugLogging)
        {
            Console.Error.WriteLine($"IncursaRawQuicServer stream #{streamIndex} on connection #{connectionIndex} canceled");
        }
    }
    catch (QuicException ex)
    {
        if (debugLogging)
        {
            Console.Error.WriteLine($"IncursaRawQuicServer stream #{streamIndex} on connection #{connectionIndex} failed with QUIC error: {ex.Message}");
        }
    }
    finally
    {
        if (debugLogging)
        {
            Console.Error.WriteLine($"IncursaRawQuicServer closing stream #{streamIndex} on connection #{connectionIndex}");
        }

        await stream.DisposeAsync();
    }
}

static X509Certificate2 GenerateSelfSignedCertificate(string subject)
{
    using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    var request = new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA256);
    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
    request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
    var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
    return X509CertificateLoader.LoadPkcs12(
        cert.Export(X509ContentType.Pfx),
        (string?)null,
        X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet);
}

static int GetFreePort()
{
    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    return ((IPEndPoint)socket.LocalEndPoint!).Port;
}
