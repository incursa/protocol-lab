// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public enum ExecutionProfile
{
    LocalProcess,
    LocalDockerBridge,
    LocalDockerHostNetwork,
    RemoteProcess,
    RemoteDocker,
    CiContainer,
    DedicatedLabBareMetal,
    DedicatedLabContainer
}

public static class ExecutionProfiles
{
    private static readonly IReadOnlyDictionary<string, ExecutionProfile> Aliases = new Dictionary<string, ExecutionProfile>(StringComparer.OrdinalIgnoreCase)
    {
        ["local-process"] = ExecutionProfile.LocalProcess,
        ["localprocess"] = ExecutionProfile.LocalProcess,
        ["process"] = ExecutionProfile.LocalProcess,
        ["local-docker-bridge"] = ExecutionProfile.LocalDockerBridge,
        ["localdockerbridge"] = ExecutionProfile.LocalDockerBridge,
        ["docker"] = ExecutionProfile.LocalDockerBridge,
        ["local-docker-host-network"] = ExecutionProfile.LocalDockerHostNetwork,
        ["localdockerhostnetwork"] = ExecutionProfile.LocalDockerHostNetwork,
        ["host-network"] = ExecutionProfile.LocalDockerHostNetwork,
        ["hostnetwork"] = ExecutionProfile.LocalDockerHostNetwork,
        ["remote-process"] = ExecutionProfile.RemoteProcess,
        ["remoteprocess"] = ExecutionProfile.RemoteProcess,
        ["remote-docker"] = ExecutionProfile.RemoteDocker,
        ["remotedocker"] = ExecutionProfile.RemoteDocker,
        ["ci-container"] = ExecutionProfile.CiContainer,
        ["cicontainer"] = ExecutionProfile.CiContainer,
        ["ci"] = ExecutionProfile.CiContainer,
        ["dedicated-lab-bare-metal"] = ExecutionProfile.DedicatedLabBareMetal,
        ["dedicatedlabbaremetal"] = ExecutionProfile.DedicatedLabBareMetal,
        ["dedicated-lab-container"] = ExecutionProfile.DedicatedLabContainer,
        ["dedicatedlabcontainer"] = ExecutionProfile.DedicatedLabContainer
    };

    public static string ToId(ExecutionProfile profile)
    {
        return profile switch
        {
            ExecutionProfile.LocalProcess => "local-process",
            ExecutionProfile.LocalDockerBridge => "local-docker-bridge",
            ExecutionProfile.LocalDockerHostNetwork => "local-docker-host-network",
            ExecutionProfile.RemoteProcess => "remote-process",
            ExecutionProfile.RemoteDocker => "remote-docker",
            ExecutionProfile.CiContainer => "ci-container",
            ExecutionProfile.DedicatedLabBareMetal => "dedicated-lab-bare-metal",
            ExecutionProfile.DedicatedLabContainer => "dedicated-lab-container",
            _ => profile.ToString()
        };
    }

    public static ExecutionProfile Parse(string? value, ExecutionProfile fallback = ExecutionProfile.LocalProcess)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        if (Aliases.TryGetValue(trimmed, out var profile))
        {
            return profile;
        }

        return Enum.TryParse<ExecutionProfile>(trimmed, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
    }

    public static ExecutionProfile Infer(string? targetMode, string? targetNetworkMode, string? explicitProfile = null)
    {
        var explicitValue = Parse(explicitProfile, ExecutionProfile.LocalProcess);
        if (!string.IsNullOrWhiteSpace(explicitProfile))
        {
            return explicitValue;
        }

        if (string.Equals(targetMode, "external", StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionProfile.RemoteProcess;
        }

        if (string.Equals(targetMode, "docker", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(targetNetworkMode, "host", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetNetworkMode, "host-network", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetNetworkMode, "host-network-mode", StringComparison.OrdinalIgnoreCase)
                ? ExecutionProfile.LocalDockerHostNetwork
                : ExecutionProfile.LocalDockerBridge;
        }

        return ExecutionProfile.LocalProcess;
    }
}
