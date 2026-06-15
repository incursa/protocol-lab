# Measurement Contract Verification

This Markdown page is supporting documentation. The canonical authored
verification artifact is [`VER-PL-MEASUREMENT-CONTRACT-0001.json`](VER-PL-MEASUREMENT-CONTRACT-0001.json).

## Verification Intent

Measurement and artifact contract verification checks that the public
repository defines shapes and examples without adding runtime collectors,
scripts, generated code, or implementation-specific dependencies.

## Evidence Checks

- Measurement and artifact JSON Schemas exist under [`schemas/measurement/v1/`](../../../schemas/measurement/v1/)
  and [`schemas/artifact/v1/`](../../../schemas/artifact/v1/).
- Valid fixtures cover runner-only smoke telemetry, benchmark summaries,
  implementation-provided custom telemetry, optional external telemetry
  correlation, raw artifact references, and sanitized public report artifacts.
- Invalid fixtures cover missing contract versions, missing metric names,
  missing source/scope/provenance, missing artifact hashes, unsafe redaction
  combinations, undisclosed high-overhead benchmark telemetry, and telemetry
  bundles that try to claim conformance status.
- Adapter and test-executor contracts expose optional telemetry capability
  discovery without requiring telemetry export.
- Repository scans show no source code, scripts, workflows, SDKs, collectors,
  or executable examples were added.
