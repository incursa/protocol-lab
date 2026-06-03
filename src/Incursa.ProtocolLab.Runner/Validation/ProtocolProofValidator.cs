// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

internal sealed record ToolCapability(
    bool Available,
    string Status,
    string? ExecutablePath = null,
    string? Version = null,
    IReadOnlyList<string>? Warnings = null);

internal sealed record CurlProofParseResult(
    string? HttpVersion,
    int? StatusCode,
    string? ContentType,
    byte[] Body,
    IReadOnlyList<string> Errors);

internal sealed record ManagedProofResponse(
    Version? ResponseVersion,
    HttpStatusCode StatusCode,
    string? ContentType,
    byte[] Body,
    IReadOnlyList<string> Warnings);

internal static class ProtocolProofValidator
{
    private const string CurlMethod = "curl --http3-only";
    private const string ManagedMethod = "managed-httpclient-h3-exact";
    private const string HttpVersionMarker = "__PROTOCOL_LAB_HTTP_VERSION__:";
    private const string StatusMarker = "__PROTOCOL_LAB_STATUS__:";
    private const string ContentTypeMarker = "__PROTOCOL_LAB_CONTENT_TYPE__:";

    public static async Task<ScenarioValidationResult> ValidateH3Async(
        RunCell cell,
        string baseUrl,
        ArtifactPaths paths,
        string certificateMode)
    {
        await EnsureProofArtifactsAsync(paths);

        var endpoint = cell.Scenario.Endpoint!;
        var uri = HttpScenarioValidator.BuildScenarioUri(baseUrl, endpoint);
        var curlCapability = await DetectCurlCapabilityAsync();
        if (curlCapability.Available && string.Equals(curlCapability.Status, "http3-supported", StringComparison.OrdinalIgnoreCase))
        {
            return await ValidateWithCurlAsync(cell, uri, paths, certificateMode, curlCapability);
        }

        var managedResult = await ValidateWithManagedHttpClientAsync(cell, uri, paths, certificateMode, curlCapability);
        await File.WriteAllTextAsync(paths.ProtocolProofJson, ResultJson.Serialize(managedResult.ProtocolProof));
        return managedResult;
    }

    public static Task<ToolCapability> DetectManagedHttpClientH3CapabilityAsync()
    {
        var warnings = new List<string>();
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return Task.FromResult(new ToolCapability(false, "platform-unsupported", Version: RuntimeInformation.FrameworkDescription));
        }

        warnings.Add("Managed HTTP/3 support is attemptable, but final capability is unknown until validation against a live HTTPS/H3 endpoint.");
        return Task.FromResult(new ToolCapability(true, "unknown-until-validation", Version: RuntimeInformation.FrameworkDescription, Warnings: warnings));
    }

    public static async Task<ToolCapability> DetectCurlCapabilityAsync()
    {
        var executable = LoadToolInvoker.ResolveExecutable("curl");
        if (executable is null)
        {
            return new ToolCapability(false, "unavailable");
        }

        var versionRun = await RunProcessAsync(executable, ["--version"]);
        var helpRun = await RunProcessAsync(executable, ["--help", "all"]);
        var optionProbeRun = await RunProcessAsync(executable, ["--http3-only", "--version"]);
        var combined = Encoding.UTF8.GetString(versionRun.Stdout.Concat(versionRun.Stderr).Concat(helpRun.Stdout).Concat(helpRun.Stderr).ToArray());
        var version = Encoding.UTF8.GetString(versionRun.Stdout.Concat(versionRun.Stderr).ToArray()).Trim();
        var hasHttp3Feature = Regex.IsMatch(combined, @"\bHTTP3\b|\bHTTP/3\b", RegexOptions.IgnoreCase);
        var hasHttp3Only = combined.Contains("--http3-only", StringComparison.OrdinalIgnoreCase);
        var acceptsHttp3Only = optionProbeRun.ExitCode == 0;

        if (hasHttp3Feature && hasHttp3Only && acceptsHttp3Only)
        {
            return new ToolCapability(true, "http3-supported", executable, version);
        }

        var warnings = new List<string>();
        if (!hasHttp3Feature)
        {
            warnings.Add("curl --version does not advertise HTTP3.");
        }

        if (!hasHttp3Only)
        {
            warnings.Add("curl --help all does not advertise --http3-only.");
        }

        if (!acceptsHttp3Only)
        {
            var stderr = Encoding.UTF8.GetString(optionProbeRun.Stderr).Trim();
            warnings.Add(string.IsNullOrWhiteSpace(stderr)
                ? "curl rejected --http3-only."
                : $"curl rejected --http3-only: {stderr}");
        }

        return new ToolCapability(true, "http3-unsupported", executable, version, warnings);
    }

    public static async Task<ToolCapability> DetectH2loadH3CapabilityAsync()
    {
        var executable = LoadToolInvoker.ResolveExecutable("h2load");
        if (executable is null)
        {
            return new ToolCapability(false, "unavailable");
        }

        var versionRun = await RunProcessAsync(executable, ["--version"]);
        var helpRun = await RunProcessAsync(executable, ["--help"]);
        var combined = Encoding.UTF8.GetString(versionRun.Stdout.Concat(versionRun.Stderr).Concat(helpRun.Stdout).Concat(helpRun.Stderr).ToArray());
        var version = Encoding.UTF8.GetString(versionRun.Stdout.Concat(versionRun.Stderr).ToArray()).Trim();
        return combined.Contains("--h3", StringComparison.OrdinalIgnoreCase)
            ? new ToolCapability(true, "h3-supported", executable, version)
            : new ToolCapability(true, "h3-unsupported", executable, version, ["h2load help does not advertise --h3."]);
    }

    public static async Task<ToolCapability> DetectOhaH3CapabilityAsync()
    {
        var executable = LoadToolInvoker.ResolveExecutable("oha");
        if (executable is null)
        {
            return new ToolCapability(false, "unavailable");
        }

        var versionRun = await RunProcessAsync(executable, ["--version"]);
        var helpRun = await RunProcessAsync(executable, ["--help"]);
        var combined = Encoding.UTF8.GetString(versionRun.Stdout.Concat(versionRun.Stderr).Concat(helpRun.Stdout).Concat(helpRun.Stderr).ToArray());
        var version = Encoding.UTF8.GetString(versionRun.Stdout.Concat(versionRun.Stderr).ToArray()).Trim();
        var advertisesHttp3 = combined.Contains("http-version", StringComparison.OrdinalIgnoreCase) &&
            Regex.IsMatch(combined, @"\b3\b|\bHTTP/3\b", RegexOptions.IgnoreCase);

        return advertisesHttp3
            ? new ToolCapability(true, "h3-advertised-experimental", executable, version, ["oha may advertise HTTP/3, but this harness does not treat oha H3 as proven in Phase 2D.1."])
            : new ToolCapability(true, "h3-unsupported", executable, version, ["oha help does not clearly advertise HTTP/3 support."]);
    }

    internal static IReadOnlyList<string> BuildCurlArguments(Uri uri)
    {
        return
        [
            "--http3-only",
            "--insecure",
            "--silent",
            "--show-error",
            "--write-out",
            $"{Environment.NewLine}{HttpVersionMarker}%{{http_version}}{Environment.NewLine}{StatusMarker}%{{http_code}}{Environment.NewLine}{ContentTypeMarker}%{{content_type}}{Environment.NewLine}",
            uri.ToString()
        ];
    }

    internal static CurlProofParseResult ParseCurlProof(byte[] stdout)
    {
        var text = Encoding.UTF8.GetString(stdout);
        var markerIndex = text.IndexOf(HttpVersionMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return new CurlProofParseResult(null, null, null, stdout, ["curl proof output did not contain protocol-lab markers."]);
        }

        var body = Encoding.UTF8.GetBytes(text[..markerIndex].TrimEnd('\r', '\n'));
        var metadata = text[markerIndex..];
        var httpVersion = FindMarker(metadata, HttpVersionMarker);
        var statusText = FindMarker(metadata, StatusMarker);
        var contentType = FindMarker(metadata, ContentTypeMarker);
        var errors = new List<string>();
        int? statusCode = null;

        if (int.TryParse(statusText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStatus))
        {
            statusCode = parsedStatus;
        }
        else
        {
            errors.Add("curl proof output did not contain a valid HTTP status marker.");
        }

        return new CurlProofParseResult(httpVersion, statusCode, contentType, body, errors);
    }

    internal static ScenarioValidationResult BuildManagedProofValidationResult(
        RunCell cell,
        Uri uri,
        ArtifactPaths paths,
        string certificateMode,
        ManagedProofResponse response,
        string curlCapabilityStatus,
        bool certificateBypassUsed)
    {
        var responseVersion = FormatVersion(response.ResponseVersion);
        var fallbackDetected = responseVersion is not null && !IsHttp3Version(responseVersion);
        var errors = new List<string>();
        var warnings = new List<string>(response.Warnings);

        if (fallbackDetected)
        {
            errors.Add($"HTTP/3 was requested with exact version policy, but HttpClient reported response version '{responseVersion}'.");
        }

        if (responseVersion is null)
        {
            errors.Add("HttpClient did not report a response version.");
        }

        errors.AddRange(HttpScenarioValidator.ValidateResponse(
            cell.Scenario.Endpoint!,
            response.StatusCode,
            response.ContentType,
            response.Body,
            HttpScenarioValidator.CreateRequestBody(cell.Scenario.Endpoint!.RequestBodyGeneration)));

        var status = errors.Count == 0 ? ValidationStatus.Passed : ValidationStatus.Failed;
        var proof = BuildProof(
            status,
            cell.Protocol,
            status == ValidationStatus.Passed ? "h3" : responseVersion,
            ManagedMethod,
            "managed-dotnet-httpclient",
            uri,
            commandLine: null,
            paths,
            certificateMode: certificateBypassUsed ? $"{certificateMode}; loopback-certificate-validation-bypass" : certificateMode,
            httpsBaseUrl: new Uri(uri.GetLeftPart(UriPartial.Authority)).ToString().TrimEnd('/'),
            fallbackDetected,
            curlCapabilityStatus,
            responseVersion,
            (int)response.StatusCode,
            response.ContentType,
            errors,
            warnings);

        return status == ValidationStatus.Passed
            ? new ScenarioValidationResult
            {
                ScenarioId = cell.Scenario.Id,
                TargetId = cell.Implementation.Id,
                AdapterId = "",
                Protocol = cell.Protocol,
                Status = ValidationStatus.Passed,
                Summary = "Endpoint behavior matched scenario over proven HTTP/3.",
                ProtocolProof = proof
            }
            : new ScenarioValidationResult
            {
                ScenarioId = cell.Scenario.Id,
                TargetId = cell.Implementation.Id,
                AdapterId = "",
                Protocol = cell.Protocol,
                Status = ValidationStatus.Failed,
                Summary = "HTTP/3 endpoint validation or managed protocol proof failed.",
                Errors = errors,
                Warnings = warnings,
                ProtocolProof = proof
            };
    }

    private static async Task<ScenarioValidationResult> ValidateWithCurlAsync(
        RunCell cell,
        Uri uri,
        ArtifactPaths paths,
        string certificateMode,
        ToolCapability capability)
    {
        var endpoint = cell.Scenario.Endpoint!;
        var arguments = BuildCurlArguments(uri);
        var commandLine = FormatCommandLine(capability.ExecutablePath!, arguments);
        var run = await RunProcessAsync(capability.ExecutablePath!, arguments);
        await File.WriteAllBytesAsync(paths.ProtocolProofStdout, run.Stdout);
        await File.WriteAllBytesAsync(paths.ProtocolProofStderr, run.Stderr);

        var parsed = ParseCurlProof(run.Stdout);
        var errors = new List<string>();
        var warnings = new List<string>();

        if (run.ExitCode != 0)
        {
            errors.Add($"curl --http3-only exited with code {run.ExitCode}.");
            var stderr = Encoding.UTF8.GetString(run.Stderr).Trim();
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                errors.Add(stderr);
            }
        }

        errors.AddRange(parsed.Errors);
        var fallbackDetected = parsed.HttpVersion is not null && !IsHttp3Version(parsed.HttpVersion);
        if (fallbackDetected)
        {
            errors.Add($"HTTP/3 was requested, but curl reported HTTP version '{parsed.HttpVersion}'.");
        }

        if (parsed.HttpVersion is null)
        {
            errors.Add("curl did not report the negotiated HTTP version.");
        }

        var responseErrors = parsed.StatusCode.HasValue
            ? HttpScenarioValidator.ValidateResponse(
                endpoint,
                (HttpStatusCode)parsed.StatusCode.Value,
                parsed.ContentType,
                parsed.Body,
                HttpScenarioValidator.CreateRequestBody(endpoint.RequestBodyGeneration))
            : ["curl did not report an HTTP status code."];
        errors.AddRange(responseErrors);

        var status = errors.Count == 0 ? ValidationStatus.Passed : ValidationStatus.Failed;
        var proofResult = BuildProof(
            status,
            cell.Protocol,
            status == ValidationStatus.Passed ? "h3" : parsed.HttpVersion,
            CurlMethod,
            "curl",
            uri,
            commandLine,
            paths,
            certificateMode,
            new Uri(uri.GetLeftPart(UriPartial.Authority)).ToString().TrimEnd('/'),
            fallbackDetected,
            capability.Status,
            parsed.HttpVersion,
            parsed.StatusCode,
            parsed.ContentType,
            errors,
            warnings);
        await File.WriteAllTextAsync(paths.ProtocolProofJson, ResultJson.Serialize(proofResult));

        return status == ValidationStatus.Passed
            ? new ScenarioValidationResult
            {
                ScenarioId = cell.Scenario.Id,
                TargetId = cell.Implementation.Id,
                AdapterId = "",
                Protocol = cell.Protocol,
                Status = ValidationStatus.Passed,
                Summary = "Endpoint behavior matched scenario over proven HTTP/3.",
                ProtocolProof = proofResult
            }
            : new ScenarioValidationResult
            {
                ScenarioId = cell.Scenario.Id,
                TargetId = cell.Implementation.Id,
                AdapterId = "",
                Protocol = cell.Protocol,
                Status = ValidationStatus.Failed,
                Summary = "HTTP/3 endpoint validation or protocol proof failed.",
                Errors = errors,
                Warnings = warnings,
                ProtocolProof = proofResult
            };
    }

    private static async Task<ScenarioValidationResult> ValidateWithManagedHttpClientAsync(
        RunCell cell,
        Uri uri,
        ArtifactPaths paths,
        string certificateMode,
        ToolCapability curlCapability)
    {
        var stderr = new List<string>();
        var certificateBypassUsed = ShouldBypassCertificateValidation(uri, certificateMode);

        try
        {
            var response = await SendManagedHttp3RequestAsync(cell, uri, certificateBypassUsed);
            var result = BuildManagedProofValidationResult(cell, uri, paths, certificateMode, response, curlCapability.Status, certificateBypassUsed);
            await WriteManagedProofArtifactsAsync(paths, result.ProtocolProof!, response.Body, stderr);
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException or InvalidOperationException or IOException)
        {
            var status = LooksLikeUnsupportedH3(ex) ? ValidationStatus.Unsupported : ValidationStatus.Failed;
            var errors = new List<string>
            {
                status == ValidationStatus.Unsupported
                    ? $"Managed HttpClient HTTP/3 proof is unsupported in this environment: {ex.Message}"
                    : $"Managed HttpClient HTTP/3 proof failed: {ex.Message}"
            };
            stderr.Add(ex.ToString());
            var proof = BuildProof(
                status,
                cell.Protocol,
                null,
                ManagedMethod,
                "managed-dotnet-httpclient",
                uri,
                commandLine: null,
                paths,
                certificateMode: certificateBypassUsed ? $"{certificateMode}; loopback-certificate-validation-bypass" : certificateMode,
                httpsBaseUrl: new Uri(uri.GetLeftPart(UriPartial.Authority)).ToString().TrimEnd('/'),
                fallbackDetected: false,
                curlCapability.Status,
                responseVersion: null,
                statusCode: null,
                contentType: null,
                errors,
                curlCapability.Warnings ?? []);
            await WriteManagedProofArtifactsAsync(paths, proof, [], stderr);

            return new ScenarioValidationResult
            {
                ScenarioId = cell.Scenario.Id,
                TargetId = cell.Implementation.Id,
                AdapterId = "",
                Protocol = cell.Protocol,
                Status = status,
                Summary = status == ValidationStatus.Unsupported
                    ? "HTTP/3 validation requires curl --http3-only or managed HttpClient H3 proof."
                    : "HTTP/3 endpoint validation or managed protocol proof failed.",
                Errors = errors,
                Warnings = proof.Warnings,
                ProtocolProof = proof
            };
        }
    }

    private static async Task<ManagedProofResponse> SendManagedHttp3RequestAsync(
        RunCell cell,
        Uri uri,
        bool certificateBypassUsed)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(10),
            SslOptions = new SslClientAuthenticationOptions()
        };

        if (certificateBypassUsed)
        {
            handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
        }

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        using var request = new HttpRequestMessage(new HttpMethod(cell.Scenario.Endpoint!.Method), uri)
        {
            Version = HttpVersion.Version30,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        var requestBody = HttpScenarioValidator.CreateRequestBody(cell.Scenario.Endpoint.RequestBodyGeneration);

        if (requestBody.Length > 0)
        {
            request.Content = new ByteArrayContent(requestBody);
        }

        foreach (var header in cell.Scenario.Endpoint.RequestHeaders)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        using var response = await client.SendAsync(request);
        var body = await HttpScenarioValidator.ReadResponseBodyAsync(response.Content);
        var warnings = certificateBypassUsed
            ? new[] { "Loopback certificate validation bypass was used for managed HTTP/3 proof." }
            : [];

        return new ManagedProofResponse(
            response.Version,
            response.StatusCode,
            response.Content.Headers.ContentType?.MediaType,
            body,
            warnings);
    }

    private static async Task EnsureProofArtifactsAsync(ArtifactPaths paths)
    {
        await File.WriteAllTextAsync(paths.ProtocolProofJson, "");
        await File.WriteAllTextAsync(paths.ProtocolProofStdout, "");
        await File.WriteAllTextAsync(paths.ProtocolProofStderr, "");
    }

    private static async Task WriteManagedProofArtifactsAsync(
        ArtifactPaths paths,
        ProtocolProofResult proof,
        byte[] body,
        IReadOnlyList<string> stderr)
    {
        var stdout = new StringBuilder();
        stdout.AppendLine($"method: {proof.Method}");
        stdout.AppendLine($"client: {proof.ProofClient}");
        stdout.AppendLine($"requestUrl: {proof.RequestUrl}");
        stdout.AppendLine($"requestedVersion: {proof.RequestedVersion}");
        stdout.AppendLine($"versionPolicy: {proof.VersionPolicy}");
        stdout.AppendLine($"responseVersion: {proof.ResponseVersion ?? "n/a"}");
        stdout.AppendLine($"statusCode: {proof.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}");
        stdout.AppendLine($"contentType: {proof.ContentType ?? "n/a"}");
        stdout.AppendLine($"bodyLength: {body.Length.ToString(CultureInfo.InvariantCulture)}");

        await File.WriteAllTextAsync(paths.ProtocolProofStdout, stdout.ToString());
        await File.WriteAllTextAsync(paths.ProtocolProofStderr, string.Join(Environment.NewLine, stderr));
        await File.WriteAllTextAsync(paths.ProtocolProofJson, ResultJson.Serialize(proof));
    }

    private static bool ShouldBypassCertificateValidation(Uri uri, string certificateMode)
    {
        if (!IsLoopback(uri))
        {
            return false;
        }

        return certificateMode.Contains("development", StringComparison.OrdinalIgnoreCase) ||
            certificateMode.Contains("local", StringComparison.OrdinalIgnoreCase) ||
            certificateMode.Contains("loopback", StringComparison.OrdinalIgnoreCase) ||
            certificateMode.Contains("self-signed", StringComparison.OrdinalIgnoreCase) ||
            certificateMode.Contains("bypass", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLoopback(Uri uri)
    {
        return IPAddress.TryParse(uri.Host, out var address)
            ? IPAddress.IsLoopback(address)
            : string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeUnsupportedH3(Exception ex)
    {
        var message = ex.ToString();
        return message.Contains("HTTP/3", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("QUIC", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("MsQuic", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("not supported", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHttp3Version(string httpVersion)
    {
        return httpVersion.StartsWith("3", StringComparison.OrdinalIgnoreCase) ||
            httpVersion.Contains("HTTP/3", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FormatVersion(Version? version)
    {
        return version is null
            ? null
            : $"{version.Major.ToString(CultureInfo.InvariantCulture)}.{version.Minor.ToString(CultureInfo.InvariantCulture)}";
    }

    private static ProtocolProofResult BuildProof(
        ValidationStatus status,
        string requestedProtocol,
        string? provenProtocol,
        string method,
        string proofClient,
        Uri uri,
        string? commandLine,
        ArtifactPaths paths,
        string certificateMode,
        string httpsBaseUrl,
        bool fallbackDetected,
        string loadToolH3CapabilityStatus,
        string? responseVersion,
        int? statusCode,
        string? contentType,
        IReadOnlyList<string> errors,
        IReadOnlyList<string> warnings)
    {
        return new ProtocolProofResult
        {
            Status = status,
            RequestedProtocol = requestedProtocol,
            ProvenProtocol = provenProtocol,
            Method = method,
            ProofClient = proofClient,
            RequestUrl = uri.ToString(),
            RequestedVersion = "3.0",
            VersionPolicy = "RequestVersionExact",
            ResponseVersion = responseVersion,
            StatusCode = statusCode,
            ContentType = contentType,
            CommandLine = commandLine,
            JsonPath = paths.ProtocolProofJson,
            StdoutPath = paths.ProtocolProofStdout,
            StderrPath = paths.ProtocolProofStderr,
            CertificateMode = certificateMode,
            HttpsBaseUrl = httpsBaseUrl,
            FallbackDetected = fallbackDetected,
            LoadToolH3CapabilityStatus = loadToolH3CapabilityStatus,
            ArtifactPaths = new Dictionary<string, string>
            {
                ["json"] = paths.ProtocolProofJson,
                ["stdout"] = paths.ProtocolProofStdout,
                ["stderr"] = paths.ProtocolProofStderr
            },
            Errors = errors,
            Warnings = warnings
        };
    }

    private static string? FindMarker(string text, string marker)
    {
        var line = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(candidate => candidate.StartsWith(marker, StringComparison.Ordinal));
        return line is null ? null : line[marker.Length..].Trim();
    }

    private static string FormatCommandLine(string executable, IReadOnlyList<string> arguments)
    {
        return string.Join(" ", new[] { executable }.Concat(arguments).Select(QuoteArgument));
    }

    private static string QuoteArgument(string argument)
    {
        return argument.Contains(' ', StringComparison.Ordinal) || argument.Contains('"', StringComparison.Ordinal)
            ? "\"" + argument.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
            : argument;
    }

    private static async Task<LoadToolRunBytes> RunProcessAsync(string executable, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{executable}'.");
        await using var stdout = new MemoryStream();
        await using var stderr = new MemoryStream();
        var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(stdout);
        var stderrTask = process.StandardError.BaseStream.CopyToAsync(stderr);
        await process.WaitForExitAsync();
        await Task.WhenAll(stdoutTask, stderrTask);
        return new LoadToolRunBytes(process.ExitCode, stdout.ToArray(), stderr.ToArray());
    }

    private sealed record LoadToolRunBytes(int ExitCode, byte[] Stdout, byte[] Stderr);
}
