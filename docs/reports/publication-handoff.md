# Public Report Handoff

This is the explicit path from a completed local ProtocolLab run to the public
Cloudflare storage layout used by the site.

## Source Of Truth

Start from a completed run under:

```text
.artifacts/runs/{runId}
```

Benchmark `run` creates this run root and stages the matching public bundle
under `.artifacts/publication/{runId}` automatically.

The run root must contain, at minimum:

- `aggregate-results.json`
- `evidence-report-v1.json` or the legacy `evidence-report.json`
- `run.json`, when present
- the per-cell artifact tree under `.artifacts/runs/{runId}/implementations/...`

## Stage The Public Bundle

Benchmark runs stage the sanitized bundle automatically. Use the existing
`publish-report` command when you need to restage a bundle from an existing
completed run:

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
`evidence-report-v1.json` is the canonical public payload for downstream
consumers and validates against
`schemas/public-report/v1/evidence-report-v1.schema.json`.

For local one-command generation, use:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publication\New-ProtocolLabPublicReportBundle.ps1
```

That script runs a short local benchmark whose `run` command writes
`.artifacts/runs/{runId}` and stages `.artifacts/publication/{runId}`. To
stage a bundle from an already-completed run, pass
`-RunRoot .artifacts\runs\{runId}`. The script does not upload to R2 and does
not write D1 metadata.

## Publish To Cloudflare

Use the explicit handoff script to upload the staged bundle and refresh the
search index metadata:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publication\Publish-ProtocolLabReport.ps1 -RunRoot .artifacts\runs\{runId}
```

For production and CI publication, pass `-VerifyPublishedRuns` so the script
re-scans D1 after metadata indexing and fails before the latest pointer is
advanced if any published run is missing its R2 payload set. The prepublish
gate also checks the required run-prefix root objects and every referenced
artifact-index object before registry/latest can advance.

The script performs these steps in order:

1. Validates the source run, report shapes, run IDs, execution profile versus
   claim level, and artifact references.
2. Re-runs `publish-report` to stage the public-safe bundle.
3. Uploads the staged bundle into the `protocol-lab-reports` bucket under:

   ```text
   public/runs/{runId}/
   ```

4. Verifies the uploaded run prefix and required metadata objects before the
   publication can advance.

5. Refreshes these R2 registry objects:

   - `public/registry/report-index.json`
   - `public/registry/latest.json`

6. Writes searchable metadata into D1 through the Cloudflare D1 REST API using
   `PROTOCOL_LAB_DB_ID`.

The script fails closed on malformed JSON, mismatched run IDs, path escapes,
private-path leaks, secret patterns, or missing Cloudflare credentials.

## GitHub Actions Automation

The repository keeps benchmark compute out of the normal CI build. The
benchmark handoff remains available through the dedicated reusable workflow,
but only the nightly/manual path invokes it automatically:

- `.github/workflows/nightly-public-report.yml` calls the reusable publish
  workflow via manual dispatch.
- The reusable workflow runs a canonical `kestrel-http3` H3 regression pass
  with `managed-httpclient-h3-load`, then publishes the resulting completed
  run with `-AllowDiagnosticPublication` so diagnostic runs stay visible
  instead of being hidden.
- For ad hoc local runs, use
  [docs/benchmarking/local-benchmark-workflow.md](../benchmarking/local-benchmark-workflow.md).

The workflow requires these repository secrets:

- `CLOUDFLARE_API_TOKEN`
- `CLOUDFLARE_ACCOUNT_ID`
- `PROTOCOL_LAB_DB_ID`
- `R2_ACCESS_KEY_ID`
- `R2_SECRET_ACCESS_KEY`

The workflow derives the S3 endpoint from `CLOUDFLARE_ACCOUNT_ID` as
`https://<ACCOUNT_ID>.r2.cloudflarestorage.com`. Store a different endpoint
only if Cloudflare tells you the bucket uses a non-default jurisdiction.

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

The handoff workflow only treats the run as published after the run-prefix
objects are verified, the registry objects are refreshed, and D1 has been
updated successfully.

## D1 Metadata

The D1 index stores searchable metadata only. It is a derived, lossy index over
the R2 payload and does not store or replace the full report JSON.

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

The D1 side also stores the object-key mapping rows for each published run in
`public_report_run_object_keys`, and the singleton latest pointer in
`public_report_latest`. Those rows are the public index contract that the
verification step scans against R2.

D1 rows must not be treated as the source of truth for validation outcomes,
benchmark acceptance, claim level, or measurement semantics. If the site needs
full details for a report, it must read
`public/runs/{runId}/evidence-report-v1.json` from R2 and use D1 only to find,
filter, or sort candidate runs.

## Validation Gates

Before any upload or indexing step, the handoff validates:

- Evidence Report JSON shape
- public report JSON schema conformance
- run ID consistency across all source files
- artifact references stay under the run root
- claim-level and execution-profile compatibility
- forbidden paths and secret patterns in the staged bundle
- uploaded R2 objects still exist and parse where applicable
- D1 object-key rows match the required run-prefix payload set before the
  latest pointer is advanced or the run is considered published

Missing optional artifacts remain visible in `publication-skipped.md`.
