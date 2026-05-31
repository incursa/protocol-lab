// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public sealed record DockerResourceLimits
{
    public string? Cpus { get; init; }
    public long? CpuQuota { get; init; }
    public long? CpuPeriod { get; init; }
    public long? CpuShares { get; init; }
    public string? CpusetCpus { get; init; }
    public string? Memory { get; init; }
    public string? MemorySwap { get; init; }
    public string? MemoryReservation { get; init; }
    public long? PidsLimit { get; init; }
    public Dictionary<string, string> Ulimits { get; init; } = [];
    public string? Notes { get; init; }

    public bool HasAnyLimit =>
        !string.IsNullOrWhiteSpace(Cpus) ||
        CpuQuota.HasValue ||
        CpuPeriod.HasValue ||
        CpuShares.HasValue ||
        !string.IsNullOrWhiteSpace(CpusetCpus) ||
        !string.IsNullOrWhiteSpace(Memory) ||
        !string.IsNullOrWhiteSpace(MemorySwap) ||
        !string.IsNullOrWhiteSpace(MemoryReservation) ||
        PidsLimit.HasValue ||
        Ulimits.Count > 0;

    public bool HasCpuLimit =>
        !string.IsNullOrWhiteSpace(Cpus) ||
        CpuQuota.HasValue ||
        CpuPeriod.HasValue ||
        CpuShares.HasValue ||
        !string.IsNullOrWhiteSpace(CpusetCpus);

    public bool HasCpuIsolation => !string.IsNullOrWhiteSpace(CpusetCpus);

    public bool HasMemoryLimit =>
        !string.IsNullOrWhiteSpace(Memory) ||
        !string.IsNullOrWhiteSpace(MemorySwap) ||
        !string.IsNullOrWhiteSpace(MemoryReservation);
}

public sealed record DockerCleanupSummary
{
    public bool TargetContainerCleanupAttempted { get; init; }
    public bool? TargetContainerCleanupSucceeded { get; init; }
    public string? TargetContainerName { get; init; }
    public bool TargetMetricsSamplerCleanupAttempted { get; init; }
    public bool? TargetMetricsSamplerCleanupSucceeded { get; init; }
    public bool LoadToolContainerCleanupAttempted { get; init; }
    public bool? LoadToolContainerCleanupSucceeded { get; init; }
    public string? LoadToolContainerName { get; init; }
    public bool LoadToolMetricsSamplerCleanupAttempted { get; init; }
    public bool? LoadToolMetricsSamplerCleanupSucceeded { get; init; }
    public bool NetworkCleanupAttempted { get; init; }
    public bool? NetworkCleanupSucceeded { get; init; }
    public string? NetworkName { get; init; }
    public IReadOnlyList<string> LeftoverContainers { get; init; } = [];
    public IReadOnlyList<string> LeftoverNetworks { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public static class LoadGeneratorSaturationStatuses
{
    public const string NotDetected = "load-generator-saturation-not-detected";
    public const string Possible = "load-generator-saturation-possible";
    public const string Unknown = "load-generator-saturation-unknown";
}

public static class TargetSaturationStatuses
{
    public const string NotDetected = "target-saturation-not-detected";
    public const string Possible = "target-saturation-possible";
    public const string Unknown = "target-saturation-unknown";
}

public sealed record DockerContainerMetricSample
{
    public DateTimeOffset TimestampUtc { get; init; }
    public string? ContainerId { get; init; }
    public string? ContainerName { get; init; }
    public double? CpuPercent { get; init; }
    public long? MemoryUsageBytes { get; init; }
    public long? MemoryLimitBytes { get; init; }
    public double? MemoryPercent { get; init; }
    public long? NetworkRxBytes { get; init; }
    public long? NetworkTxBytes { get; init; }
    public long? BlockReadBytes { get; init; }
    public long? BlockWriteBytes { get; init; }
    public int? PidsCurrent { get; init; }
}

public sealed record DockerContainerMetricsSummary
{
    public IReadOnlyList<DockerContainerMetricSample> Samples { get; init; } = [];
    public DateTimeOffset? CollectionStartUtc { get; init; }
    public DateTimeOffset? CollectionEndUtc { get; init; }
    public double? CpuMeanPercent { get; init; }
    public double? CpuMaxPercent { get; init; }
    public long? MemoryMeanBytes { get; init; }
    public long? MemoryMaxBytes { get; init; }
    public long? MemoryLimitBytes { get; init; }
    public double? MemoryMaxPercent { get; init; }
    public long? NetworkRxBytesDelta { get; init; }
    public long? NetworkTxBytesDelta { get; init; }
    public long? BlockReadBytesDelta { get; init; }
    public long? BlockWriteBytesDelta { get; init; }
    public int? PidsMax { get; init; }
    public IReadOnlyList<string> ParseWarnings { get; init; } = [];
}

public sealed record TargetDockerMetricsSummary
{
    public IReadOnlyList<DockerContainerMetricSample> Samples { get; init; } = [];
    public DateTimeOffset? CollectionStartUtc { get; init; }
    public DateTimeOffset? CollectionEndUtc { get; init; }
    public double? CpuMeanPercent { get; init; }
    public double? CpuMaxPercent { get; init; }
    public long? MemoryMeanBytes { get; init; }
    public long? MemoryMaxBytes { get; init; }
    public long? MemoryLimitBytes { get; init; }
    public double? MemoryMaxPercent { get; init; }
    public long? NetworkRxBytesDelta { get; init; }
    public long? NetworkTxBytesDelta { get; init; }
    public long? BlockReadBytesDelta { get; init; }
    public long? BlockWriteBytesDelta { get; init; }
    public int? PidsMax { get; init; }
    public IReadOnlyList<string> ParseWarnings { get; init; } = [];
}
