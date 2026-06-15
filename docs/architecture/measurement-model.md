# Measurement Model

This page is a spec-level architecture note for the public measurement,
telemetry, artifact, redaction, and comparability contracts. Canonical
requirements are authored as SpecTrace JSON under [`specs/requirements/`](../../specs/requirements/).

## Evidence Layers

ProtocolLab separates:

- behavior validation outcomes
- normalized measurement and telemetry bundles
- raw artifact references and artifact bundles
- redaction state and publication safety
- public report claim level and comparability statements

The public repository defines the shape of evidence returned after a run. It
does not define collectors, runtime instrumentation, storage services, upload
workflows, or runner implementation code.

## Measurement Profiles

- `smoke` is primarily functional proof; measurements are minimal and may be
  incomplete.
- `diagnostic` may include high-overhead telemetry and raw artifacts; it must
  not be treated as clean benchmark evidence.
- `regression` is suitable for trend comparison when environment, profile,
  scenario, and load shape are consistent.
- `benchmark` requires low-overhead measurement, explicit provenance,
  requested and effective load shape, and comparability warnings.
- `soak` is for long-duration stability, memory, degradation, and error trend
  evidence.

A measurement profile constrains the claim level a result can support. Heavy
instrumentation can reduce comparability and must be disclosed.

## Telemetry Semantics

Telemetry bundles normalize samples, summaries, collector descriptors,
producer metadata, run binding, warnings, errors, redaction state, and optional
artifact references.

Implementation-side telemetry is auxiliary evidence unless a run plan
explicitly requires it. Missing implementation telemetry does not fail an
otherwise valid conformance run. External correlation fields may be preserved,
but no external telemetry backend or raw trace format is the canonical
ProtocolLab contract.

Runner-observed request results are the canonical evidence for benchmark
timing unless a specific test-executor contract explicitly defines another
timing authority.

## Artifact Semantics

Raw artifacts are preserved by reference and content hash. Artifact manifests
can describe logs, traces, metric exports, process output, profiles, summaries,
reports, raw blobs, or other media without requiring any one artifact kind.

Public evidence must carry redaction state. Artifacts marked public-safe or
sanitized must not also declare that they contain sensitive data.

## Comparability

Comparability classes are evidence statements, not numeric scores:

- `none`
- `same-run`
- `same-profile`
- `same-environment`
- `lab-controlled`
- `public-reference`

A report may not claim stronger comparability than its measurement profile,
provenance, load shape, and environment evidence support.
