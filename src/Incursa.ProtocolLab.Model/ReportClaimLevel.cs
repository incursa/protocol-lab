// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public enum ReportClaimLevel
{
    DiagnosticOnly,
    Validation,
    Regression,
    Benchmark,
    Verified
}

public static class ReportClaimLevels
{
    public static bool IsPublishable(ReportClaimLevel claimLevel)
    {
        return claimLevel is ReportClaimLevel.Benchmark or ReportClaimLevel.Verified;
    }
}

public static class ReportClaimDeriver
{
    public static ReportClaimLevel Derive(RunMetadata? metadata, IReadOnlyList<BenchmarkResult> results)
    {
        if (results.Count == 0)
        {
            return ReportClaimLevel.DiagnosticOnly;
        }

        if (results.Any(static result => result.ValidationResult.Status != ValidationStatus.Passed))
        {
            return ReportClaimLevel.DiagnosticOnly;
        }

        if (results.Any(static result => result.BenchmarkExecutionStatus != LoadToolExecutionStatuses.Succeeded))
        {
            return ReportClaimLevel.Validation;
        }

        var executionProfile = metadata?.ExecutionProfile ?? ExecutionProfiles.Parse(results[0].ExecutionProfile);
        var repetitions = results.Select(static result => result.Repetition).Distinct().Count();
        var measurementIntent = DetermineMeasurementIntent(results);
        var hasStableEvidence = results.All(static result =>
            !string.Equals(result.Evidence?.EvidenceClass, BenchmarkEvidenceClasses.LocalSmoke, StringComparison.OrdinalIgnoreCase));

        if (executionProfile is ExecutionProfile.DedicatedLabBareMetal or ExecutionProfile.DedicatedLabContainer)
        {
            if (measurementIntent == LoadProfilePurpose.PublishableBenchmark &&
                repetitions >= 3 &&
                hasStableEvidence &&
                results.All(static result => result.Evidence?.EvidenceClass == BenchmarkEvidenceClasses.Publishable))
            {
                return ReportClaimLevel.Benchmark;
            }

            if (repetitions > 1 && measurementIntent != LoadProfilePurpose.Smoke && hasStableEvidence)
            {
                return ReportClaimLevel.Regression;
            }

            return ReportClaimLevel.Validation;
        }

        if (repetitions > 1 && measurementIntent != LoadProfilePurpose.Smoke && hasStableEvidence)
        {
            return ReportClaimLevel.Regression;
        }

        return ReportClaimLevel.Validation;
    }

    private static LoadProfilePurpose DetermineMeasurementIntent(IReadOnlyList<BenchmarkResult> results)
    {
        var strongest = LoadProfilePurpose.Smoke;
        foreach (var result in results)
        {
            var purpose = ParsePurpose(result.LoadProfilePurpose);
            if (GetStrength(purpose) > GetStrength(strongest))
            {
                strongest = purpose;
            }
        }

        return strongest;
    }

    private static LoadProfilePurpose ParsePurpose(string? purpose)
    {
        return purpose?.Trim().ToLowerInvariant() switch
        {
            "smoke" => LoadProfilePurpose.Smoke,
            "regression" => LoadProfilePurpose.Regression,
            "comparison" => LoadProfilePurpose.Comparison,
            "stress" => LoadProfilePurpose.Stress,
            "soak" => LoadProfilePurpose.Soak,
            "publishable-benchmark" => LoadProfilePurpose.PublishableBenchmark,
            _ => LoadProfilePurpose.Comparison
        };
    }

    private static int GetStrength(LoadProfilePurpose purpose)
    {
        return purpose switch
        {
            LoadProfilePurpose.Smoke => 0,
            LoadProfilePurpose.Regression => 1,
            LoadProfilePurpose.Comparison => 1,
            LoadProfilePurpose.Stress => 2,
            LoadProfilePurpose.Soak => 2,
            LoadProfilePurpose.PublishableBenchmark => 3,
            _ => 1
        };
    }
}
