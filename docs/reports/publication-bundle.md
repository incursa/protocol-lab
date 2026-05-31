# Public Report Publication Bundle

A publication bundle is a public-safe derivative of a completed ProtocolLab
run. It is prepared from `.artifacts/runs/{runId}` by the `publish-report`
command and is the staging input for the Cloudflare handoff script. The
bundle is not canonical source material and does not replace the completed run
artifacts.

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

To publish the staged bundle into R2 and D1, use
`scripts/publication/Publish-ProtocolLabReport.ps1`. The script expects the
bundle to live under `.artifacts/publication/{runId}` and uploads it to
`public/runs/{runId}/` through the R2 S3-compatible API.

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

- `evidence-report-v1.json` is the sanitized public report JSON.
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

## Dry Run

`--dry-run` validates the input run, builds the publication plan, and scans
the generated output content for forbidden paths or secret patterns without
writing the bundle to disk.

Use dry-run before any upload step or PR-based publication handoff.
