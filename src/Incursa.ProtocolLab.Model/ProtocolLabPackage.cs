// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Incursa.ProtocolLab.Model;

public static class ProtocolLabPackageSchemaVersions
{
    public const string V1 = "protocol-lab-package-v1";
    public const string V2 = "protocol-lab-package-v2";
}

public static class ProtocolLabPackageKinds
{
    public const string Implementation = "implementation";
    public const string TestExecutor = "test-executor";
    public const string ScenarioPack = "scenario-pack";
    public const string Toolchain = "toolchain";
}

public sealed record ProtocolLabPackageManifest
{
    public string SchemaVersion { get; init; } = "";

    public string PackageId { get; init; } = "";

    public string PackageVersion { get; init; } = "";

    public string Kind { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public List<ProtocolLabPackageProvidedImplementation> ProvidedImplementations { get; init; } = [];

    public List<ProtocolLabPackageProvidedTestExecutor> ProvidedTestExecutors { get; init; } = [];

    public List<ProtocolLabPackageProvidedScenario> ProvidedScenarios { get; init; } = [];

    public List<ProtocolLabPackageProvidedSuite> ProvidedSuites { get; init; } = [];

    public List<string> EntryManifests { get; init; } = [];

    public List<ProtocolLabPackageEnvironment> Environments { get; init; } = [];

    public ProtocolLabPackageDependencies Dependencies { get; init; } = new();
}

public sealed record ProtocolLabPackageProvidedImplementation
{
    public string ImplementationId { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public List<string> Protocols { get; init; } = [];

    public List<string> Scenarios { get; init; } = [];
}

public sealed record ProtocolLabPackageProvidedTestExecutor
{
    public string TestExecutorId { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public List<string> Protocols { get; init; } = [];

    public List<string> Scenarios { get; init; } = [];

    public List<string> Tests { get; init; } = [];

    public List<ProtocolLabPackageCapabilityRequirement> RequiredCapabilities { get; init; } = [];
}

public sealed record ProtocolLabPackageProvidedScenario
{
    public string ScenarioId { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public List<string> Protocols { get; init; } = [];
}

public sealed record ProtocolLabPackageProvidedSuite
{
    public string SuiteId { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public List<string> Protocols { get; init; } = [];

    public List<string> TestExecutors { get; init; } = [];
}

public sealed record ProtocolLabPackageEnvironment
{
    public string Os { get; init; } = "";

    public string Arch { get; init; } = "";

    public ProtocolLabPackageEntrypoint Entrypoint { get; init; } = new();
}

public sealed record ProtocolLabPackageEntrypoint
{
    public string Kind { get; init; } = "";

    public string Path { get; init; } = "";

    public List<string> Arguments { get; init; } = [];

    public string WorkingDirectory { get; init; } = ".";
}

public sealed record ProtocolLabPackageDependencies
{
    public bool RequiresDotNet { get; init; }

    public bool RequiresDocker { get; init; }

    public bool RequiresPwsh { get; init; }

    public bool RequiresBash { get; init; }

    public bool RequiresGo { get; init; }

    public List<ProtocolLabPackageCapabilityRequirement> RequiredCapabilities { get; init; } = [];
}

public sealed record ProtocolLabPackageCapabilityRequirement
{
    public string Name { get; init; } = "";

    public string? Value { get; init; }
}

[JsonSerializable(typeof(ProtocolLabPackageManifest))]
[JsonSerializable(typeof(ProtocolLabPackageProvidedImplementation))]
[JsonSerializable(typeof(ProtocolLabPackageProvidedTestExecutor))]
[JsonSerializable(typeof(ProtocolLabPackageProvidedScenario))]
[JsonSerializable(typeof(ProtocolLabPackageProvidedSuite))]
[JsonSerializable(typeof(ProtocolLabPackageEnvironment))]
[JsonSerializable(typeof(ProtocolLabPackageEntrypoint))]
[JsonSerializable(typeof(ProtocolLabPackageDependencies))]
[JsonSerializable(typeof(ProtocolLabPackageCapabilityRequirement))]
public partial class ProtocolLabPackageJsonContext : JsonSerializerContext;
