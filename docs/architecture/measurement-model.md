---
title: "Measurement Model"
---

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

## Typed Protocol Results

The Protocol Metric Catalog v2 gives every metric required by a typed TLS,
gRPC, secure-DNS, or WebSocket scenario one canonical identity, unit,
aggregation, measured window, and timing authority. An executor must not emit
the right-looking metric name with a different byte scope or percentile rule.

Protocol Execution Result v2 is the normalized per-cell evidence envelope. It
binds run, repetition, scenario, load profile, packages, selected component,
endpoint, requested and observed protocol, no-fallback proof, required check
outcomes, canonical metrics, artifact hashes, and family-specific protocol
facts. An overall pass is valid only when every required check passes exactly
once.

TLS evidence distinguishes TLS 1.2 from TLS 1.3, full and resumed handshakes,
mutual authentication, accepted or rejected early data, KeyUpdate, and the
six-case record-coverage profile. DNS evidence distinguishes secure bindings
from classic UDP/TCP diagnostics and records response counts, truncation, and
whether the one permitted UDP-to-TCP fallback occurred. gRPC evidence binds
the v2 service digest, RPC shape, metadata and compression policies, expected
terminal initiator, message counts, and reused-channel versus fresh-channel
measurement windows; expected nonzero diagnostic statuses remain successful
contract outcomes when their required checks pass.

WebSocket family evidence is binding-specific. HTTP/1 Upgrade results record
the opening-handshake count, zero key reuse, valid 16-byte nonce decoding, zero
`Sec-WebSocket-Accept` computation mismatches, and a sample key/accept pair.
HTTP/2 and HTTP/3 Extended CONNECT results instead prove
`SETTINGS_ENABLE_CONNECT_PROTOCOL = 1`, the required CONNECT pseudo-header
semantics, and zero occurrences of prohibited Upgrade, Connection,
`Sec-WebSocket-Key`, or `Sec-WebSocket-Accept` fields.

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
