# Public Report Safety

The publication workflow is designed to keep public bundles safe by default.
It should fail closed when the input run looks private, malformed, or too
ambitious for its execution profile.

## Safety Checks

The workflow validates:

- `evidence-report-v1.json` or `evidence-report.json`
- the aggregate run report
- optional `run.json` consistency, when present
- claim level versus execution profile
- `publishable=false` load profiles
- secret and private-path markers in generated output
- allowed artifact names and maximum copied artifact size

## Private Content That Must Not Leak

The workflow rejects obvious private markers, including:

- `C:\shared`
- `C:\src`
- `protocol-lab-internal`
- private checkout markers or equivalent local-only paths
- secrets, tokens, passwords, private keys, and similar obvious patterns

If any generated bundle file or copied artifact still contains one of these
markers, publication fails.

## Claim-Level Rules

- `DiagnosticOnly` requires `--allow-diagnostic-publication`.
- `Validation`, `Regression`, `Benchmark`, and `Verified` are surfaced
  explicitly from the report data.
- `Benchmark` and `Verified` remain gated by execution profile and
  publishability rules.
- `Verified` is never implied by the publication workflow.

## Artifact Rules

By default the bundle excludes:

- `.git`
- `bin`
- `obj`
- raw internal analysis outputs
- private URLs and machine-specific configuration
- packet captures, qlogs, and SSL key logs unless they are explicitly
  public-safe and selected
- oversized raw artifacts unless they are explicitly allowed

Missing optional artifacts are listed in `publication-skipped.md` rather than
treated as failures.

## Dry-Run Guidance

Always run the publication workflow with `--dry-run` first. Dry-run validates
the run, applies the safety checks, and reports the bundle plan without
writing output files.

Actual upload to R2 or another store must remain a separate, explicit step.

