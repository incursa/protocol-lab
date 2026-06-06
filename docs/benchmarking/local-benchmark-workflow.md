# Local Benchmark Workflow

This repo keeps benchmark compute out of CI. Use the local scripts below
to run selected benchmark and test sets, then upload completed public bundles
to R2.

## Prerequisites

```powershell
dotnet tool restore
dotnet restore Incursa.ProtocolLab.sln
```

## Run Build, Tests, Check, And Selected Benchmark Suites

Run the local workflow script with the `Regression` profile when you want build,
tests, check, and one or more benchmark suites in a single command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\benchmarking\Invoke-ProtocolLabBenchmarkSet.ps1 `
  -RunBuild `
  -RunTests `
  -RunCheck `
  -WorkflowProfile Regression `
  -Suite ci-public-report,h3-local-v1-comparison,quic-transport-v1-comparison `
  -RunIdPrefix local-workflow
```

For the fastest local artifact proof, use the `Quick` profile:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\benchmarking\Invoke-ProtocolLabBenchmarkSet.ps1 `
  -WorkflowProfile Quick `
  -RunIdPrefix local-quick
```

To run only the full HTTP/3 comparison suite:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\benchmarking\Invoke-ProtocolLabBenchmarkSet.ps1 `
  -WorkflowProfile Comparison `
  -Suite h3-local-v1-comparison `
  -RunIdPrefix local-h3-comparison
```

To run only the raw QUIC comparison suite:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\benchmarking\Invoke-ProtocolLabBenchmarkSet.ps1 `
  -WorkflowProfile Comparison `
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
  -WorkflowProfile Comparison `
  -RunIdPrefix local-all
```

This wrapper runs build, tests, repository check, and the benchmark suite
catalog listed in [docs/benchmarking/suite-catalog.md](suite-catalog.md).

## Suite Catalog

See [docs/benchmarking/suite-catalog.md](suite-catalog.md) for the full list
of benchmark and acceptance suite IDs, target modes, and load tools.

## Upload Completed Runs To R2

To batch upload completed local runs to R2:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publication\Publish-ProtocolLabRuns.ps1 `
  -RunsRoot .artifacts\runs `
  -PrefixFilter local-workflow `
  -VerifyUploadedObjects
```

To upload one or more explicit run IDs:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publication\Publish-ProtocolLabRuns.ps1 `
  -RunsRoot .artifacts\runs `
  -RunIds local-quic-comparison-cache-safe `
  -VerifyUploadedObjects
```

The batch publisher writes:

- `.artifacts\runs\publication-summary.md`
- `.artifacts\publication\<runId>\evidence-report-v1.json`
- `.artifacts\publication\<runId>\evidence-report-v1.md`

## Stage Or Upload A Single Run Manually

If you need to restage or inspect a single completed run without uploading it,
use the CLI directly:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- publish-report --run .artifacts\runs\{runId} --output .artifacts\publication\{runId} --visibility public --dry-run
```

For the R2 upload handoff, use:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publication\Upload-ProtocolLabReportBundle.ps1 `
  -RunRoot .artifacts\runs\{runId} `
  -AllowDiagnosticPublication `
  -VerifyUploadedObjects
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

R2 uploads use the matching run prefix:

```text
public/runs/{runId}/
```

The key report files are:

- `summary.md`
- `run.json`
- `aggregate-results.json`
- `evidence-report-v1.json`
- `evidence-report-v1.md`
