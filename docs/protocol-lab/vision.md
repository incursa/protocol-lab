# ProtocolLab Vision

ProtocolLab defines public contracts for protocol measurement without tying
those contracts to one language, runner, hosted lab, or implementation stack.

## Purpose

The project gives implementers a shared vocabulary for:

- protocol test cases and scenarios
- suites and load profiles
- implementation and test-executor package metadata
- run-plan selection and provenance
- measurement and artifact contracts
- public report safety and claim boundaries

The public repository is the neutral contract source. Concrete implementations
consume it.

## Principles

- Contracts before implementations.
- Explicit unsupported and unavailable outcomes.
- Raw QUIC and managed HTTP/3 remain separate lanes.
- Public reports do not claim stronger evidence than their provenance permits.
- Internal and third-party runners are implementations, not the public source
  of truth.
- Canonical requirements, architecture, work items, and verification records
  use SpecTrace JSON.

## Public Layer

The public layer contains schemas, fixtures, scenarios, suites, load profiles,
SpecTrace artifacts, and documentation. It intentionally excludes executable
source code, runnable automation, hosted lab operations, package publication,
and implementation-specific tooling.

## Implementation Layer

The implementation layer may include runners, adapters, test executors,
package stores, worker orchestration, hosted controllers, dashboards,
diagnostics, and retained artifacts. An internal or third-party runner is one
implementation of the public contracts.
