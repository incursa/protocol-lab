# Public Report Publication Bundle

A publication bundle is a public-safe derivative of a completed ProtocolLab
run. Benchmark `run` stages it automatically after completed run artifacts are
written. The `publish-report` command can also restage a bundle from
`.artifacts/runs/{runId}` and is the staging input for the Cloudflare handoff
script. The bundle is not canonical source material and does not replace the
completed run artifacts.

## What It Consumes

The bundle workflow reads:

- `aggregate-results.json`
- `evidence-report-v1.json` or the legacy `evidence-report.json`
- `run.json`, when present, to cross-check the run ID and metadata
- the run's artifact index from `EvidenceReport.ArtifactIndex`

## Command Shape

```powershell
protocol-lab publish-report `
  --run .artifacts/runs/{runId} `
  --output .artifacts/publication/{runId} `
  --visibility public `
  --dry-run
```

Use `--allow-diagnostic-publication` only when the bundle is intentionally
diagnostic-only and that label should remain visible in the output.

## Local Generation

Every benchmark `run` writes completed run artifacts under `.artifacts/runs`
and stages the schema-validated public bundle under `.artifacts/publication`
by default, or under the custom path supplied with `--publication-output`.

Use `scripts/publication/New-ProtocolLabPublicReportBundle.ps1` when you want a
one-command local smoke run with the standard public-report defaults:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publication\New-ProtocolLabPublicReportBundle.ps1
```

The default command runs a short local Kestrel HTTP/3 smoke run with the
managed load generator. The benchmark run itself writes the completed run under
`.artifacts/runs` and stages the schema-validated public bundle under
`.artifacts/publication`.

To stage the public bundle from an existing completed run without rerunning the
benchmark:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publication\New-ProtocolLabPublicReportBundle.ps1 `
  -RunRoot .artifacts\runs\{runId}
```

The script does not upload. It allows diagnostic-only local bundles by default
so local failures still produce inspectable public-safe artifacts with
`DiagnosticOnly` preserved. Use `-RequirePublishable` when local generation
should fail instead of staging a diagnostic-only bundle.

To upload the staged bundle to R2, use
`scripts/publication/Upload-ProtocolLabReportBundle.ps1`. The script expects
the bundle to live under `.artifacts/publication/{runId}` unless `-BundleRoot`
points elsewhere, and uploads it to `public/runs/{runId}/` through the R2
S3-compatible API. The downstream site owns processing, indexing, and latest
selection after the run-prefix objects exist in R2.

## Bundle Layout

```text
.artifacts/publication/{runId}/
  evidence-report-v1.json
  evidence-report-v1.md
  artifacts-index.json
  publication-manifest.json
  publication-warnings.md
  publication-skipped.md
  report-index-entry.json
  report-index.json
  artifacts/
    {cellKey}/
      ...
```

### Included Files

- `evidence-report-v1.json` is the canonical sanitized public report JSON.
  It uses schema version `protocol-lab.evidence-report.v1` and validates
  against `schemas/public-report/v1/evidence-report-v1.schema.json`.
- `evidence-report-v1.md` is the Markdown rendering of the sanitized report.
- `artifacts-index.json` describes the public artifact references.
- `publication-manifest.json` records run metadata, claim level, counts, and
  public labels.
- `publication-warnings.md` summarizes warnings and safety notes.
- `publication-skipped.md` lists skipped artifacts and reasons.
- `report-index-entry.json` is the per-run registry entry for the public site.
- `report-index.json` is the local one-entry registry used for static or
  staged publication.

## Artifact Selection

The workflow copies only public-safe artifacts by default. The selection is
allowlisted and intentionally narrow. Optional or oversized artifacts are
recorded in `publication-skipped.md` rather than silently omitted.

The bundle keeps report semantics driven by Evidence Report v1 JSON. It does
not invent new claim levels, report summaries, or validation outcomes.

Supporting JSON files are metadata and pointers. They must not redefine
validation status, benchmark acceptance, claim level, or measurement meaning
independently of `evidence-report-v1.json`.

## Public Report Schema

The public report schema set lives under `schemas/public-report/v1/`:

- `evidence-report-v1.schema.json`
- `artifacts-index.schema.json`
- `publication-manifest.schema.json`
- `report-index-entry.schema.json`
- `report-index.schema.json`

`evidence-report-v1.json` is the semantic source for a published run. It
contains run identity, matrix coverage, validation summary, benchmark
acceptance, per-cell validation and benchmark status, generic measurements,
warnings, errors, and public artifact references.

Measurements use a common shape: `name`, `category`, `unit`, `source`,
`value`, and optional `statistic` and `higherIsBetter`. This lets the same
format carry HTTP throughput, QUIC transport counters, validation quantities,
latency distributions, memory, CPU, network, diagnostic, and future
protocol-specific values without creating protocol-specific top-level report
contracts.

## Dry Run

`--dry-run` validates the input run, builds the publication plan, and scans
the generated output content for forbidden paths or secret patterns without
writing the bundle to disk.

Use dry-run before any upload step or PR-based publication handoff.
