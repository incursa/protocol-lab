// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using NJsonSchema;

namespace Incursa.ProtocolLab.Adapter.Conformance;

internal static class ContractSchemaLoader
{
    public static async Task<JsonSchema> LoadAsync(string schemaPath, CancellationToken cancellationToken = default)
    {
        var schemaJson = await File.ReadAllTextAsync(schemaPath, cancellationToken).ConfigureAwait(false);
        schemaJson = await ExpandSchemaDefinitionsAsync(schemaPath, schemaJson, cancellationToken).ConfigureAwait(false);
        return await JsonSchema.FromJsonAsync(schemaJson, schemaPath).ConfigureAwait(false);
    }

    private static async Task<string> ExpandSchemaDefinitionsAsync(string schemaPath, string schemaJson, CancellationToken cancellationToken)
    {
        var schemaNode = JsonNode.Parse(schemaJson)!.AsObject();
        var definitions = schemaNode["definitions"] as JsonObject ?? new JsonObject();

        if (schemaNode["$defs"] is JsonObject localDefs)
        {
            foreach (var (key, value) in localDefs)
            {
                definitions[key] = value?.DeepClone();
            }
        }

        var commonSchemaPath = Path.Combine(Path.GetDirectoryName(schemaPath)!, "common.schema.json");
        if (File.Exists(commonSchemaPath) &&
            !Path.GetFileName(schemaPath).Equals("common.schema.json", StringComparison.OrdinalIgnoreCase))
        {
            var commonNode = JsonNode.Parse(await File.ReadAllTextAsync(commonSchemaPath, cancellationToken).ConfigureAwait(false))!.AsObject();
            if (commonNode["$defs"] is JsonObject commonDefs)
            {
                foreach (var (key, value) in commonDefs)
                {
                    definitions[key] ??= value?.DeepClone();
                }
            }
        }

        if (definitions.Count > 0)
        {
            schemaNode["definitions"] = definitions;
        }

        schemaNode.Remove("$defs");
        RewriteReferences(schemaNode);
        return schemaNode.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static void RewriteReferences(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToArray())
                {
                    if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        const string CommonDefsPrefix = "./common.schema.json#/$defs/";
                        const string LocalDefsPrefix = "#/$defs/";

                        if (text.StartsWith(CommonDefsPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            obj[property.Key] = "#/definitions/" + text[CommonDefsPrefix.Length..];
                            continue;
                        }

                        if (text.StartsWith(LocalDefsPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            obj[property.Key] = "#/definitions/" + text[LocalDefsPrefix.Length..];
                            continue;
                        }
                    }

                    if (property.Value is not null)
                    {
                        RewriteReferences(property.Value);
                    }
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is not null)
                    {
                        RewriteReferences(item);
                    }
                }

                break;
        }
    }
}
