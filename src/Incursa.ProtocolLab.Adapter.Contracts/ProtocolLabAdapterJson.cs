// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Incursa.ProtocolLab.Adapter.Contracts;

public static class ProtocolLabAdapterJson
{
    private static readonly JsonSerializerOptions NonSourceGenOptions = new(JsonSerializerDefaults.Web);

    public static readonly JsonSerializerOptions Options = CreateOptions();

    public static JsonElement SerializeValue<T>(T value)
    {
        return JsonSerializer.SerializeToElement(value, NonSourceGenOptions);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = ProtocolLabAdapterJsonContext.Default
        };

        options.Converters.Add(new SnakeCaseEnumJsonConverterFactory());
        return options;
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AdapterHealthResponse))]
[JsonSerializable(typeof(AdapterManifestResponse))]
[JsonSerializable(typeof(AdapterSessionCreateRequest))]
[JsonSerializable(typeof(AdapterSessionResource))]
[JsonSerializable(typeof(AdapterPrepareRequest))]
[JsonSerializable(typeof(AdapterOperationResult))]
[JsonSerializable(typeof(AdapterStatusResponse))]
[JsonSerializable(typeof(AdapterEndpointsResponse))]
[JsonSerializable(typeof(AdapterMetricsResponse))]
[JsonSerializable(typeof(AdapterArtifactsResponse))]
[JsonSerializable(typeof(AdapterProblemDetails))]
[JsonSerializable(typeof(AdapterIdentity))]
[JsonSerializable(typeof(AdapterVersionCompatibility))]
[JsonSerializable(typeof(AdapterCapability))]
[JsonSerializable(typeof(AdapterScenarioSelector))]
[JsonSerializable(typeof(AdapterEndpointType))]
[JsonSerializable(typeof(AdapterArtifactType))]
[JsonSerializable(typeof(AdapterMetricsAvailability))]
[JsonSerializable(typeof(AdapterSessionSummary))]
[JsonSerializable(typeof(AdapterReadinessSnapshot))]
[JsonSerializable(typeof(AdapterHealthSnapshot))]
[JsonSerializable(typeof(AdapterEndpointBinding))]
[JsonSerializable(typeof(AdapterArtifactExpectation))]
[JsonSerializable(typeof(AdapterTlsNotes))]
[JsonSerializable(typeof(AdapterEndpoint))]
[JsonSerializable(typeof(AdapterMetric))]
[JsonSerializable(typeof(AdapterArtifact))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
public partial class ProtocolLabAdapterJsonContext : JsonSerializerContext;

public sealed class SnakeCaseEnumJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsEnum;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(SnakeCaseEnumJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

internal sealed class SnakeCaseEnumJsonConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException($"Unable to read {typeof(TEnum).Name} from an empty string.");
        }

        var normalized = Normalize(value);
        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (Normalize(name) == normalized)
            {
                return Enum.Parse<TEnum>(name);
            }
        }

        throw new JsonException($"Unable to read {typeof(TEnum).Name} value '{value}'.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(ToSnakeCase(value.ToString()));
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is '_' or '-' or ' ')
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character))
            {
                if (index > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
