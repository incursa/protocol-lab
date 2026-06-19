---
title: "Benchmark And Experiment Workflow"
---

# Benchmark And Experiment Workflow

ProtocolLab defines the public contract workflow for benchmark and experiment
evidence. It does not define a local launcher, hosted controller, worker, load
generator, or implementation runtime.

## Configure

Configuration starts with public, declarative inputs:

- scenarios define protocol behavior and validation expectations
- suites group scenarios for a purpose such as smoke, conformance,
  regression, diagnostic, benchmark, or soak evidence
- load profiles describe requested load intent and measurement shape
- packages describe implementation, test-executor, scenario-pack, or
  toolchain metadata
- run plans pin package identities, hashes, selected implementations, selected
  test executors, suites or scenarios, protocols, target mode, network mode,
  and load profile

The public repository owns those document shapes and examples. It does not
choose private package stores, launch processes, allocate workers, bind
endpoints, or collect runtime diagnostics.

## Execute

Execution belongs to a runner, hosted lab, or other implementation-owned
system. That system consumes the public contracts, resolves selected packages,
prepares targets, applies the selected scenarios and suites, and records
unsupported or unavailable outcomes explicitly.

An implementation must not silently substitute another implementation, test
executor, package, protocol lane, scenario, suite, or load profile when the
selected work is incompatible or unavailable.

## Collect

Evidence should be reported through the public measurement, telemetry,
artifact, and public-report contracts:

- measurement samples and summaries carry producer, source, scope, collector,
  provenance, units, tags, warnings, and errors
- telemetry bundles normalize post-run evidence without making a telemetry
  backend canonical
- artifact bundles reference raw or derived artifacts by hash, redaction
  state, sensitivity, retention class, and provenance
- public reports preserve claim level, comparability, unsupported outcomes,
  unavailable outcomes, and publication safety

Implementation-side telemetry is auxiliary unless the selected run plan
explicitly requires it. Telemetry must not upgrade a functional result into a
stronger benchmark or conformance claim after the fact.

## Interpret

Interpret results through the declared measurement profile and provenance:

- `smoke` evidence is primarily functional and may be minimal
- `diagnostic` evidence may include high-overhead instrumentation and raw
  artifacts
- `regression` evidence supports trend comparison when environment, profile,
  scenario, and load shape remain consistent
- `benchmark` evidence requires low-overhead measurement, explicit
  provenance, requested and effective load shape, and comparability warnings
- `soak` evidence focuses on long-duration stability, degradation, memory, and
  error trends

Public reports must not claim stronger comparability than the measurement
profile, environment evidence, provenance, and load-shape evidence support.

## Publish

Publication is a consumer workflow, not a runtime feature of this repository.
Publication-safe bundles and reports should preserve source provenance and
redaction state. Public sites or archive importers should validate the public
schemas and treat private lab state as implementation-owned evidence unless it
is represented by the public contracts.
