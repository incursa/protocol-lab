// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public sealed record SupportResult(bool IsSupported, string Reason)
{
    public static SupportResult Supported { get; } = new(true, "supported");
}

public static class ScenarioSupport
{
    public static SupportResult Evaluate(ImplementationManifest implementation, ScenarioDefinition scenario, string protocol)
    {
        var failures = new List<string>();

        var effectiveRoles = scenario.GetEffectiveRoles();
        foreach (var role in effectiveRoles)
        {
            if (!ContainsIgnoreCase(implementation.Roles, role))
            {
                failures.Add($"role '{role}' is not supported");
            }
        }

        if (!string.IsNullOrWhiteSpace(scenario.ImplementationRole) && effectiveRoles.Count == 0)
        {
            if (!ContainsIgnoreCase(implementation.Roles, scenario.ImplementationRole))
            {
                failures.Add($"role '{scenario.ImplementationRole}' is not supported");
            }
        }

        if (!implementation.SupportedProtocols.Any(candidate =>
                string.Equals(ProtocolIds.Normalize(candidate), ProtocolIds.Normalize(protocol), StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add($"protocol '{protocol}' is not supported");
        }

        if (!ContainsIgnoreCase(implementation.SupportedWorkloadFamilies, scenario.Family))
        {
            failures.Add($"workload family '{scenario.Family}' is not supported");
        }

        foreach (var capability in scenario.RequiredCapabilities)
        {
            if (!ContainsIgnoreCase(implementation.Capabilities, capability))
            {
                failures.Add($"capability '{capability}' is not supported");
            }
        }

        return failures.Count == 0
            ? SupportResult.Supported
            : new SupportResult(false, string.Join("; ", failures));
    }

    private static bool ContainsIgnoreCase(IEnumerable<string> values, string value)
    {
        return values.Any(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
    }
}
