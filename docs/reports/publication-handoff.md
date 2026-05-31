# Public Report Handoff

This is the explicit path from a completed local ProtocolLab run to the public
Cloudflare storage layout used by the site.

## Source Of Truth

Start from a completed run under:

```text
.artifacts/runs/{runId}
```

The run root must contain, at minimum:

- `aggregate-results.json`
- `evidence-report-v1.json` or the legacy `evidence-report.json`
- `run.json`, when present
- the per-cell artifact tree under `.artifacts/runs/{runId}/implementations/...`

## Stage The Public Bundle

Use the existing `publish-report` command to create the sanitized bundle:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- publish-report --run .artifacts\runs\{runId} --output .artifacts\publication\{runId} --visibility public
```

The staged bundle is written to:

```text
.artifacts/publication/{runId}
```

The bundle contains:

- `evidence-report-v1.json`
- `evidence-report-v1.md`
- `artifacts-index.json`
- `publication-manifest.json`
- `publication-warnings.md`
- `publication-skipped.md`
- `report-index-entry.json`
- `report-index.json`
- `artifacts/{cellKey}/...`

The bundle preserves the Evidence Report v1 semantics. It does not invent new
claims, suppress `DiagnosticOnly`, or silently hide `publishable=false`.

## Publish To Cloudflare

Use the explicit handoff script to upload the staged bundle and refresh the
search index metadata:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publication\Publish-ProtocolLabReport.ps1 -RunRoot .artifacts\runs\{runId}
```

The script performs these steps in order:

1. Validates the source run, report shapes, run IDs, execution profile versus
   claim level, and artifact references.
2. Re-runs `publish-report` to stage the public-safe bundle.
3. Uploads the staged bundle into the `protocol-lab-reports` bucket under:

   ```text
   public/runs/{runId}/
   ```

4. Refreshes these R2 registry objects:

   - `public/registry/report-index.json`
   - `public/registry/latest.json`

5. Writes searchable metadata into D1 through the `PROTOCOL_LAB_DB` binding.

The script fails closed on malformed JSON, mismatched run IDs, path escapes,
private-path leaks, secret patterns, or missing Cloudflare credentials.

## GitHub Actions Automation

The repository wires the same handoff into GitHub Actions so a fresh public
bundle is published automatically from the main branch and from the nightly
run:

- `.github/workflows/ci.yml` calls the reusable publish workflow after the
  main branch build and test job.
- `.github/workflows/nightly-public-report.yml` calls the same reusable
  workflow on a schedule and via manual dispatch.
- The reusable workflow runs a canonical `kestrel-http3` H3 regression pass
  with `managed-httpclient-h3-load`, then publishes the resulting completed
  run with `-AllowDiagnosticPublication` so diagnostic runs stay visible
  instead of being hidden.

The workflow requires these repository secrets:

- `CLOUDFLARE_API_TOKEN`
- `CLOUDFLARE_ACCOUNT_ID`
- `PROTOCOL_LAB_DB_ID`

## Public R2 Layout

The site reads these objects from the run prefix:

- `public/runs/{runId}/evidence-report-v1.json`
- `public/runs/{runId}/evidence-report-v1.md`
- `public/runs/{runId}/artifacts-index.json`
- `public/runs/{runId}/publication-manifest.json`
- `public/runs/{runId}/publication-warnings.md`
- `public/runs/{runId}/publication-skipped.md`
- `public/runs/{runId}/report-index-entry.json`
- `public/runs/{runId}/artifacts/...`

The registry objects stay separate under `public/registry/`.

## D1 Metadata

The D1 index stores searchable metadata only. It does not store the full report
JSON.

Indexed metadata includes:

- run identity
- generated and published timestamps
- claim level
- publishable flag
- diagnostic-only label
- execution profile
- implementation IDs
- scenario IDs
- protocol IDs
- counts
- warnings
- artifact reference keys
- latest-pointer metadata when applicable

## Validation Gates

Before any upload or indexing step, the handoff validates:

- Evidence Report JSON shape
- run ID consistency across all source files
- artifact references stay under the run root
- claim-level and execution-profile compatibility
- forbidden paths and secret patterns in the staged bundle

Missing optional artifacts remain visible in `publication-skipped.md`.
