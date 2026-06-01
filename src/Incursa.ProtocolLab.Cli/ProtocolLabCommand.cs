// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Runner;

namespace Incursa.ProtocolLab.Cli;

internal static class ProtocolLabCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            WriteUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var commandArgs = args.Skip(1).ToArray();
        var options = CliOptions.Parse(commandArgs).ToRunnerOptions();
        var root = options.Get("root") ?? Directory.GetCurrentDirectory();
        var runner = new RunnerEngine();

        try
        {
            var result = command switch
            {
                "list" => runner.List(commandArgs, root),
                "validate" => await runner.ValidateAsync(root, options),
                "run" => await runner.RunBenchmarkAsync(root, options),
                "check" or "doctor" => await runner.CheckAsync(root),
                "report" => runner.Report(root, options),
                "publish-report" => await runner.PublishReportAsync(root, options),
                _ => Unknown(command)
            };
            RunnerConsoleRenderer.Render(result);
            return result.ExitCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "help";
    }

    private static RunnerCommandResult Unknown(string command)
    {
        return RunnerCommandResult.Create(
            RunnerCommandKind.Check,
            1,
            [new RunnerMessage(RunnerMessageSeverity.Error, $"Unknown command '{command}'.")]);
    }

    private static void WriteUsage()
    {
        Console.WriteLine("""
        protocol-lab commands:
          list implementations [--root <path>]
          list scenarios [--root <path>]
          list network-profiles [--root <path>]
          list load-tools [--root <path>]
          check [--root <path>]
          validate --implementations <ids> --scenarios <ids> [--base-url <url>] [--protocol <id>] [--run-id <id>] [--execution-profile <id>] [--target-mode process|docker|external] [--target-network-mode published-port|shared-docker-network] [--target-configuration <Debug|Release>] [--network-profile <id>]
          run --implementations <ids> --scenarios <ids> [--base-url <url>] [--run-id <id>] [--execution-profile <id>] [--target-mode process|docker|external] [--target-network-mode published-port|shared-docker-network] [--target-configuration <Debug|Release>] [--target-docker-build] [--target-docker-image <image>] [--load-profile <id>] [--load-tool h2load|oha|managed-httpclient-h3-load] [--load-tool-mode process|docker|managed] [--disable-load-tool-qlog] [--capture-load-tool-metrics] [--load-tool-metrics-interval <seconds>] [--capture-target-container-metrics] [--target-container-metrics-interval <seconds>] [--network-profile <id>] [--output <path>] [--publication-output <path>]
          report --run-id <id> [--output <path>]
          publish-report --run <path> [--output <path>] [--visibility public] [--dry-run] [--allow-diagnostic-publication]
""");
    }
}
