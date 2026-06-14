# Terminology And Policies

This page defines shared public-repository terminology and lightweight policy
rules for ProtocolLab contract authors.

## Terminology

| Term | Meaning |
| --- | --- |
| ProtocolLab | The public contract system and related implementations. Use this spelling and casing. |
| Public repository | This language-neutral contract repository. It owns specs, schemas, fixtures, scenarios, suites, load profiles, and docs. |
| Internal lab | An implementation-owned lab environment that may run benchmarks, retain private artifacts, collect diagnostics, publish reports, and operate dashboards outside this public repository. |
| Implementation repository | A separate repository that may provide runners, hosted labs, adapters, test executors, packages, collectors, provider integrations, or operations. |
| Runner | An implementation-owned participant that plans work, coordinates execution, and produces contract-compliant evidence. It is not defined as source code in this repository. |
| Implementation | A protocol participant under test or a package that describes one. |
| Adapter | An implementation-owned control plane that prepares, starts, observes, and disposes an implementation endpoint through the Adapter Contract. |
| Test executor | A participant that performs selected checks against prepared endpoints through the Test Executor Contract. |
| Package | A versioned public manifest that describes implementation, test-executor, scenario-pack, or toolchain metadata and package-relative catalog entries. |
| Scenario | A durable protocol test-case definition. It describes behavior and validation expectations without selecting packages or execution mechanics. |
| Suite | A named selection of scenarios for a purpose such as conformance, regression, benchmark, diagnostic, or soak evidence. |
| Load profile | A declarative load intent and measurement-shape contract. It does not name a required load generator. |
| Load generator | A participant that produces traffic or operation pressure according to a load profile. It is implementation-owned and is not defined by this repository. |
| Execution profile | Run-context metadata that describes how a selected run was intended or classified by a runner or hosted lab. It is not a public launch mechanism. |
| Measurement profile | The claim-level profile for evidence: `smoke`, `diagnostic`, `regression`, `benchmark`, or `soak`. |
| Producer | The declared actor or system that emitted a measurement, telemetry bundle, artifact, or report input. Producer kind is contract vocabulary, not a requirement to use a specific runtime or backend. |
| Source | The declared origin of a measurement or artifact observation, including producer kind, identifier, and optional telemetry system. |
| Scope | The contract category a measurement or artifact observation describes, such as runner, implementation, test-executor, load-generator, environment, protocol, artifact, or other. |
| Provenance | Metadata explaining how evidence was observed, declared, derived, estimated, or otherwise produced. |
| Measurement sample | A normalized individual metric observation with metric name, kind, unit, source, scope, collector, timestamp, provenance, tags, and warnings. |
| Measurement summary | A normalized aggregate metric record for evidence such as latency, throughput, requests, errors, timeouts, or other protocol-neutral summary values. |
| Telemetry bundle | A normalized post-run evidence bundle containing samples, summaries, producers, collectors, warnings, errors, redaction state, and optional artifact references. |
| Collector descriptor | Metadata describing an evidence producer or collector, including source, scope, availability, requirement status, configuration summary, overhead risk, and warnings. |
| Comparability class | An evidence statement describing what comparisons a result can support, such as `same-run`, `same-profile`, or `lab-controlled`. It is not a numeric score. |
| Artifact bundle | A hash-addressable manifest of raw or derived evidence references. |
| Artifact reference | A single artifact manifest entry with identity, kind, media type, path or URI, SHA-256 hash, size, producer, redaction state, sensitivity flag, retention class, description, and tags. |
| Evidence report | A schema-versioned report payload that preserves claim level, provenance, and evidence. |
| Public report | A publication-safe report surface derived from public-safe evidence. |
| Publication bundle | A public-safe collection of report, index, manifest, and artifact-reference documents prepared for a consumer such as an archive or public site. |
| Redaction state | The declared review state for an artifact or bundle: `not-reviewed`, `producer-declared-safe`, `sanitized`, `contains-sensitive-data`, `internal-only`, or `unknown`. |
| Conformance | Functional or behavioral evidence that a selected participant satisfies scenario expectations. Conformance is not upgraded by telemetry after the fact. |
| Regression | Evidence intended for trend comparison when environment, profile, and selection remain consistent. |
| Benchmark | Evidence intended for performance comparison with low-overhead measurement, explicit provenance, requested/effective load shape, and comparability warnings. |
| Diagnostic | Evidence that may include high-overhead telemetry or raw artifacts and must not be treated as clean benchmark evidence by itself. |
| Soak | Long-duration stability evidence focused on degradation, memory, durability, and error trends. |

## Versioning Policy

Contract surfaces use explicit schema or contract version fields when the
payload is exchanged independently. Version changes should preserve stable
identifiers when semantics remain compatible and should introduce a new
version when a breaking shape or meaning change is required.

Scenario IDs, suite IDs, package IDs, requirement IDs, and schema paths should
remain stable after publication. Deprecated terms should remain documented
until consumers have a replacement path.

## Compatibility Policy

Compatibility is explicit. A runner or hosted lab may consume public contracts,
but the public repository must not depend on implementation code, private
services, private configuration, or private artifacts.

Unsupported and unavailable outcomes must remain visible. A participant must
not silently substitute another implementation, adapter, test executor,
scenario, suite, protocol lane, package version, load profile, measurement
profile, or artifact contract when a selected item is incompatible.

Comparability is an evidence statement, not a numeric score. Public reports
must not claim stronger comparability than their profile, environment,
provenance, and load-shape evidence support.

## Schema Policy

JSON Schema is the default for JSON documents. OpenAPI or YAML contracts are
allowed for HTTP control-plane surfaces when they add useful contract clarity.

Schemas define public document shape. They must not prescribe implementation
languages, telemetry backends, package runners, local execution mechanics,
generated code, SDKs, or hosted services.

## Fixture Policy

Fixtures under `fixtures/public-contracts/` are declarative contract examples.
They may be valid, invalid, or incompatible. They must not contain source code,
scripts, binaries, generated code, or runnable implementation payloads.

Valid fixtures show accepted shapes. Invalid fixtures show schema failures.
Incompatible fixtures show structurally valid inputs that a contract consumer
must reject or report as unsupported/unavailable because selected components do
not satisfy the requested work.

## SpecTrace Usage Policy

Canonical requirements, architecture, work items, and verification records are
SpecTrace JSON artifacts under `specs/`.

Requirements are the smallest normative unit. Requirement statements should
use stable IDs and approved normative keywords: `MUST`, `MUST NOT`, `SHALL`,
`SHALL NOT`, `SHOULD`, `SHOULD NOT`, and `MAY`.

Trace relationships should use SpecTrace conventions, including `Satisfied By`,
`Implemented By`, `Verified By`, `Derived From`, `Supersedes`,
`Upstream Refs`, and `Related`. Convenience indexes must point back to
SpecTrace artifacts instead of creating a separate traceability system.

## Neutrality Policy

ProtocolLab public contracts may describe optional producer metadata or
artifact kinds, but they must not require a specific programming language,
runtime, package manager, container backend, telemetry backend, diagnostic
format, shell, runner, CLI, SDK, validator, or hosted lab.
