// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Adapter.Conformance;

public enum RunPlanConformanceOutcome
{
    Passed,
    Failed,
}

public sealed record RunPlanConformanceOptions
{
    public string RunPlanSchemaPath { get; init; } = "";

    public string PackageSchemaPath { get; init; } = "";

    public string TestExecutorSchemaRootPath { get; init; } = "";

    public string ScenarioSchemaPath { get; init; } = "";

    public IReadOnlyList<string> PackagePaths { get; init; } = [];

    public bool ValidatePackages { get; init; } = true;
}

public sealed record RunPlanConformanceStepResult(
    string Step,
    RunPlanConformanceOutcome Outcome,
    string Message,
    string? Path = null,
    IReadOnlyList<string>? Diagnostics = null);

public sealed record RunPlanConformanceReport(
    string RunPlanPath,
    RunPlanConformanceOutcome Outcome,
    IReadOnlyList<RunPlanConformanceStepResult> Steps,
    string? RunPlanId = null,
    string? RunPlanVersion = null);
