// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Text.Json;

namespace Incursa.ProtocolLab.Tests;

public sealed class IncursaQuicSourceOverrideTests
{
    private static readonly string[] IncursaPackageIds =
    [
        "Incursa.Qpack",
        "Incursa.Quic",
        "Incursa.Quic.Http3"
    ];

    [Fact]
    public async Task Normal_package_mode_keeps_incursa_quic_package_references()
    {
        var items = await EvaluateItemsAsync(ProjectPath("src", "Incursa.ProtocolLab.Adapters.IncursaHttp3", "Incursa.ProtocolLab.Adapters.IncursaHttp3.csproj"));

        Assert.DoesNotContain(items.ProjectReferences, static reference => reference.Contains("Incursa.Qpack.csproj", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(items.ProjectReferences, static reference => reference.Contains("Incursa.Quic.csproj", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(items.ProjectReferences, static reference => reference.Contains("Incursa.Quic.Http3.csproj", StringComparison.OrdinalIgnoreCase));
        Assert.All(IncursaPackageIds, packageId => Assert.Contains(packageId, items.PackageReferences));
    }

    [Fact]
    public async Task Source_override_replaces_incursa_quic_packages_with_source_project_references()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"incursa-quic-source-{Guid.NewGuid():N}");
        var expectedReferences = new[]
        {
            Path.Combine(sourceRoot, "src", "Incursa.Qpack", "Incursa.Qpack.csproj"),
            Path.Combine(sourceRoot, "src", "Incursa.Quic", "Incursa.Quic.csproj"),
            Path.Combine(sourceRoot, "src", "Incursa.Quic.Http3", "Incursa.Quic.Http3.csproj")
        };

        var items = await EvaluateItemsAsync(
            ProjectPath("src", "Incursa.ProtocolLab.Adapters.IncursaHttp3", "Incursa.ProtocolLab.Adapters.IncursaHttp3.csproj"),
            $"-p:IncursaQuicSourceRoot={sourceRoot}");

        Assert.DoesNotContain(items.PackageReferences, package => IncursaPackageIds.Contains(package, StringComparer.OrdinalIgnoreCase));
        Assert.All(expectedReferences, expected => Assert.Contains(Path.GetFullPath(expected), items.ProjectReferences));
    }

    private static async Task<EvaluatedItems> EvaluateItemsAsync(string projectPath, params string[] additionalArguments)
    {
        using Process process = new();
        process.StartInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = TestPaths.RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        process.StartInfo.ArgumentList.Add("msbuild");
        process.StartInfo.ArgumentList.Add(projectPath);
        process.StartInfo.ArgumentList.Add("-getItem:PackageReference");
        process.StartInfo.ArgumentList.Add("-getItem:ProjectReference");

        foreach (var argument in additionalArguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, $"dotnet msbuild exited {process.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");

        using var document = JsonDocument.Parse(stdout);
        var items = document.RootElement.GetProperty("Items");
        return new EvaluatedItems(
            ReadIdentities(items.GetProperty("PackageReference")),
            ReadFullPaths(items.GetProperty("ProjectReference")));
    }

    private static string[] ReadIdentities(JsonElement itemArray)
    {
        return itemArray.EnumerateArray()
            .Select(static item => item.GetProperty("Identity").GetString())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
    }

    private static string[] ReadFullPaths(JsonElement itemArray)
    {
        return itemArray.EnumerateArray()
            .Select(static item => item.GetProperty("FullPath").GetString())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => Path.GetFullPath(value!))
            .ToArray();
    }

    private static string ProjectPath(params string[] relativePath)
    {
        return Path.Combine([TestPaths.RepoRoot, .. relativePath]);
    }

    private sealed record EvaluatedItems(string[] PackageReferences, string[] ProjectReferences);
}
