# ProtocolLab Overview

ProtocolLab is the public contract repository for protocol measurement work. It
defines the shared documents that runners, hosted labs, adapters, test
executors, implementation packages, report consumers, and archive importers
use to agree on what was selected, what was measured, and what a result may
claim.

ProtocolLab solves a coordination problem: protocol benchmarks and
conformance checks are hard to compare when every lab uses private scenario
names, private load terminology, private report fields, or private evidence
rules. This repository keeps the public vocabulary, schema shapes, examples,
and traceability records in one implementation-neutral place.

## What This Repository Owns

The public repository owns:

- repository-native SpecTrace JSON requirements, architecture, work items, and
  verification records
- JSON Schemas for public contract payloads
- declarative fixtures for valid, invalid, and incompatible examples
- scenario, suite, and load-profile definitions
- package, run-plan, measurement, artifact, and public-report contract docs
- governance and contribution docs for public contract changes

The repository does not own concrete runners, local benchmark launchers,
hosted lab operations, package stores, private diagnostics, cloud publication,
or implementation source code. Those live in implementation repositories that
consume the public contracts.

## Supported Work

ProtocolLab is intended to support:

- protocol conformance and smoke checks
- regression and diagnostic evidence
- benchmark evidence with explicit comparability boundaries
- soak evidence for long-duration stability work
- package-backed selection of implementations, test executors, scenarios, and
  suites
- publication-safe evidence reports and artifact manifests

The current public protocol examples include HTTP/1, HTTP/2, HTTP/3, QUIC
transport, WebTransport, WebSocket, MASQUE, and network profile surfaces where
the corresponding scenario or suite definitions exist.

## Stable And Developing Areas

The contract areas marked `complete` in the
[contract coverage matrix](contracts/coverage-matrix.md) are the current
stable public source of truth for implementation repositories and report
consumers.

Open work is still expected around new contract versions, additional
fixtures, future protocol surfaces, package-index admission policy, and
public-report evolution. Open work should extend or revise the public
contracts explicitly; it should not replace the public contract layer with
implementation-owned behavior.
