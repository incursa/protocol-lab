// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public static class BenchmarkEvidenceClasses
{
    public const string LocalSmoke = "local-smoke";
    public const string LocalLab = "local-lab";
    public const string ExternalReferenceLocal = "external-reference-local";
    public const string IsolatedHost = "isolated-host";
    public const string Publishable = "publishable";
}

public static class BenchmarkComparabilityStatuses
{
    public const string ComparableLocal = "comparable-local";
    public const string ComparableWithWarnings = "comparable-with-warnings";
    public const string NotComparable = "not-comparable";
    public const string Invalid = "invalid";
}

public static class BenchmarkEvidenceReasons
{
    public const string LocalhostSharedHost = "localhost/shared-host";
    public const string DockerLoadToolHostProcessTarget = "docker-load-tool-host-process-target";
    public const string DockerTargetLocal = "docker-target-local";
    public const string HostPublishedPort = "host-published-port";
    public const string SharedDockerNetwork = "shared-docker-network";
    public const string DockerNetworkLocal = "docker-network-local";
    public const string DockerNetworkGenerated = "docker-network-generated";
    public const string DockerNetworkCleanupFailed = "docker-network-cleanup-failed";
    public const string TargetHostPortStillPublishedForValidation = "target-host-port-still-published-for-validation";
    public const string CertificateSniConnectToRouting = "certificate-sni-connect-to-routing";
    public const string DockerTargetImageLocalTag = "docker-target-image-local-tag";
    public const string TargetContainerResourceLimitsMissing = "target-container-resource-limits-missing";
    public const string LoadToolContainerResourceLimitsMissing = "load-tool-container-resource-limits-missing";
    public const string TargetContainerCpuNotIsolated = "target-container-cpu-not-isolated";
    public const string TargetContainerMemoryLimitMissing = "target-container-memory-limit-missing";
    public const string TargetContainerResourceLimitsApplied = "target-container-resource-limits-applied";
    public const string LoadToolContainerResourceLimitsApplied = "load-tool-container-resource-limits-applied";
    public const string DockerResourceLimitsLocalOnly = "docker-resource-limits-local-only";
    public const string DockerContainerCpuNotIsolated = "docker-container-cpu-not-isolated";
    public const string DockerContainerMemoryLimitMissing = "docker-container-memory-limit-missing";
    public const string DockerNetworkSharedHost = "docker-network-shared-host";
    public const string CertificateModeLocalDev = "certificate-mode-local-dev";
    public const string HostDockerInternalRewrite = "host-docker-internal-rewrite";
    public const string NoCpuIsolation = "no-cpu-isolation";
    public const string NoTargetResourceMetrics = "no-target-resource-metrics";
    public const string NoLoadGeneratorSaturationCheck = "no-load-generator-saturation-check";
    public const string NoNetworkIsolation = "no-network-isolation";
    public const string SingleMachine = "single-machine";
    public const string NoRepeatedStableMedian = "no-repeated-stable-median";
    public const string NoQlogProtocolCounterReview = "no-qlog/protocol-counter-review";
    public const string SelfSignedLoopbackCertificateMode = "self-signed/loopback-certificate-mode";
    public const string ExternalReferenceLoadToolProven = "external-reference-load-tool-proven";
    public const string ManagedLabLoadTool = "managed-lab-load-tool";
    public const string AdapterBackedTarget = "adapter-backed-target";
    public const string ValidationFailure = "validation-failure";
    public const string BenchmarkExecutionFailure = "benchmark-execution-failure";
    public const string ProtocolProofMissing = "protocol-proof-missing";
    public const string ParsedMetricsMissing = "parsed-metrics-missing";
    public const string UnstableResult = "unstable-result";
    public const string DifferentRequestedEffectiveLoadShape = "different-requested/effective-load-shape";
    public const string ProtocolProofMethodMixed = "protocol-proof-method-mixed";
    public const string LoadGeneratorCpuNotCaptured = "load-generator-cpu-not-captured";
    public const string LoadGeneratorMetricsCaptured = "load-generator-metrics-captured";
    public const string LoadGeneratorMetricsMissing = "load-generator-metrics-missing";
    public const string LoadGeneratorMetricsPartial = "load-generator-metrics-partial";
    public const string LoadGeneratorSingleSample = "load-generator-single-sample";
    public const string LoadGeneratorCpuHigh = "load-generator-cpu-high";
    public const string LoadGeneratorCpuUnknown = "load-generator-cpu-unknown";
    public const string LoadGeneratorMemoryHigh = "load-generator-memory-high";
    public const string LoadGeneratorNetworkHigh = "load-generator-network-high";
    public const string LoadGeneratorSaturationPossible = "load-generator-saturation-possible";
    public const string LoadGeneratorSaturationNotDetected = "load-generator-saturation-not-detected";
    public const string LoadGeneratorSaturationUnknown = "load-generator-saturation-unknown";
    public const string LoadToolNotDocker = "load-tool-not-docker";
    public const string DockerStatsUnavailable = "docker-stats-unavailable";
    public const string ContainerExitedTooQuickly = "container-exited-too-quickly";
    public const string MetricsParserFailed = "metrics-parser-failed";
    public const string TargetContainerMetricsCaptured = "target-container-metrics-captured";
    public const string TargetContainerMetricsMissing = "target-container-metrics-missing";
    public const string TargetContainerMetricsPartial = "target-container-metrics-partial";
    public const string TargetContainerSingleSample = "target-container-single-sample";
    public const string TargetContainerCpuHigh = "target-container-cpu-high";
    public const string TargetContainerCpuUnknown = "target-container-cpu-unknown";
    public const string TargetContainerCpuNotCaptured = "target-container-cpu-not-captured";
    public const string TargetContainerMemoryHigh = "target-container-memory-high";
    public const string TargetContainerNetworkHigh = "target-container-network-high";
    public const string TargetContainerSaturationPossible = "target-container-saturation-possible";
    public const string TargetContainerSaturationNotDetected = "target-container-saturation-not-detected";
    public const string TargetContainerSaturationUnknown = "target-container-saturation-unknown";
}

public sealed record BenchmarkEvidenceAssessment
{
    public string EvidenceClass { get; init; } = BenchmarkEvidenceClasses.LocalSmoke;
    public IReadOnlyList<string> EvidenceReasons { get; init; } = [];
    public string ComparabilityStatus { get; init; } = BenchmarkComparabilityStatuses.NotComparable;
    public IReadOnlyList<string> ComparabilityWarnings { get; init; } = [];
}

public sealed record ProcessMetricSnapshot
{
    public DateTimeOffset TimestampUtc { get; init; }
    public long? WorkingSetBytes { get; init; }
    public long? PrivateMemoryBytes { get; init; }
    public double? CpuTimeSeconds { get; init; }
    public int? ThreadCount { get; init; }
    public int? HandleCount { get; init; }
}

public sealed record ProcessMetricSample
{
    public DateTimeOffset TimestampUtc { get; init; }
    public long? WorkingSetBytes { get; init; }
    public double? CpuTimeDeltaSeconds { get; init; }
    public int? ThreadCount { get; init; }
    public int? HandleCount { get; init; }
}

public sealed record TargetProcessMetrics
{
    public int? ProcessId { get; init; }
    public DateTimeOffset? StartTimeUtc { get; init; }
    public DateTimeOffset? ReadyTimeUtc { get; init; }
    public DateTimeOffset? EndTimeUtc { get; init; }
    public int? ExitCode { get; init; }
    public bool Crashed { get; init; }
    public ProcessMetricSnapshot? Before { get; init; }
    public ProcessMetricSnapshot? After { get; init; }
    public IReadOnlyList<ProcessMetricSample> Samples { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
