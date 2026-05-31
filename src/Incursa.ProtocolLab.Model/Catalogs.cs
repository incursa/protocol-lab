// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public static class ManifestCatalog
{
    public static IReadOnlyList<ImplementationManifest> Load(string root)
    {
        return YamlFile.LoadAll<ImplementationManifest>(root);
    }
}

public static class ScenarioCatalog
{
    public static IReadOnlyList<ScenarioDefinition> Load(string root)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(root, "*.yaml", SearchOption.AllDirectories)
            .Where(static path => !path.Contains(Path.Combine("network", "profiles"), StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Select(YamlFile.Load<ScenarioDefinition>)
            .ToArray();
    }
}

public static class NetworkProfileCatalog
{
    public static IReadOnlyList<NetworkProfileDefinition> Load(string root)
    {
        return YamlFile.LoadAll<NetworkProfileDefinition>(root);
    }
}

public static class LoadToolCatalog
{
    public static IReadOnlyList<LoadToolManifest> Load(string root)
    {
        return YamlFile.LoadAll<LoadToolManifest>(root);
    }
}

public static class LoadProfileCatalog
{
    public static IReadOnlyList<LoadProfileDefinition> Load(string root)
    {
        return YamlFile.LoadAll<LoadProfileDefinition>(root);
    }
}

public static class SuiteCatalog
{
    public static IReadOnlyList<SuiteDefinition> Load(string root)
    {
        return YamlFile.LoadAll<SuiteDefinition>(root);
    }
}
