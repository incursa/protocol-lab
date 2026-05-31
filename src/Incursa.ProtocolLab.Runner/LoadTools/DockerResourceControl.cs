// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Globalization;
using System.Text.Json;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

internal static class DockerResourceControl
{
    public static DockerResourceLimits? Merge(DockerResourceLimits? manifest, DockerResourceLimits? overrides)
    {
        if (manifest is null)
        {
            return overrides?.HasAnyLimit == true ? overrides : null;
        }

        if (overrides is null || !overrides.HasAnyLimit)
        {
            return manifest.HasAnyLimit ? manifest : null;
        }

        var ulimits = new Dictionary<string, string>(manifest.Ulimits, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in overrides.Ulimits)
        {
            ulimits[pair.Key] = pair.Value;
        }

        var merged = manifest with
        {
            Cpus = overrides.Cpus ?? manifest.Cpus,
            CpuQuota = overrides.CpuQuota ?? manifest.CpuQuota,
            CpuPeriod = overrides.CpuPeriod ?? manifest.CpuPeriod,
            CpuShares = overrides.CpuShares ?? manifest.CpuShares,
            CpusetCpus = overrides.CpusetCpus ?? manifest.CpusetCpus,
            Memory = overrides.Memory ?? manifest.Memory,
            MemorySwap = overrides.MemorySwap ?? manifest.MemorySwap,
            MemoryReservation = overrides.MemoryReservation ?? manifest.MemoryReservation,
            PidsLimit = overrides.PidsLimit ?? manifest.PidsLimit,
            Ulimits = ulimits,
            Notes = overrides.Notes ?? manifest.Notes
        };

        return merged.HasAnyLimit ? merged : null;
    }

    public static void AddDockerRunArguments(List<string> arguments, DockerResourceLimits? limits)
    {
        if (limits?.HasAnyLimit != true)
        {
            return;
        }

        Add(arguments, "--cpus", limits.Cpus);
        Add(arguments, "--cpu-quota", limits.CpuQuota);
        Add(arguments, "--cpu-period", limits.CpuPeriod);
        Add(arguments, "--cpu-shares", limits.CpuShares);
        Add(arguments, "--cpuset-cpus", limits.CpusetCpus);
        Add(arguments, "--memory", limits.Memory);
        Add(arguments, "--memory-swap", limits.MemorySwap);
        Add(arguments, "--memory-reservation", limits.MemoryReservation);
        Add(arguments, "--pids-limit", limits.PidsLimit);

        foreach (var pair in limits.Ulimits.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            arguments.Add("--ulimit");
            arguments.Add($"{pair.Key}={pair.Value}");
        }
    }

    public static IReadOnlyList<string> BuildWarnings(DockerResourceLimits? requested, DockerResourceLimits? effective, bool targetContainer)
    {
        var warnings = new List<string>();
        var resourceMissing = targetContainer
            ? BenchmarkEvidenceReasons.TargetContainerResourceLimitsMissing
            : BenchmarkEvidenceReasons.LoadToolContainerResourceLimitsMissing;
        var memoryMissing = targetContainer
            ? BenchmarkEvidenceReasons.TargetContainerMemoryLimitMissing
            : BenchmarkEvidenceReasons.DockerContainerMemoryLimitMissing;
        var cpuNotIsolated = targetContainer
            ? BenchmarkEvidenceReasons.TargetContainerCpuNotIsolated
            : BenchmarkEvidenceReasons.DockerContainerCpuNotIsolated;
        var applied = targetContainer
            ? BenchmarkEvidenceReasons.TargetContainerResourceLimitsApplied
            : BenchmarkEvidenceReasons.LoadToolContainerResourceLimitsApplied;

        var appliedLimits = effective?.HasAnyLimit == true ? effective : requested;
        if (appliedLimits?.HasAnyLimit == true)
        {
            warnings.Add(applied);
            warnings.Add(BenchmarkEvidenceReasons.DockerResourceLimitsLocalOnly);
        }
        else
        {
            warnings.Add(resourceMissing);
        }

        if (appliedLimits?.HasMemoryLimit != true)
        {
            warnings.Add(memoryMissing);
        }

        if (appliedLimits?.HasCpuIsolation != true)
        {
            warnings.Add(cpuNotIsolated);
        }

        return warnings;
    }

    public static DockerResourceLimits? ParseEffectiveLimitsFromInspectFile(string path, ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            warnings.Add("docker-resource-limit-inspect-missing");
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                if (root.GetArrayLength() == 0)
                {
                    warnings.Add("docker-resource-limit-inspect-empty");
                    return null;
                }

                root = root[0];
            }

            if (!root.TryGetProperty("HostConfig", out var hostConfig))
            {
                warnings.Add("docker-resource-limit-host-config-missing");
                return null;
            }

            var nanoCpus = GetLong(hostConfig, "NanoCpus");
            var memory = GetLong(hostConfig, "Memory");
            var memorySwap = GetLong(hostConfig, "MemorySwap");
            var memoryReservation = GetLong(hostConfig, "MemoryReservation");
            var pidsLimit = GetLong(hostConfig, "PidsLimit");
            var limits = new DockerResourceLimits
            {
                Cpus = nanoCpus is > 0
                    ? (nanoCpus.Value / 1_000_000_000d).ToString("0.###", CultureInfo.InvariantCulture)
                    : null,
                CpuQuota = PositiveOrNull(GetLong(hostConfig, "CpuQuota")),
                CpuPeriod = PositiveOrNull(GetLong(hostConfig, "CpuPeriod")),
                CpuShares = PositiveOrNull(GetLong(hostConfig, "CpuShares")),
                CpusetCpus = GetString(hostConfig, "CpusetCpus"),
                Memory = memory is > 0 ? memory.Value.ToString(CultureInfo.InvariantCulture) : null,
                MemorySwap = memorySwap is > 0 ? memorySwap.Value.ToString(CultureInfo.InvariantCulture) : null,
                MemoryReservation = memoryReservation is > 0 ? memoryReservation.Value.ToString(CultureInfo.InvariantCulture) : null,
                PidsLimit = pidsLimit is > 0 ? pidsLimit : null,
                Ulimits = ParseUlimits(hostConfig)
            };

            return limits.HasAnyLimit ? limits : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            warnings.Add($"docker-resource-limit-inspect-parse-failed: {ex.Message}");
            return null;
        }
    }

    private static void Add(List<string> arguments, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        arguments.Add(name);
        arguments.Add(value);
    }

    private static void Add(List<string> arguments, string name, long? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        arguments.Add(name);
        arguments.Add(value.Value.ToString(CultureInfo.InvariantCulture));
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()
                : null;
    }

    private static long? GetLong(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? number
            : null;
    }

    private static long? PositiveOrNull(long? value)
    {
        return value is > 0 ? value : null;
    }

    private static Dictionary<string, string> ParseUlimits(JsonElement hostConfig)
    {
        var ulimits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!hostConfig.TryGetProperty("Ulimits", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return ulimits;
        }

        foreach (var item in values.EnumerateArray())
        {
            var name = GetString(item, "Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var soft = GetLong(item, "Soft");
            var hard = GetLong(item, "Hard");
            ulimits[name] = hard.HasValue
                ? $"{soft?.ToString(CultureInfo.InvariantCulture) ?? ""}:{hard.Value.ToString(CultureInfo.InvariantCulture)}"
                : soft?.ToString(CultureInfo.InvariantCulture) ?? "";
        }

        return ulimits;
    }
}
