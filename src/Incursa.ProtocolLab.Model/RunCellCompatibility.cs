// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public sealed record RunCellCompatibility(string Status, string? Reason)
{
    public bool CanRun => string.Equals(Status, RunCellCompatibilityStatuses.Runnable, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(Status, RunCellCompatibilityStatuses.Supported, StringComparison.OrdinalIgnoreCase);

    public static RunCellCompatibility Supported(string? reason = null)
    {
        return new RunCellCompatibility(RunCellCompatibilityStatuses.Supported, reason);
    }

    public static RunCellCompatibility Runnable()
    {
        return new RunCellCompatibility(RunCellCompatibilityStatuses.Runnable, null);
    }

    public static RunCellCompatibility Unsupported(string reason)
    {
        return new RunCellCompatibility(RunCellCompatibilityStatuses.Unsupported, reason);
    }

    public static RunCellCompatibility MissingCapability(string reason)
    {
        return new RunCellCompatibility(RunCellCompatibilityStatuses.MissingCapability, reason);
    }

    public static RunCellCompatibility MissingLoadTool(string reason)
    {
        return new RunCellCompatibility(RunCellCompatibilityStatuses.MissingLoadTool, reason);
    }

    public static RunCellCompatibility ExperimentalNotEnabled(string reason)
    {
        return new RunCellCompatibility(RunCellCompatibilityStatuses.ExperimentalNotEnabled, reason);
    }

    public static RunCellCompatibility PlaceholderNotRunnable(string reason)
    {
        return new RunCellCompatibility(RunCellCompatibilityStatuses.PlaceholderNotRunnable, reason);
    }

    public static RunCellCompatibility Unavailable(string reason)
    {
        return new RunCellCompatibility(RunCellCompatibilityStatuses.Unavailable, reason);
    }

    public static RunCellCompatibility Incompatible(string reason)
    {
        return new RunCellCompatibility(RunCellCompatibilityStatuses.Incompatible, reason);
    }

    public static RunCellCompatibility IncompatibleTrafficShape(string reason)
    {
        return new RunCellCompatibility(RunCellCompatibilityStatuses.IncompatibleTrafficShape, reason);
    }

    public static RunCellCompatibility IncompatibleLoadProfile(string reason)
    {
        return new RunCellCompatibility(RunCellCompatibilityStatuses.IncompatibleLoadProfile, reason);
    }

    public static RunCellCompatibility ExperimentalProfileNotEnabled(string reason)
    {
        return new RunCellCompatibility(RunCellCompatibilityStatuses.ExperimentalProfileNotEnabled, reason);
    }
}

public static class RunCellCompatibilityStatuses
{
    public const string Supported = "supported";
    public const string Runnable = "runnable";
    public const string Unsupported = "unsupported";
    public const string MissingCapability = "missing-capability";
    public const string MissingLoadTool = "missing-load-tool";
    public const string ExperimentalNotEnabled = "experimental-not-enabled";
    public const string PlaceholderNotRunnable = "placeholder-not-runnable";
    public const string Unavailable = "unavailable";
    public const string Incompatible = "incompatible";
    public const string IncompatibleTrafficShape = "incompatible-traffic-shape";
    public const string IncompatibleLoadProfile = "incompatible-load-profile";
    public const string ExperimentalProfileNotEnabled = "experimental-profile-not-enabled";
}
