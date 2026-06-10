// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Adapter.Conformance;

public enum PackageConformanceOutcome
{
    Passed,
    Failed,
}

public sealed record PackageConformanceOptions
{
    public string PackageSchemaPath { get; init; } = "";

    public string TestExecutorSchemaRootPath { get; init; } = "";

    public string ScenarioSchemaPath { get; init; } = "";

    public bool ValidateEntryManifestSchemas { get; init; } = true;
}

public sealed record PackageConformanceStepResult(
    string Step,
    PackageConformanceOutcome Outcome,
    string Message,
    string? Path = null,
    IReadOnlyList<string>? Diagnostics = null);

public sealed record PackageConformanceReport(
    string PackagePath,
    PackageConformanceOutcome Outcome,
    IReadOnlyList<PackageConformanceStepResult> Steps,
    string? PackageId = null,
    string? PackageVersion = null,
    string? Kind = null);
