// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

public static class CompatibilityClassifier
{
    public static RunCellCompatibility Classify(
        RunCell cell,
        IReadOnlyList<LoadToolManifest>? loadTools = null,
        string? requestedLoadTool = null,
        IReadOnlyList<NetworkProfileDefinition>? networkProfiles = null,
        IReadOnlyList<LoadProfileDefinition>? loadProfiles = null,
        bool allowExperimental = false,
        bool allowPlaceholder = false)
    {
        if (cell.Scenario.IsPlaceholder() && !allowPlaceholder)
        {
            return RunCellCompatibility.PlaceholderNotRunnable(
                $"Scenario '{cell.Scenario.Id}' is a placeholder and cannot run without explicit opt-in.");
        }

        if (cell.Scenario.IsPlaceholder() && allowPlaceholder)
        {
            return RunCellCompatibility.PlaceholderNotRunnable(
                $"Scenario '{cell.Scenario.Id}' is a placeholder and no validator or load tool is implemented.");
        }

        if (cell.Scenario.IsExperimental() && !allowExperimental)
        {
            return RunCellCompatibility.ExperimentalNotEnabled(
                $"Scenario '{cell.Scenario.Id}' is experimental and requires explicit opt-in.");
        }

        var scenarioSupport = ScenarioSupport.Evaluate(cell.Implementation, cell.Scenario, cell.Protocol);
        if (!scenarioSupport.IsSupported)
        {
            return RunCellCompatibility.MissingCapability(scenarioSupport.Reason);
        }

        if (!string.IsNullOrWhiteSpace(cell.LoadProfileId) && loadProfiles is { Count: > 0 })
        {
            var profile = loadProfiles.FirstOrDefault(p =>
                string.Equals(p.Id, cell.LoadProfileId, StringComparison.OrdinalIgnoreCase));
            if (profile is null)
            {
                return RunCellCompatibility.IncompatibleLoadProfile(
                    $"Load profile '{cell.LoadProfileId}' was not found.");
            }

            if (profile.IsExperimental() && !allowExperimental)
            {
                return RunCellCompatibility.ExperimentalProfileNotEnabled(
                    $"Load profile '{cell.LoadProfileId}' is experimental and requires explicit opt-in.");
            }
        }

        if (!string.Equals(cell.NetworkProfile, "clean", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(cell.NetworkProfile, "none", StringComparison.OrdinalIgnoreCase))
        {
            if (networkProfiles is not null)
            {
                var networkProfile = networkProfiles.FirstOrDefault(profile =>
                    string.Equals(profile.Id, cell.NetworkProfile, StringComparison.OrdinalIgnoreCase));
                if (networkProfile is null)
                {
                    return RunCellCompatibility.Incompatible($"Network profile '{cell.NetworkProfile}' was not found.");
                }

                var networkSupport = NetworkProfileSupportEvaluator.Evaluate(networkProfile);
                if (!networkSupport.IsSupported)
                {
                    return RunCellCompatibility.Unsupported(networkSupport.Reason);
                }
            }
            else
            {
                return RunCellCompatibility.Unsupported($"Network profile '{cell.NetworkProfile}' requires an impairment provider that is not executable in Runner v1.");
            }
        }

        if (loadTools is { Count: > 0 })
        {
            var protocolFamilyCompatible = loadTools.Any(tool =>
                tool.SupportedProtocols.Contains(cell.Protocol, StringComparer.OrdinalIgnoreCase) &&
                tool.SupportedScenarioFamilies.Contains(cell.Scenario.Family, StringComparer.OrdinalIgnoreCase));
            if (!protocolFamilyCompatible)
            {
                return RunCellCompatibility.MissingLoadTool($"No load tool supports protocol '{cell.Protocol}' and scenario family '{cell.Scenario.Family}'.");
            }

            var trafficShape = cell.Scenario.TrafficShape;
            if (!string.IsNullOrWhiteSpace(trafficShape))
            {
                var shapeCompatible = loadTools.Any(tool => tool.SupportsTrafficShape(trafficShape));
                if (!shapeCompatible)
                {
                    return RunCellCompatibility.IncompatibleTrafficShape(
                        $"No load tool supports traffic shape '{trafficShape}' for scenario '{cell.Scenario.Id}'.");
                }
            }

            if (!string.IsNullOrWhiteSpace(requestedLoadTool))
            {
                var requested = loadTools.FirstOrDefault(tool =>
                    string.Equals(tool.Id, requestedLoadTool, StringComparison.OrdinalIgnoreCase));
                if (requested is null)
                {
                    return RunCellCompatibility.MissingLoadTool($"Requested load tool '{requestedLoadTool}' is not present in the manifest catalog.");
                }

                if (!string.IsNullOrWhiteSpace(trafficShape) && !requested.SupportsTrafficShape(trafficShape))
                {
                    return RunCellCompatibility.IncompatibleTrafficShape(
                        $"Requested load tool '{requestedLoadTool}' does not support traffic shape '{trafficShape}'.");
                }
            }
        }

        if (cell.Scenario.IsExperimental() && allowExperimental)
        {
            return RunCellCompatibility.Supported(
                $"Scenario '{cell.Scenario.Id}' is experimental. Results may not be comparable.");
        }

        return RunCellCompatibility.Supported();
    }
}
