// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

[assembly: SupportedOSPlatform("windows")]
[assembly: SupportedOSPlatform("linux")]
[assembly: SupportedOSPlatform("macos")]

var port = args.Length > 0 && int.TryParse(args[0], out var parsedPort) ? parsedPort : 0;
var alpn = Environment.GetEnvironmentVariable("PROTOCOL_LAB_INCURSA_RAW_QUIC_ALPN") ?? "plab-raw-quic";
var certSubject = Environment.GetEnvironmentVariable("PROTOCOL_LAB_INCURSA_RAW_QUIC_CERT_SUBJECT") ?? "CN=Incursa-RawQuic-Local";

var certificate = GenerateSelfSignedCertificate(certSubject);
var alpnProtocol = new SslApplicationProtocol(alpn);
var endpoint = new IPEndPoint(IPAddress.Loopback, port);

var listenerOptions = new QuicListenerOptions
{
    ListenEndPoint = endpoint,
    ApplicationProtocols = [alpnProtocol],
    ConnectionOptionsCallback = (_, _, _) =>
    {
        return ValueTask.FromResult(new QuicServerConnectionOptions
        {
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0,
            ServerAuthenticationOptions = new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,
                ApplicationProtocols = [alpnProtocol]
            }
        });
    }
};

var listener = await QuicListener.ListenAsync(listenerOptions);
var bindEndpoint = listener.LocalEndPoint;

Console.Error.WriteLine($"IncursaRawQuicServer listening on {bindEndpoint} with ALPN '{alpn}'");
Console.WriteLine($"QUIC_ENDPOINT={bindEndpoint.Address}:{bindEndpoint.Port}");
Console.WriteLine($"QUIC_PORT={bindEndpoint.Port}");
Console.WriteLine($"QUIC_ALPN={alpn}");
Console.WriteLine($"QUIC_IMPLEMENTATION=incursa-raw-quic");

try
{
    while (true)
    {
        var connection = await listener.AcceptConnectionAsync(default);
        _ = HandleConnectionAsync(connection, default);
    }
}
catch (OperationCanceledException)
{
}
catch (ObjectDisposedException)
{
}
finally
{
    await listener.DisposeAsync();
}

static async Task HandleConnectionAsync(QuicConnection connection, CancellationToken cancellationToken)
{
    try
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var stream = await connection.AcceptInboundStreamAsync(cancellationToken);
            _ = HandleStreamAsync(stream, cancellationToken);
        }
    }
    catch (OperationCanceledException) { }
    catch (QuicException) { }
    catch (ObjectDisposedException) { }
    finally
    {
        await connection.DisposeAsync();
    }
}

static async Task HandleStreamAsync(QuicStream stream, CancellationToken cancellationToken)
{
    try
    {
        var buffer = new byte[65536];
        while ((await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (stream.CanWrite)
            {
                await stream.WriteAsync(buffer, cancellationToken);
            }
        }
        if (stream.CanWrite)
        {
            await stream.WriteAsync(ReadOnlyMemory<byte>.Empty, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
    }
    catch (OperationCanceledException) { }
    catch (QuicException) { }
    finally
    {
        await stream.DisposeAsync();
    }
}

static X509Certificate2 GenerateSelfSignedCertificate(string subject)
{
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
    request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
    var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
    return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), (string?)null);
}
