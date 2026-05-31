// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Incursa.ProtocolLab.Model;

public static class YamlFile
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static T Load<T>(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var reader = File.OpenText(path);
        return Deserializer.Deserialize<T>(reader)
            ?? throw new InvalidDataException($"YAML file '{path}' did not deserialize to {typeof(T).Name}.");
    }

    public static IReadOnlyList<T> LoadAll<T>(string root, string searchPattern = "*.yaml")
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(root, searchPattern, SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Select(Load<T>)
            .ToArray();
    }
}
