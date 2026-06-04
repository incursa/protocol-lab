# Local Benchmark Workflow

This repo keeps benchmark compute out of CI. Use the local scripts below
to run selected benchmark and test sets, then publish completed runs to R2/D1.

## Prerequisites

```powershell
dotnet tool restore
dotnet restore Incursa.ProtocolLab.sln
```

## Run Build, Tests, Check, And Selected Benchmark Suites

Run the local workflow script when you want build, tests, check, and one or
more benchmark suites in a single command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\benchmarking\Invoke-ProtocolLabBenchmarkSet.ps1 `
  -RunBuild `
  -RunTests `
  -RunCheck `
  -Suite ci-public-report,h3-local-v1-comparison,quic-transport-v1-comparison `
  -RunIdPrefix local-workflow
```

To run only the full HTTP/3 comparison suite:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\benchmarking\Invoke-ProtocolLabBenchmarkSet.ps1 `
  -Suite h3-local-v1-comparison `
  -RunIdPrefix local-h3-comparison
```

To run only the raw QUIC comparison suite:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\benchmarking\Invoke-ProtocolLabBenchmarkSet.ps1 `
  -Suite quic-transport-v1-comparison `
  -RunIdPrefix local-quic-comparison
```

The script keeps going when a stage fails and writes:

- `.artifacts\runs\workflow-summary.md`
- `.artifacts\runs\<runId>\summary.md`
- `.artifacts\runs\<runId>\evidence-report-v1.json`
- `.artifacts\runs\<runId>\evidence-report-v1.md`
- `.artifacts\runs\<runId>\aggregate-results.json`

## Run Everything

Run the full benchmark suite catalog without supplying suite IDs:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\benchmarking\Invoke-ProtocolLabBenchmarkAll.ps1 `
  -RunIdPrefix local-all
```

This wrapper runs build, tests, repository check, and the benchmark suite
catalog listed in [docs/benchmarking/suite-catalog.md](suite-catalog.md).

## Suite Catalog

See [docs/benchmarking/suite-catalog.md](suite-catalog.md) for the full list
of benchmark and acceptance suite IDs, target modes, and load tools.

## Publish Completed Runs

To batch publish completed local runs to R2/D1:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publication\Publish-ProtocolLabRuns.ps1 `
  -RunsRoot .artifacts\runs `
  -PrefixFilter local-workflow `
  -VerifyPublishedRuns
```

To publish one or more explicit run IDs:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publication\Publish-ProtocolLabRuns.ps1 `
  -RunsRoot .artifacts\runs `
  -RunIds local-quic-comparison-cache-safe `
  -VerifyPublishedRuns
```

The batch publisher writes:

- `.artifacts\runs\publication-summary.md`
- `.artifacts\publication\<runId>\evidence-report-v1.json`
- `.artifacts\publication\<runId>\evidence-report-v1.md`

## Publish A Single Run Manually

If you need to restage or inspect a single completed run without uploading it,
use the CLI directly:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- publish-report --run .artifacts\runs\{runId} --output .artifacts\publication\{runId} --visibility public --dry-run
```

For the Cloudflare handoff, use:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publication\Publish-ProtocolLabReport.ps1 `
  -RunRoot .artifacts\runs\{runId} `
  -AllowDiagnosticPublication `
  -VerifyPublishedRuns
```

## Output Layout

Completed benchmark runs live under:

```text
.artifacts/runs/{runId}
```

Published bundles live under:

```text
.artifacts/publication/{runId}
```

The key report files are:

- `summary.md`
- `run.json`
- `aggregate-results.json`
- `evidence-report-v1.json`
- `evidence-report-v1.md`
