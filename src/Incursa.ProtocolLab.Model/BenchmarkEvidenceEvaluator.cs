// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Globalization;

namespace Incursa.ProtocolLab.Model;

public static class BenchmarkEvidenceEvaluator
{
    public static BenchmarkEvidenceAssessment Assess(BenchmarkResult result)
    {
        var evidenceReasons = new List<string>();
        var comparabilityWarnings = new List<string>();

        var evidenceClass = DetermineEvidenceClass(result);
        AddEvidenceReasons(result, evidenceClass, evidenceReasons);
        AddComparabilityWarnings(result, comparabilityWarnings);

        var comparabilityStatus = DetermineComparabilityStatus(result, evidenceClass, comparabilityWarnings);

        return new BenchmarkEvidenceAssessment
        {
            EvidenceClass = evidenceClass,
            EvidenceReasons = evidenceReasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            ComparabilityStatus = comparabilityStatus,
            ComparabilityWarnings = comparabilityWarnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    public static BenchmarkEvidenceAssessment AssessAggregate(IReadOnlyList<BenchmarkResult> results)
    {
        if (results.Count == 0)
        {
            return new BenchmarkEvidenceAssessment();
        }

        var assessments = results
            .Select(result => result.Evidence ?? Assess(result))
            .ToArray();
        var firstEvidenceClass = assessments[0].EvidenceClass;
        var evidenceReasons = assessments
            .SelectMany(static assessment => assessment.EvidenceReasons)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var comparabilityWarnings = assessments
            .SelectMany(static assessment => assessment.ComparabilityWarnings)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var comparabilityStatus = assessments
            .Select(static assessment => assessment.ComparabilityStatus)
            .Aggregate(BenchmarkComparabilityStatuses.ComparableLocal, WorseStatus);

        if (results.Count == 1)
        {
            comparabilityWarnings.Add(BenchmarkEvidenceReasons.NoRepeatedStableMedian);
            comparabilityStatus = WorseStatus(comparabilityStatus, BenchmarkComparabilityStatuses.ComparableWithWarnings);
        }

        var proofMethods = results
            .Select(static result => result.ProtocolProof?.Method)
            .Where(static method => !string.IsNullOrWhiteSpace(method))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (proofMethods.Length > 1)
        {
            comparabilityWarnings.Add(BenchmarkEvidenceReasons.ProtocolProofMethodMixed);
            comparabilityStatus = WorseStatus(comparabilityStatus, BenchmarkComparabilityStatuses.ComparableWithWarnings);
        }

        var rpsSamples = results
            .Select(static result => result.Metrics.RequestsPerSecond)
            .Where(static value => value.HasValue)
            .Select(static value => value!.Value)
            .ToArray();
        if (rpsSamples.Length > 1)
        {
            var median = ComputeMedian(rpsSamples);
            if (median is not null && median > 0)
            {
                var best = rpsSamples.Max();
                var worst = rpsSamples.Min();
                var spread = (best - worst) / median.Value;
                if (spread >= 0.05d)
                {
                    comparabilityWarnings.Add(
                        $"{BenchmarkEvidenceReasons.UnstableResult}: requests/s varied by {spread.ToString("P1", CultureInfo.InvariantCulture)} across repetitions.");
                    comparabilityStatus = WorseStatus(comparabilityStatus, BenchmarkComparabilityStatuses.ComparableWithWarnings);
                }
            }
        }

        if (results.Any(static result =>
                result.ValidationResult.Status != ValidationStatus.Passed ||
                ProtocolIds.IsHttp3(result.Protocol) && result.ProtocolProof is null))
        {
            comparabilityStatus = BenchmarkComparabilityStatuses.Invalid;
            if (results.Any(static result => result.ValidationResult.Status != ValidationStatus.Passed))
            {
                comparabilityWarnings.Add(BenchmarkEvidenceReasons.ValidationFailure);
            }

            if (results.Any(static result => ProtocolIds.IsHttp3(result.Protocol) && result.ProtocolProof is null))
            {
                comparabilityWarnings.Add(BenchmarkEvidenceReasons.ProtocolProofMissing);
            }
        }
        else if (results.Any(static result => result.BenchmarkExecutionStatus != LoadToolExecutionStatuses.Succeeded))
        {
            comparabilityStatus = BenchmarkComparabilityStatuses.Invalid;
            comparabilityWarnings.Add(BenchmarkEvidenceReasons.BenchmarkExecutionFailure);
        }
        else if (results.Any(static result => result.ParsedMetricsAvailable == false))
        {
            comparabilityWarnings.Add(BenchmarkEvidenceReasons.ParsedMetricsMissing);
            comparabilityStatus = WorseStatus(comparabilityStatus, BenchmarkComparabilityStatuses.ComparableWithWarnings);
        }

        return new BenchmarkEvidenceAssessment
        {
            EvidenceClass = firstEvidenceClass,
            EvidenceReasons = evidenceReasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            ComparabilityStatus = comparabilityStatus,
            ComparabilityWarnings = comparabilityWarnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static string DetermineEvidenceClass(BenchmarkResult result)
    {
        if (result.BenchmarkExecutionStatus != LoadToolExecutionStatuses.Succeeded)
        {
            return BenchmarkEvidenceClasses.LocalSmoke;
        }

        if (string.Equals(result.LoadToolCategory, LoadToolCategories.ManagedLab, StringComparison.OrdinalIgnoreCase))
        {
            return BenchmarkEvidenceClasses.LocalLab;
        }

        if (string.Equals(result.LoadToolCategory, LoadToolCategories.ExternalReference, StringComparison.OrdinalIgnoreCase))
        {
            return BenchmarkEvidenceClasses.ExternalReferenceLocal;
        }

        return BenchmarkEvidenceClasses.LocalSmoke;
    }

    private static void AddEvidenceReasons(BenchmarkResult result, string evidenceClass, ICollection<string> reasons)
    {
        if (string.Equals(evidenceClass, BenchmarkEvidenceClasses.LocalLab, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(BenchmarkEvidenceReasons.ManagedLabLoadTool);
        }
        else if (string.Equals(evidenceClass, BenchmarkEvidenceClasses.ExternalReferenceLocal, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(BenchmarkEvidenceReasons.ExternalReferenceLoadToolProven);
        }

        if (IsLocalhostSharedHost(result))
        {
            reasons.Add(BenchmarkEvidenceReasons.LocalhostSharedHost);
            reasons.Add(BenchmarkEvidenceReasons.SingleMachine);
        }

        if (string.Equals(result.LoadToolMode, LoadToolKinds.Docker, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(result.TargetExecutionMode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase) &&
            (result.HostRewriteMode is not null || IsLocalhostSharedHost(result)))
        {
            reasons.Add(BenchmarkEvidenceReasons.DockerLoadToolHostProcessTarget);
        }

        if (string.Equals(result.TargetExecutionMode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(BenchmarkEvidenceReasons.DockerTargetLocal);
            AddDockerResourceReasons(
                reasons,
                result.TargetDockerResourceLimitsRequested,
                result.TargetDockerResourceLimitsEffective,
                targetContainer: true);

            if (string.Equals(result.TargetDockerNetworkMode, TargetNetworkModes.PublishedPort, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add(BenchmarkEvidenceReasons.HostPublishedPort);
                reasons.Add(BenchmarkEvidenceReasons.DockerNetworkSharedHost);
            }
            else if (string.Equals(result.TargetDockerNetworkMode, TargetNetworkModes.SharedDockerNetwork, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add(BenchmarkEvidenceReasons.SharedDockerNetwork);
                reasons.Add(BenchmarkEvidenceReasons.DockerNetworkLocal);
                reasons.Add(BenchmarkEvidenceReasons.TargetHostPortStillPublishedForValidation);
                reasons.Add(BenchmarkEvidenceReasons.CertificateSniConnectToRouting);
                if (result.TargetDockerNetworkName?.StartsWith("protocol-lab-", StringComparison.OrdinalIgnoreCase) == true)
                {
                    reasons.Add(BenchmarkEvidenceReasons.DockerNetworkGenerated);
                }
            }

            if (result.TargetDockerImage?.EndsWith(":local", StringComparison.OrdinalIgnoreCase) == true)
            {
                reasons.Add(BenchmarkEvidenceReasons.DockerTargetImageLocalTag);
            }
        }

        if (string.Equals(result.TargetContract, "adapter-v1", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(BenchmarkEvidenceReasons.AdapterBackedTarget);
        }

        if (IsHostDockerInternalRewrite(result.HostRewriteMode))
        {
            reasons.Add(BenchmarkEvidenceReasons.HostDockerInternalRewrite);
        }

        if (string.Equals(result.LoadToolMode, LoadToolKinds.Docker, StringComparison.OrdinalIgnoreCase))
        {
            AddDockerResourceReasons(
                reasons,
                result.LoadToolDockerResourceLimitsRequested,
                result.LoadToolDockerResourceLimitsEffective,
                targetContainer: false);
        }

        if (result.CertificateMode is not null && IsLoopbackCertificateMode(result.CertificateMode))
        {
            reasons.Add(BenchmarkEvidenceReasons.SelfSignedLoopbackCertificateMode);
            reasons.Add(BenchmarkEvidenceReasons.CertificateModeLocalDev);
        }

        if (result.ProtocolProof?.CertificateMode is not null && IsLoopbackCertificateMode(result.ProtocolProof.CertificateMode))
        {
            reasons.Add(BenchmarkEvidenceReasons.SelfSignedLoopbackCertificateMode);
            reasons.Add(BenchmarkEvidenceReasons.CertificateModeLocalDev);
        }

        if (result.TargetProcessMetrics is null &&
            !(string.Equals(result.TargetExecutionMode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase) &&
              result.TargetDockerMetricsAvailable))
        {
            reasons.Add(BenchmarkEvidenceReasons.NoTargetResourceMetrics);
        }

        if ((result.QlogFileCount ?? 0) > 0)
        {
            reasons.Add(BenchmarkEvidenceReasons.NoQlogProtocolCounterReview);
        }

        if (result.Repetition == 1)
        {
            reasons.Add(BenchmarkEvidenceReasons.NoRepeatedStableMedian);
        }

        if (string.Equals(result.LoadToolCategory, LoadToolCategories.ManagedLab, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(BenchmarkEvidenceReasons.ManagedLabLoadTool);
        }
        else if (string.Equals(result.LoadToolCategory, LoadToolCategories.ExternalReference, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(BenchmarkEvidenceReasons.ExternalReferenceLoadToolProven);
        }

        if (IsLocalhostSharedHost(result))
        {
            reasons.Add(BenchmarkEvidenceReasons.NoCpuIsolation);
            reasons.Add(BenchmarkEvidenceReasons.NoNetworkIsolation);
        }

        AddLoadGeneratorMetricReasons(result, reasons);
        AddTargetContainerMetricReasons(result, reasons);
    }

    private static void AddComparabilityWarnings(BenchmarkResult result, ICollection<string> warnings)
    {
        AddRange(warnings, result.Warnings);
        AddRange(warnings, result.LoadShapeWarnings);
        AddRange(warnings, result.LoadToolH3CapabilityWarnings);
        AddRange(warnings, result.TargetProcessMetrics?.Warnings);

        if (result.ValidationResult.Status != ValidationStatus.Passed)
        {
            warnings.Add(BenchmarkEvidenceReasons.ValidationFailure);
            return;
        }

        if (ProtocolIds.IsHttp3(result.Protocol) && result.ProtocolProof is null)
        {
            warnings.Add(BenchmarkEvidenceReasons.ProtocolProofMissing);
            return;
        }

        if (result.BenchmarkExecutionStatus != LoadToolExecutionStatuses.Succeeded)
        {
            warnings.Add(BenchmarkEvidenceReasons.BenchmarkExecutionFailure);
            return;
        }

        if (HasRequestedEffectiveLoadShapeMismatch(result))
        {
            warnings.Add(BenchmarkEvidenceReasons.DifferentRequestedEffectiveLoadShape);
        }

        if (IsHostDockerInternalRewrite(result.HostRewriteMode))
        {
            warnings.Add(BenchmarkEvidenceReasons.HostDockerInternalRewrite);
        }

        if (string.Equals(result.TargetExecutionMode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(BenchmarkEvidenceReasons.DockerTargetLocal);
            AddDockerResourceReasons(
                warnings,
                result.TargetDockerResourceLimitsRequested,
                result.TargetDockerResourceLimitsEffective,
                targetContainer: true);

            if (string.Equals(result.TargetDockerNetworkMode, TargetNetworkModes.PublishedPort, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(BenchmarkEvidenceReasons.HostPublishedPort);
                warnings.Add(BenchmarkEvidenceReasons.DockerNetworkSharedHost);
            }
            else if (string.Equals(result.TargetDockerNetworkMode, TargetNetworkModes.SharedDockerNetwork, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(BenchmarkEvidenceReasons.SharedDockerNetwork);
                warnings.Add(BenchmarkEvidenceReasons.DockerNetworkLocal);
                warnings.Add(BenchmarkEvidenceReasons.TargetHostPortStillPublishedForValidation);
                warnings.Add(BenchmarkEvidenceReasons.CertificateSniConnectToRouting);
                if (result.TargetDockerNetworkName?.StartsWith("protocol-lab-", StringComparison.OrdinalIgnoreCase) == true)
                {
                    warnings.Add(BenchmarkEvidenceReasons.DockerNetworkGenerated);
                }
            }

            if (result.TargetDockerImage?.EndsWith(":local", StringComparison.OrdinalIgnoreCase) == true)
            {
                warnings.Add(BenchmarkEvidenceReasons.DockerTargetImageLocalTag);
            }
        }

        if (string.Equals(result.TargetContract, "adapter-v1", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(BenchmarkEvidenceReasons.AdapterBackedTarget);
        }

        if ((result.QlogFileCount ?? 0) > 0)
        {
            warnings.Add(BenchmarkEvidenceReasons.NoQlogProtocolCounterReview);
        }

        if (result.Repetition == 1)
        {
            warnings.Add(BenchmarkEvidenceReasons.NoRepeatedStableMedian);
        }

        if (result.ParsedMetricsAvailable == false)
        {
            warnings.Add(BenchmarkEvidenceReasons.ParsedMetricsMissing);
        }

        if (result.TargetProcessMetrics is null &&
            !(string.Equals(result.TargetExecutionMode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase) &&
              result.TargetDockerMetricsAvailable))
        {
            warnings.Add(BenchmarkEvidenceReasons.NoTargetResourceMetrics);
        }

        if (IsLocalhostSharedHost(result))
        {
            warnings.Add(BenchmarkEvidenceReasons.NoCpuIsolation);
            warnings.Add(BenchmarkEvidenceReasons.NoNetworkIsolation);
            warnings.Add(BenchmarkEvidenceReasons.SingleMachine);
        }

        AddLoadGeneratorMetricReasons(result, warnings);
        AddTargetContainerMetricReasons(result, warnings);

        if (string.Equals(result.LoadToolMode, LoadToolKinds.Docker, StringComparison.OrdinalIgnoreCase))
        {
            AddDockerResourceReasons(
                warnings,
                result.LoadToolDockerResourceLimitsRequested,
                result.LoadToolDockerResourceLimitsEffective,
                targetContainer: false);
        }

        var failedRequests = result.Metrics.FailedRequests.GetValueOrDefault();
        var timeoutRequests = result.Metrics.TimeoutRequests.GetValueOrDefault();
        var totalRequests = result.Metrics.TotalRequests.GetValueOrDefault();
        var failureCount = failedRequests + timeoutRequests;
        if (failureCount > 0)
        {
            var ratio = totalRequests > 0 ? (double)failureCount / totalRequests : 1d;
            var severity = ratio >= 0.05d || failureCount > 100
                ? BenchmarkComparabilityStatuses.Invalid
                : BenchmarkComparabilityStatuses.ComparableWithWarnings;
            warnings.Add($"nonzero-failures: failed={failedRequests.ToString(CultureInfo.InvariantCulture)}, timeout={timeoutRequests.ToString(CultureInfo.InvariantCulture)}.");
            if (severity == BenchmarkComparabilityStatuses.Invalid)
            {
                warnings.Add(BenchmarkEvidenceReasons.BenchmarkExecutionFailure);
            }
        }
    }

    private static string DetermineComparabilityStatus(
        BenchmarkResult result,
        string evidenceClass,
        IReadOnlyCollection<string> warnings)
    {
        if (result.ValidationResult.Status != ValidationStatus.Passed)
        {
            return BenchmarkComparabilityStatuses.Invalid;
        }

        if (ProtocolIds.IsHttp3(result.Protocol) && result.ProtocolProof is null)
        {
            return BenchmarkComparabilityStatuses.Invalid;
        }

        if (result.BenchmarkExecutionStatus != LoadToolExecutionStatuses.Succeeded)
        {
            return BenchmarkComparabilityStatuses.Invalid;
        }

        var failedRequests = result.Metrics.FailedRequests.GetValueOrDefault();
        var timeoutRequests = result.Metrics.TimeoutRequests.GetValueOrDefault();
        var totalRequests = result.Metrics.TotalRequests.GetValueOrDefault();
        var failureCount = failedRequests + timeoutRequests;
        if (failureCount > 0)
        {
            var ratio = totalRequests > 0 ? (double)failureCount / totalRequests : 1d;
            if (ratio >= 0.05d || failureCount > 100)
            {
                return BenchmarkComparabilityStatuses.Invalid;
            }
        }

        var status = evidenceClass switch
        {
            BenchmarkEvidenceClasses.IsolatedHost => BenchmarkComparabilityStatuses.ComparableLocal,
            BenchmarkEvidenceClasses.Publishable => BenchmarkComparabilityStatuses.ComparableLocal,
            BenchmarkEvidenceClasses.LocalSmoke => BenchmarkComparabilityStatuses.NotComparable,
            _ => BenchmarkComparabilityStatuses.ComparableWithWarnings
        };

        if (status == BenchmarkComparabilityStatuses.ComparableLocal && warnings.Count > 0)
        {
            return BenchmarkComparabilityStatuses.ComparableWithWarnings;
        }

        return status;
    }

    private static bool IsLocalhostSharedHost(BenchmarkResult result)
    {
        return IsLocalishHost(result.RequestedLoadToolUrl) ||
            IsLocalishHost(result.EffectiveLoadToolUrl) ||
            result.HostRewriteMode is not null;
    }

    private static bool IsLocalishHost(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Host, "host.docker.internal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLoopbackCertificateMode(string certificateMode)
    {
        return certificateMode.Contains("development", StringComparison.OrdinalIgnoreCase) ||
            certificateMode.Contains("local", StringComparison.OrdinalIgnoreCase) ||
            certificateMode.Contains("loopback", StringComparison.OrdinalIgnoreCase) ||
            certificateMode.Contains("self-signed", StringComparison.OrdinalIgnoreCase) ||
            certificateMode.Contains("bypass", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHostDockerInternalRewrite(string? hostRewriteMode)
    {
        return hostRewriteMode?.Contains("host.docker.internal", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool HasRequestedEffectiveLoadShapeMismatch(BenchmarkResult result)
    {
        var requested = result.RequestedLoadShape;
        var effective = result.EffectiveLoadShape;
        if (requested is null || effective is null)
        {
            return false;
        }

        return requested.Connections != effective.Connections ||
            requested.Concurrency != effective.Concurrency ||
            requested.StreamsPerConnection != effective.StreamsPerConnection ||
            requested.DurationSeconds != effective.DurationSeconds ||
            requested.WarmupSeconds != effective.WarmupSeconds ||
            requested.Repetitions != effective.Repetitions;
    }

    private static void AddDockerResourceReasons(
        ICollection<string> target,
        DockerResourceLimits? requested,
        DockerResourceLimits? effective,
        bool targetContainer)
    {
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

        var limits = effective?.HasAnyLimit == true ? effective : requested;
        if (limits?.HasAnyLimit == true)
        {
            target.Add(applied);
            target.Add(BenchmarkEvidenceReasons.DockerResourceLimitsLocalOnly);
        }
        else
        {
            target.Add(resourceMissing);
        }

        if (limits?.HasMemoryLimit != true)
        {
            target.Add(memoryMissing);
        }

        if (limits?.HasCpuIsolation != true)
        {
            target.Add(cpuNotIsolated);
        }
    }

    private static void AddLoadGeneratorMetricReasons(BenchmarkResult result, ICollection<string> target)
    {
        if (string.IsNullOrWhiteSpace(result.LoadTool) ||
            result.BenchmarkExecutionStatus != LoadToolExecutionStatuses.Succeeded)
        {
            return;
        }

        if (!string.Equals(result.LoadToolMode, LoadToolKinds.Docker, StringComparison.OrdinalIgnoreCase))
        {
            target.Add(BenchmarkEvidenceReasons.NoLoadGeneratorSaturationCheck);
            target.Add(BenchmarkEvidenceReasons.LoadGeneratorCpuNotCaptured);
            target.Add(BenchmarkEvidenceReasons.LoadToolNotDocker);
            return;
        }

        if (result.LoadToolDockerMetricsAvailable && result.LoadToolDockerMetricsSummary is not null)
        {
            target.Add(BenchmarkEvidenceReasons.LoadGeneratorMetricsCaptured);
            if (!string.IsNullOrWhiteSpace(result.LoadToolSaturationStatus))
            {
                target.Add(result.LoadToolSaturationStatus);
            }

            AddRange(target, result.LoadToolSaturationWarnings);
            AddRange(target, result.LoadToolDockerMetricsSummary.ParseWarnings);
            return;
        }

        target.Add(BenchmarkEvidenceReasons.NoLoadGeneratorSaturationCheck);
        target.Add(BenchmarkEvidenceReasons.LoadGeneratorCpuNotCaptured);
        target.Add(BenchmarkEvidenceReasons.LoadGeneratorMetricsMissing);
        if (!string.IsNullOrWhiteSpace(result.LoadToolSaturationStatus))
        {
            target.Add(result.LoadToolSaturationStatus);
        }
    }

    private static void AddTargetContainerMetricReasons(BenchmarkResult result, ICollection<string> target)
    {
        if (result.BenchmarkExecutionStatus != LoadToolExecutionStatuses.Succeeded ||
            !string.Equals(result.TargetExecutionMode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (result.TargetDockerMetricsAvailable && result.TargetDockerMetricsSummary is not null)
        {
            target.Add(BenchmarkEvidenceReasons.TargetContainerMetricsCaptured);
            if (!string.IsNullOrWhiteSpace(result.TargetSaturationStatus))
            {
                target.Add(result.TargetSaturationStatus);
            }

            AddRange(target, result.TargetSaturationWarnings);
            AddRange(target, result.TargetDockerMetricsSummary.ParseWarnings);
            return;
        }

        target.Add(BenchmarkEvidenceReasons.TargetContainerMetricsMissing);
        target.Add(BenchmarkEvidenceReasons.TargetContainerCpuNotCaptured);
        if (!string.IsNullOrWhiteSpace(result.TargetSaturationStatus))
        {
            target.Add(result.TargetSaturationStatus);
        }
        else
        {
            target.Add(BenchmarkEvidenceReasons.TargetContainerSaturationUnknown);
        }
    }

    private static void AddRange(ICollection<string> target, IEnumerable<string>? values)
    {
        if (values is null)
        {
            return;
        }

        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private static string WorseStatus(string left, string right)
    {
        return GetStatusRank(right) > GetStatusRank(left) ? right : left;
    }

    private static int GetStatusRank(string status)
    {
        return status switch
        {
            BenchmarkComparabilityStatuses.Invalid => 3,
            BenchmarkComparabilityStatuses.NotComparable => 2,
            BenchmarkComparabilityStatuses.ComparableWithWarnings => 1,
            BenchmarkComparabilityStatuses.ComparableLocal => 0,
            _ => 0
        };
    }

    private static double? ComputeMedian(IReadOnlyList<double> samples)
    {
        if (samples.Count == 0)
        {
            return null;
        }

        var ordered = samples.OrderBy(static sample => sample).ToArray();
        return ordered.Length % 2 == 1
            ? ordered[ordered.Length / 2]
            : (ordered[(ordered.Length / 2) - 1] + ordered[ordered.Length / 2]) / 2.0d;
    }
}
