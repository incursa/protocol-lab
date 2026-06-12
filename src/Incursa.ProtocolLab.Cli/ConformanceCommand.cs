// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Adapter.Conformance;
using Incursa.ProtocolLab.Adapter.Contracts;

namespace Incursa.ProtocolLab.Cli;

internal static class ConformanceCommand
{
    public static async Task<int> RunAsync(string[] args, string root)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            WriteUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var commandArgs = args.Skip(1).ToArray();
        var options = CliOptions.Parse(commandArgs);
        var effectiveRoot = Path.GetFullPath(options.Get("root") ?? root);

        try
        {
            return command switch
            {
                "package" => await RunPackageAsync(commandArgs, options, effectiveRoot).ConfigureAwait(false),
                "run-plan" => await RunRunPlanAsync(commandArgs, options, effectiveRoot).ConfigureAwait(false),
                "adapter" => await RunAdapterAsync(options, effectiveRoot).ConfigureAwait(false),
                "test-executor" => await RunTestExecutorAsync(options, effectiveRoot).ConfigureAwait(false),
                _ => Unknown(command)
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static async Task<int> RunPackageAsync(string[] args, CliOptions options, string root)
    {
        var packagePath = options.Get("package");
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            Console.Error.WriteLine("conformance package requires --package <path>.");
            return 1;
        }

        var validator = new PackageConformanceValidator();
        var report = await validator.ValidateAsync(
            Path.GetFullPath(packagePath),
            new PackageConformanceOptions
            {
                PackageSchemaPath = Path.Combine(root, "schemas", "package", "v2", "package.schema.json"),
                TestExecutorSchemaRootPath = Path.Combine(root, "schemas", "test-executor", "v1"),
                ScenarioSchemaPath = Path.Combine(root, "schemas", "scenario.schema.json"),
                ValidateEntryManifestSchemas = !HasSwitch(args, "skip-entry-schema-validation")
            }).ConfigureAwait(false);

        Render(report);
        return report.Outcome == PackageConformanceOutcome.Passed ? 0 : 1;
    }

    private static async Task<int> RunRunPlanAsync(string[] args, CliOptions options, string root)
    {
        var runPlanPath = options.Get("run-plan");
        if (string.IsNullOrWhiteSpace(runPlanPath))
        {
            Console.Error.WriteLine("conformance run-plan requires --run-plan <path>.");
            return 1;
        }

        var packagePaths = GetOptionValues(args, "package")
            .Select(Path.GetFullPath)
            .ToArray();
        if (packagePaths.Length == 0)
        {
            Console.Error.WriteLine("conformance run-plan requires at least one --package <path>.");
            return 1;
        }

        var validator = new RunPlanConformanceValidator();
        var report = await validator.ValidateAsync(
            Path.GetFullPath(runPlanPath),
            new RunPlanConformanceOptions
            {
                RunPlanSchemaPath = Path.Combine(root, "schemas", "run-plan", "v1", "run-plan.schema.json"),
                PackageSchemaPath = Path.Combine(root, "schemas", "package", "v2", "package.schema.json"),
                TestExecutorSchemaRootPath = Path.Combine(root, "schemas", "test-executor", "v1"),
                ScenarioSchemaPath = Path.Combine(root, "schemas", "scenario.schema.json"),
                PackagePaths = packagePaths,
                ValidatePackages = !HasSwitch(args, "skip-package-validation")
            }).ConfigureAwait(false);

        Render(report);
        return report.Outcome == RunPlanConformanceOutcome.Passed ? 0 : 1;
    }

    private static async Task<int> RunAdapterAsync(CliOptions options, string root)
    {
        var baseUrl = options.Get("base-url");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            Console.Error.WriteLine("conformance adapter requires --base-url <url>.");
            return 1;
        }

        var protocol = options.Get("protocol") ?? "h3";
        var suite = new AdapterConformanceSuite();
        var report = await suite.RunAsync(
            new Uri(baseUrl),
            new AdapterConformanceScenario
            {
                ScenarioId = options.Get("scenario-id") ?? "adapter.conformance",
                ScenarioVersion = options.Get("scenario-version") ?? "1.0",
                Role = options.Get("role") ?? "server",
                Protocol = protocol,
                RunId = options.Get("run-id") ?? "adapter-conformance",
                CellId = options.Get("cell-id") ?? "adapter-conformance",
                SessionLabel = options.Get("session-label") ?? "adapter-conformance",
                RequestedEndpointBindings =
                [
                    new AdapterEndpointBinding
                    {
                        BindingId = options.Get("binding-id") ?? "primary",
                        Purpose = options.Get("binding-purpose") ?? "test-endpoint",
                        EndpointType = options.Get("endpoint-type") ?? protocol
                    }
                ],
                ArtifactOutputExpectations =
                [
                    new AdapterArtifactExpectation
                    {
                        ArtifactType = options.Get("artifact-type") ?? "log",
                        Required = true
                    }
                ]
            },
            new AdapterConformanceOptions
            {
                SchemaRootPath = Path.Combine(root, "schemas", "adapter", "v1"),
                SupportedContractVersion = "v1",
                Timeout = ParseTimeout(options),
                ValidateSchemas = !IsTrue(options.Get("no-schema-validation")),
                ValidateInvalidLifecycleTransition = !IsTrue(options.Get("skip-invalid-transition")),
                ValidateDeleteIdempotency = !IsTrue(options.Get("skip-delete-idempotency"))
            }).ConfigureAwait(false);

        Render(report);
        return report.Outcome == AdapterConformanceOutcome.Passed ? 0 : 1;
    }

    private static async Task<int> RunTestExecutorAsync(CliOptions options, string root)
    {
        var baseUrl = options.Get("base-url");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            Console.Error.WriteLine("conformance test-executor requires --base-url <url>.");
            return 1;
        }

        var protocol = options.Get("protocol") ?? "h3";
        var suite = new TestExecutorConformanceSuite();
        var report = await suite.RunAsync(
            new Uri(baseUrl),
            new TestExecutorConformanceScenario
            {
                TestId = options.Get("test-id") ?? "test-executor.conformance",
                ScenarioId = options.Get("scenario-id") ?? "test-executor.conformance",
                ScenarioVersion = options.Get("scenario-version") ?? "1.0",
                Protocol = protocol,
                RunId = options.Get("run-id") ?? "test-executor-conformance",
                CellId = options.Get("cell-id") ?? "test-executor-conformance",
                SessionLabel = options.Get("session-label") ?? "test-executor-conformance",
                TargetEndpoints =
                [
                    new TestExecutorTargetEndpoint
                    {
                        BindingId = options.Get("binding-id") ?? "primary",
                        EndpointId = options.Get("target-endpoint-id") ?? "target-001",
                        Purpose = options.Get("binding-purpose") ?? "test-endpoint",
                        Scheme = options.Get("target-scheme") ?? "https",
                        Protocol = protocol,
                        Host = options.Get("target-host") ?? "127.0.0.1",
                        Port = ParseInt(options.Get("target-port"), 4433, "target-port"),
                        Path = options.Get("target-path") ?? "/"
                    }
                ],
                ArtifactOutputExpectations =
                [
                    new TestExecutorArtifactExpectation
                    {
                        ArtifactType = options.Get("artifact-type") ?? "log",
                        Required = IsTrue(options.Get("require-artifacts"))
                    }
                ]
            },
            new TestExecutorConformanceOptions
            {
                SchemaRootPath = Path.Combine(root, "schemas", "test-executor", "v1"),
                SupportedContractVersion = "test-executor-v1",
                Timeout = ParseTimeout(options),
                ValidateSchemas = !IsTrue(options.Get("no-schema-validation")),
                RequireAvailableMetrics = IsTrue(options.Get("require-metrics")),
                RequireAvailableArtifacts = IsTrue(options.Get("require-artifacts")),
                ValidateDeleteIdempotency = !IsTrue(options.Get("skip-delete-idempotency"))
            }).ConfigureAwait(false);

        Render(report);
        return report.Outcome == TestExecutorConformanceOutcome.Passed ? 0 : 1;
    }

    private static void Render(PackageConformanceReport report)
    {
        Console.WriteLine($"ProtocolLab package conformance: {report.Outcome}");
        if (!string.IsNullOrWhiteSpace(report.PackageId))
        {
            Console.WriteLine($"Package: {report.PackageId} {report.PackageVersion} ({report.Kind})");
        }

        foreach (var step in report.Steps)
        {
            RenderStep(step.Outcome == PackageConformanceOutcome.Passed, step.Step, step.Message, step.Path, step.Diagnostics);
        }
    }

    private static void Render(RunPlanConformanceReport report)
    {
        Console.WriteLine($"ProtocolLab run plan conformance: {report.Outcome}");
        if (!string.IsNullOrWhiteSpace(report.RunPlanId))
        {
            Console.WriteLine($"Run plan: {report.RunPlanId} {report.RunPlanVersion}");
        }

        foreach (var step in report.Steps)
        {
            RenderStep(step.Outcome == RunPlanConformanceOutcome.Passed, step.Step, step.Message, step.Path, step.Diagnostics);
        }
    }

    private static void Render(AdapterConformanceReport report)
    {
        Console.WriteLine($"ProtocolLab Adapter v1 conformance: {report.Outcome}");
        Console.WriteLine($"Control plane: {report.ControlPlaneBaseUrl}");
        foreach (var step in report.Steps)
        {
            RenderStep(step.Outcome == AdapterConformanceOutcome.Passed, step.Step, step.Message, step.SchemaPath, step.Diagnostics);
        }
    }

    private static void Render(TestExecutorConformanceReport report)
    {
        Console.WriteLine($"ProtocolLab Test Executor v1 conformance: {report.Outcome}");
        Console.WriteLine($"Control plane: {report.ControlPlaneBaseUrl}");
        foreach (var step in report.Steps)
        {
            RenderStep(step.Outcome == TestExecutorConformanceOutcome.Passed, step.Step, step.Message, step.SchemaPath, step.Diagnostics);
        }
    }

    private static void RenderStep(bool passed, string step, string message, string? path, IReadOnlyList<string>? diagnostics)
    {
        var status = passed ? "PASS" : "FAIL";
        Console.WriteLine($"[{status}] {step}: {message}");
        if (!string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine($"  path: {path}");
        }

        if (diagnostics is null)
        {
            return;
        }

        foreach (var diagnostic in diagnostics)
        {
            Console.WriteLine($"  - {diagnostic}");
        }
    }

    private static TimeSpan ParseTimeout(CliOptions options)
    {
        return TimeSpan.FromSeconds(ParseInt(options.Get("timeout-seconds"), 10, "timeout-seconds"));
    }

    private static int ParseInt(string? value, int defaultValue, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, out var parsed) || parsed < 0)
        {
            throw new ArgumentException($"--{name} must be a non-negative integer.");
        }

        return parsed;
    }

    private static bool HasSwitch(string[] args, string name)
    {
        return args.Any(arg => string.Equals(arg, "--" + name, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> GetOptionValues(string[] args, string name)
    {
        var option = "--" + name;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], option, StringComparison.OrdinalIgnoreCase))
            {
                yield return args[i + 1];
            }
        }
    }

    private static bool IsTrue(string? value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "help";
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown conformance command '{command}'.");
        WriteUsage();
        return 1;
    }

    private static void WriteUsage()
    {
        Console.WriteLine("""
        protocol-lab conformance commands:
          conformance package --package <path> [--root <repo-root>] [--skip-entry-schema-validation]
          conformance run-plan --run-plan <path> --package <path> [--package <path> ...] [--root <repo-root>] [--skip-package-validation]
          conformance adapter --base-url <url> [--scenario-id <id>] [--scenario-version <version>] [--role <role>] [--protocol <id>] [--endpoint-type <type>] [--artifact-type <type>] [--timeout-seconds <seconds>]
          conformance test-executor --base-url <url> [--test-id <id>] [--scenario-id <id>] [--scenario-version <version>] [--protocol <id>] [--target-scheme <scheme>] [--target-host <host>] [--target-port <port>] [--target-path <path>] [--timeout-seconds <seconds>] [--require-metrics] [--require-artifacts]
        """);
    }
}
