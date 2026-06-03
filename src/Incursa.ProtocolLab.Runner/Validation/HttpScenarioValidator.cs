// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

internal static class HttpScenarioValidator
{
    public static async Task<ScenarioValidationResult> ValidateAsync(
        RunCell cell,
        string? baseUrl,
        ArtifactPaths? paths = null,
        string certificateMode = "")
    {
        var support = ScenarioSupport.Evaluate(cell.Implementation, cell.Scenario, cell.Protocol);
        if (!support.IsSupported)
        {
            return CreateResult(cell, ValidationStatus.Unsupported, support.Reason);
        }

        if (cell.Scenario.H3Protocol is not null)
        {
            return CreateResult(cell, ValidationStatus.Unsupported,
                "H3 protocol validation is modeled, but no H3 protocol validator or load generator is implemented yet.");
        }

        if (cell.Scenario.QuicTransport is not null)
        {
            return await QuicTransportValidator.ValidateAsync(cell, baseUrl, paths, certificateMode);
        }

        if (cell.Scenario.WebTransport is not null)
        {
            return CreateResult(cell, ValidationStatus.Unsupported,
                "WebTransport validation is modeled as a future workload family, but no validator or load generator is implemented yet.");
        }

        if (cell.Scenario.Masque is not null)
        {
            return CreateResult(cell, ValidationStatus.Unsupported,
                "MASQUE validation is modeled as a future workload family, but no validator or load generator is implemented yet.");
        }

        if (cell.Scenario.Endpoint is null)
        {
            return CreateResult(cell, ValidationStatus.Unsupported,
                "Phase 1 validates HTTP endpoint scenarios only.");
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return CreateResult(cell, ValidationStatus.NotApplicable,
                "No --base-url supplied; endpoint behavior validation was not run.",
                warnings: ["Definition compatibility was checked only."]);
        }

        if (ProtocolIds.IsHttp3(cell.Protocol))
        {
            if (paths is null)
            {
                var proof = new ProtocolProofResult
                {
                    Status = ValidationStatus.Unsupported,
                    RequestedProtocol = cell.Protocol,
                    Method = "curl --http3-only",
                    Errors = ["HTTP/3 validation requires protocol proof artifact paths."]
                };
                return CreateResult(cell, ValidationStatus.Unsupported,
                    "HTTP/3 validation requires protocol proof artifact paths.",
                    errors: proof.Errors,
                    protocolProof: proof);
            }

            return await ProtocolProofValidator.ValidateH3Async(cell, baseUrl, paths, certificateMode);
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            using var request = new HttpRequestMessage(new HttpMethod(cell.Scenario.Endpoint.Method), BuildScenarioUri(baseUrl, cell.Scenario.Endpoint));
            var requestBody = CreateRequestBody(cell.Scenario.Endpoint.RequestBodyGeneration);

            if (requestBody.Length > 0)
            {
                request.Content = new ByteArrayContent(requestBody);
            }

            foreach (var header in cell.Scenario.Endpoint.RequestHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            using var response = await client.SendAsync(request);
            var body = await ReadResponseBodyAsync(response.Content);
            var errors = ValidateResponse(cell.Scenario.Endpoint, response.StatusCode, response.Headers, response.Content.Headers, body, requestBody);

            return errors.Count == 0
                ? CreateResult(cell, ValidationStatus.Passed, "Endpoint behavior matched scenario.")
                : CreateResult(cell, ValidationStatus.Failed, "Endpoint behavior did not match scenario.", errors: errors);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException or IOException)
        {
            return CreateResult(cell, ValidationStatus.Failed, ex.Message, errors: [ex.Message]);
        }
    }

    internal static async Task<byte[]> ReadResponseBodyAsync(HttpContent content, CancellationToken cancellationToken = default)
    {
        using var stream = await content.ReadAsStreamAsync();
        using var buffer = new MemoryStream();
        var rented = ArrayPool<byte>.Shared.Rent(81920);

        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(rented.AsMemory(), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                buffer.Write(rented, 0, read);
            }

            return buffer.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static ScenarioValidationResult CreateResult(
        RunCell cell,
        ValidationStatus status,
        string summary,
        IReadOnlyList<string>? errors = null,
        IReadOnlyList<string>? warnings = null,
        ProtocolProofResult? protocolProof = null)
    {
        return new ScenarioValidationResult
        {
            ScenarioId = cell.Scenario.Id,
            TargetId = cell.Implementation.Id,
            AdapterId = "",
            Protocol = cell.Protocol,
            Status = status,
            Summary = summary,
            Errors = errors ?? [],
            Warnings = warnings ?? [],
            ProtocolProof = protocolProof
        };
    }

    public static Uri BuildScenarioUri(string baseUrl, HttpEndpointSpec endpoint)
    {
        var builder = new UriBuilder(new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), endpoint.Path.TrimStart('/')));
        if (endpoint.Query.Count > 0)
        {
            builder.Query = string.Join("&", endpoint.Query.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        }

        return builder.Uri;
    }

    internal static byte[] CreateRequestBody(string? requestBodyGeneration)
    {
        if (string.IsNullOrWhiteSpace(requestBodyGeneration))
        {
            return [];
        }

        const string deterministicBytesPrefix = "deterministic-bytes:";
        if (requestBodyGeneration.StartsWith(deterministicBytesPrefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(requestBodyGeneration[deterministicBytesPrefix.Length..], out var size))
        {
            return CreateDeterministicBytes(size);
        }

        return Encoding.UTF8.GetBytes(requestBodyGeneration);
    }

    internal static byte[] CreateDeterministicBytes(int size)
    {
        var bytes = new byte[size];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)(index % 251);
        }

        return bytes;
    }

    internal static List<string> ValidateResponse(
        HttpEndpointSpec endpoint,
        HttpStatusCode statusCode,
        HttpResponseHeaders responseHeaders,
        HttpContentHeaders contentHeaders,
        byte[] body,
        byte[] requestBody)
    {
        var errors = new List<string>();
        var bodyText = Encoding.UTF8.GetString(body);

        if ((int)statusCode != endpoint.ExpectedStatus)
        {
            errors.Add($"Expected status {endpoint.ExpectedStatus}, got {(int)statusCode}.");
        }

        foreach (var expectedHeader in endpoint.ExpectedHeaders)
        {
            var actual = expectedHeader.Key.Equals("content-type", StringComparison.OrdinalIgnoreCase)
                ? contentHeaders.ContentType?.MediaType
                : responseHeaders.TryGetValues(expectedHeader.Key, out var values) ? string.Join(",", values) : null;

            if (actual is null || !actual.Contains(expectedHeader.Value, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Expected header '{expectedHeader.Key}' to contain '{expectedHeader.Value}', got '{actual ?? "<missing>"}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(endpoint.ExpectedHeaderPrefix) &&
            endpoint.ExpectedHeaderCount is not null &&
            endpoint.ExpectedHeaderValueSize is not null)
        {
            for (var index = 0; index < endpoint.ExpectedHeaderCount.Value; index++)
            {
                var name = $"{endpoint.ExpectedHeaderPrefix}{index:000}";
                if (!responseHeaders.TryGetValues(name, out var values))
                {
                    errors.Add($"Expected response header '{name}' was missing.");
                    continue;
                }

                var value = string.Join(",", values);
                if (value.Length != endpoint.ExpectedHeaderValueSize.Value)
                {
                    errors.Add($"Expected response header '{name}' value size {endpoint.ExpectedHeaderValueSize}, got {value.Length}.");
                }
            }
        }

        if (endpoint.ExpectedBodySize is not null && body.Length != endpoint.ExpectedBodySize)
        {
            errors.Add($"Expected body size {endpoint.ExpectedBodySize}, got {body.Length}.");
        }

        if (endpoint.ExpectedBodyRule.Equals("exact", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(endpoint.ExpectedBody, bodyText, StringComparison.Ordinal))
        {
            errors.Add("Response body did not match the exact expected body.");
        }

        if (endpoint.ExpectedBodyRule.Equals("jsonEquivalent", StringComparison.OrdinalIgnoreCase) &&
            !JsonEquivalent(endpoint.ExpectedBody, bodyText))
        {
            errors.Add("Response body was not JSON-equivalent to the expected body.");
        }

        if (endpoint.ExpectedBodyRule.Equals("deterministicBytes", StringComparison.OrdinalIgnoreCase) &&
            !body.SequenceEqual(CreateDeterministicBytes(body.Length)))
        {
            errors.Add("Response body did not match the deterministic byte pattern.");
        }

        if (endpoint.ExpectedBodyRule.Equals("requestBodyEcho", StringComparison.OrdinalIgnoreCase) &&
            !body.SequenceEqual(requestBody))
        {
            errors.Add("Response body did not echo the request body.");
        }

        if (endpoint.ExpectedBodyRule.Equals("byteCount", StringComparison.OrdinalIgnoreCase) &&
            endpoint.ExpectedBodySize is null)
        {
            errors.Add("Expected body rule 'byteCount' requires expectedBodySize.");
        }

        if (endpoint.ExpectedBodyRule.Equals("jsonBytesRead", StringComparison.OrdinalIgnoreCase))
        {
            ValidateJsonProperty(bodyText, "bytesRead", requestBody.Length.ToString(), errors);
        }

        if (endpoint.ExpectedBodyRule.Equals("jsonHashResult", StringComparison.OrdinalIgnoreCase))
        {
            ValidateJsonProperty(bodyText, "bytesRead", requestBody.Length.ToString(), errors);
            ValidateJsonProperty(bodyText, "sha256", Convert.ToHexString(SHA256.HashData(requestBody)).ToLowerInvariant(), errors);
        }

        if (endpoint.ExpectedJsonProperties.Count > 0 || endpoint.ExpectedJsonKeys.Count > 0)
        {
            ValidateJsonProperties(bodyText, endpoint.ExpectedJsonProperties, endpoint.ExpectedJsonKeys, errors);
        }

        return errors;
    }

    internal static List<string> ValidateResponse(
        HttpEndpointSpec endpoint,
        HttpStatusCode statusCode,
        string? contentType,
        byte[] body,
        byte[] requestBody)
    {
        var errors = new List<string>();
        var bodyText = Encoding.UTF8.GetString(body);

        if ((int)statusCode != endpoint.ExpectedStatus)
        {
            errors.Add($"Expected status {endpoint.ExpectedStatus}, got {(int)statusCode}.");
        }

        foreach (var expectedHeader in endpoint.ExpectedHeaders)
        {
            if (!expectedHeader.Key.Equals("content-type", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Expected header '{expectedHeader.Key}' cannot be validated by curl protocol proof.");
                continue;
            }

            if (contentType is null || !contentType.Contains(expectedHeader.Value, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Expected header '{expectedHeader.Key}' to contain '{expectedHeader.Value}', got '{contentType ?? "<missing>"}'.");
            }
        }

        if (endpoint.ExpectedBodySize is not null && body.Length != endpoint.ExpectedBodySize)
        {
            errors.Add($"Expected body size {endpoint.ExpectedBodySize}, got {body.Length}.");
        }

        if (endpoint.ExpectedBodyRule.Equals("exact", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(endpoint.ExpectedBody, bodyText, StringComparison.Ordinal))
        {
            errors.Add("Response body did not match the exact expected body.");
        }

        if (endpoint.ExpectedBodyRule.Equals("jsonEquivalent", StringComparison.OrdinalIgnoreCase) &&
            !JsonEquivalent(endpoint.ExpectedBody, bodyText))
        {
            errors.Add("Response body was not JSON-equivalent to the expected body.");
        }

        return errors;
    }

    private static bool JsonEquivalent(string? expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return string.IsNullOrWhiteSpace(actual);
        }

        using var expectedJson = JsonDocument.Parse(expected);
        using var actualJson = JsonDocument.Parse(actual);
        return JsonElement.DeepEquals(expectedJson.RootElement, actualJson.RootElement);
    }

    private static void ValidateJsonProperty(string json, string name, string expectedValue, List<string> errors)
    {
        ValidateJsonProperties(json, new Dictionary<string, string> { [name] = expectedValue }, [], errors);
    }

    private static void ValidateJsonProperties(
        string json,
        IReadOnlyDictionary<string, string> expectedProperties,
        IReadOnlyList<string> expectedKeys,
        List<string> errors)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            foreach (var expectedKey in expectedKeys)
            {
                if (!document.RootElement.TryGetProperty(expectedKey, out _))
                {
                    errors.Add($"Expected JSON property '{expectedKey}' was missing.");
                }
            }

            foreach (var expectedProperty in expectedProperties)
            {
                if (!document.RootElement.TryGetProperty(expectedProperty.Key, out var value))
                {
                    errors.Add($"Expected JSON property '{expectedProperty.Key}' was missing.");
                    continue;
                }

                var actual = value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : value.GetRawText();

                if (!string.Equals(expectedProperty.Value, actual, StringComparison.Ordinal))
                {
                    errors.Add($"Expected JSON property '{expectedProperty.Key}' to be '{expectedProperty.Value}', got '{actual}'.");
                }
            }
        }
        catch (JsonException ex)
        {
            errors.Add($"Response body was not valid JSON: {ex.Message}");
        }
    }
}
