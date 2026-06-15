# ProtocolLab Measurement Requirements

This Markdown page is supporting documentation. The canonical authored
measurement requirements are the SpecTrace JSON artifacts in this directory.

## Scope

The measurement contract defines normalized evidence that can be returned after
a ProtocolLab run. It does not define how measurements are collected, where
telemetry is stored, or which instrumentation system an implementation uses.

## Measurement Profiles

ProtocolLab defines these profile identifiers:

| Profile | Contract Meaning |
| --- | --- |
| `smoke` | Primarily functional proof. Measurements are minimal and MAY be incomplete. |
| `diagnostic` | MAY include high-overhead telemetry and raw artifacts. It MUST NOT be treated as clean benchmark evidence. |
| `regression` | Suitable for trend comparison when environment, scenario, profile, and load shape are consistent. |
| `benchmark` | Requires low-overhead measurement, explicit provenance, requested and effective load shape, and comparability warnings. |
| `soak` | Long-duration stability, memory, degradation, and error-trend evidence. |

A measurement profile MUST constrain the claim level a result can support. Heavy
instrumentation can reduce comparability and MUST be disclosed.

## Normative Rules

- `PLAB-MEASURE-001`: ProtocolLab telemetry bundles MUST use the normalized
  public bundle shape and MUST NOT require any specific telemetry backend.
- `PLAB-MEASURE-002`: Measurement profiles MUST be one of `smoke`,
  `diagnostic`, `regression`, `benchmark`, or `soak`.
- `PLAB-MEASURE-003`: Runner-observed request results are the canonical
  benchmark timing evidence unless a specific test-executor contract explicitly
  defines another timing authority.
- `PLAB-MEASURE-004`: Implementation-provided telemetry MAY be included as
  auxiliary evidence, but missing implementation telemetry MUST NOT fail an
  otherwise valid conformance run unless the run plan explicitly requires it.
- `PLAB-MEASURE-005`: Benchmark telemetry MUST disclose requested load shape,
  effective load shape when available, provenance, and comparability warnings
  when overhead or environment uncertainty affects comparison.
- `PLAB-MEASURE-006`: Diagnostic telemetry MAY include high-overhead
  instrumentation and raw artifact references, but diagnostic evidence MUST NOT
  be promoted to benchmark evidence without disclosure.
- `PLAB-MEASURE-007`: Comparability classes MUST be treated as evidence
  statements, not numeric scores.
- `PLAB-MEASURE-008`: Telemetry bundles MUST distinguish producer, source, and
  scope so runner, implementation, test-executor, load-generator, environment,
  protocol, and artifact observations remain separable.
- `PLAB-MEASURE-009`: External correlation identifiers MAY be preserved, but
  ProtocolLab MUST NOT make OpenTelemetry, Prometheus, qlog, pcap, EventPipe,
  JSON logs, or binary traces canonical field models.
- `PLAB-MEASURE-010`: A telemetry bundle MUST NOT claim or revise
  conformance pass/fail status after the fact.
