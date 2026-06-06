# Public Report R2 Handoff

This is the explicit path from a completed local ProtocolLab run to the public
R2 object layout consumed by the downstream site.

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
`-RunRoot .artifacts\runs\{runId}`. The script does not upload.

## Upload To R2

Use the explicit R2 handoff script to upload the staged bundle:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publication\Upload-ProtocolLabReportBundle.ps1 `
  -RunRoot .artifacts\runs\{runId} `
  -AllowDiagnosticPublication `
  -VerifyUploadedObjects
```

If the bundle is already staged and should not be restaged, upload it directly:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publication\Upload-ProtocolLabReportBundle.ps1 `
  -BundleRoot .artifacts\publication\{runId} `
  -VerifyUploadedObjects
```

The upload script performs these steps in order:

1. Validates the source run and restages the public-safe bundle when `-RunRoot`
   is supplied.
2. Validates required bundle files, run ID consistency, run-prefix object keys,
   and artifact-index references.
3. Builds one upload manifest for all files in the bundle.
4. Uploads the bundle into the `protocol-lab-reports` bucket under:

   ```text
   public/runs/{runId}/
   ```

5. When `-VerifyUploadedObjects` is supplied, verifies uploaded objects exist
   and parses key JSON objects.

The script fails closed on malformed JSON, mismatched run IDs, invalid prefixes,
path escapes, private-path leaks inherited from bundle staging, or missing R2
credentials.

## GitHub Actions Automation

The repository keeps benchmark compute out of the normal CI build. The
benchmark handoff remains available through the dedicated reusable workflow,
but only the nightly/manual path invokes it automatically:

- `.github/workflows/nightly-public-report.yml` calls the reusable publish
  workflow via manual dispatch.
- The reusable workflow runs a canonical quick H3 regression pass with
  `managed-httpclient-h3-load`, then uploads the resulting public bundle with
  `-AllowDiagnosticPublication` so diagnostic runs stay visible instead of
  being hidden.
- For ad hoc local runs, use
  [docs/benchmarking/local-benchmark-workflow.md](../benchmarking/local-benchmark-workflow.md).

The workflow requires these repository secrets:

- `CLOUDFLARE_ACCOUNT_ID`
- `R2_ACCESS_KEY_ID`
- `R2_SECRET_ACCESS_KEY`

The workflow derives the S3 endpoint from `CLOUDFLARE_ACCOUNT_ID` as
`https://<ACCOUNT_ID>.r2.cloudflarestorage.com`. Store a different endpoint
only if Cloudflare tells you the bucket uses a non-default jurisdiction.

## Local R2 Credentials

Do not commit R2 credentials and do not put machine-specific paths in source
control. Local upload scripts resolve credentials in this order:

1. Existing environment variables.
2. A file passed with `-R2CredentialsPath`.
3. A file path in `PROTOCOL_LAB_R2_CREDENTIALS_PATH`.
4. PowerShell SecretManagement secrets.

The direct environment variable contract is:

```text
AWS_ACCESS_KEY_ID=...
AWS_SECRET_ACCESS_KEY=...
AWS_SESSION_TOKEN=...        # optional, for temporary credentials
CLOUDFLARE_ACCOUNT_ID=...    # or R2_ENDPOINT=...
AWS_DEFAULT_REGION=auto
```

For a durable local file outside the repository, set one user environment
variable:

```powershell
[Environment]::SetEnvironmentVariable(
  "PROTOCOL_LAB_R2_CREDENTIALS_PATH",
  "C:\Users\$env:USERNAME\.config\incursa\protocol-lab-r2.env",
  "User")
```

That file may use the S3-compatible names above or the aliases
`R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, and `R2_SESSION_TOKEN`.

For a local secret store, use PowerShell SecretManagement with these default
secret names:

```powershell
Set-Secret -Name ProtocolLab-R2-AccessKeyId -Secret "<access-key-id>"
Set-Secret -Name ProtocolLab-R2-SecretAccessKey -Secret "<secret-access-key>"
Set-Secret -Name ProtocolLab-CloudflareAccountId -Secret "<account-id>"
```

Pass `-R2SecretVault <vault-name>` when the secrets live outside the default
vault. The upload scripts also expose parameters to override the default secret
names.

## Public R2 Layout

The site reads these objects from the run prefix:

- `public/runs/{runId}/evidence-report-v1.json`
- `public/runs/{runId}/evidence-report-v1.md`
- `public/runs/{runId}/artifacts-index.json`
- `public/runs/{runId}/publication-manifest.json`
- `public/runs/{runId}/publication-warnings.md`
- `public/runs/{runId}/publication-skipped.md`
- `public/runs/{runId}/report-index-entry.json`
- `public/runs/{runId}/report-index.json`
- `public/runs/{runId}/artifacts/...`

The downstream site owns any browse index, latest selection, or derived
metadata needed after these run-prefix objects exist in R2.

## Validation Gates

Before any upload step, the handoff validates:

- Evidence Report JSON shape
- public report JSON schema conformance during bundle staging
- run ID consistency across source and bundle files
- artifact references stay under the run root during bundle staging
- claim-level and execution-profile compatibility
- forbidden paths and secret patterns in the staged bundle
- uploaded R2 objects still exist and parse where applicable

Missing optional artifacts remain visible in `publication-skipped.md`.
