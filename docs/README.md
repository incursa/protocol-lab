# ProtocolLab Documentation

ProtocolLab is a scenario-driven validation, benchmarking, and diagnostic lab
for modern transport protocols. This documentation set covers the
public/community surface of the project. Hosted, attested, or commercial
extensions live in the sibling internal repository and must not be duplicated
here as if they were public contracts.

## Getting Started

- [Quickstart](quickstart.md) - bootstrap, build, contract validation, and package proof
- [Repository Layout](repository-layout.md) - physical directory conventions

## Project Direction

- [Vision](protocol-lab/vision.md) - project purpose, public/community boundary, and operating principles
- [Roadmap](protocol-lab/roadmap.md) - phased development plan and current implementation status
- [Product Boundaries](protocol-lab/product-boundaries.md) - public/community versus internal boundary
- [First Public Release Checklist](protocol-lab/first-public-release-checklist.md) - public release gate and verification checklist
- [Public Contract Definition of Done](v1-definition-of-done.md) - public contract readiness criteria

## Architecture

- [Overview](architecture/overview.md) - component map, data flow, key decisions
- [Lab Roles](architecture/lab-roles.md) - lab, adapter, test executor, and package boundaries
- [Runner Model](architecture/runner-model.md) - orchestration, lifecycle, validation, execution
- [Scenario Model](architecture/scenario-model.md) - scenarios, workload families, matrix expansion
- [Adapter Model](architecture/adapter-model.md) - adapter control plane contract and lifecycle
- [Test Executor Contract v1](architecture/test-executor-contract-v1.md) - tester/executor control plane API specification
- [Load Model](architecture/load-model.md) - load tools, load shapes, load profiles
- [Artifact Model](architecture/artifact-model.md) - deterministic layout, paths, and preservation
- [Measurement Model](architecture/measurement-model.md) - execution profile, effective load shape, provenance model, comparability
- [Report Model](architecture/report-model.md) - claim levels, publishable gating, environment manifest, report pipeline
- [Architecture](architecture.md) - foundational architecture summary and public contract boundaries
- [Adapter Contract v1](architecture/adapter-contract-v1.md) - full control plane API specification
- [Package v2](lab/package-v2.md) - schema-backed component package contract
- [Public Contract Fixtures](../fixtures/public-contracts/README.md) - neutral package examples for local conformance validation

## Reports

- [Public Report Handoff](reports/publication-handoff.md) - local run to R2 upload path
- [Public Report Publication Bundle](reports/publication-bundle.md) - staged bundle format and command flow
- [Public Report Safety](reports/public-report-safety.md) - safety checks, labels, and exclusion rules

## Decisions

- [ADR Index](decisions/README.md) - architecture decision records

## Runner

- [Runner Overview](runner/overview.md) - runner boundaries and design
- [Fixture Lab](runner/fixture-lab.md) - runner contract fixture lab
- [Adapter Conformance](runner/adapter-conformance.md) - adapter conformance suite
- [Test Executor Conformance](runner/test-executor-conformance.md) - test executor conformance suite
- [Raw QUIC Foundation](runner/raw-quic-foundation.md)

## Scenarios

- [Authoring Guide](scenarios/authoring-guide.md)
- [Catalog](scenarios/catalog.md)
- [Scenario Model](scenarios/scenario-model.md)

## Specifications

- [Requirements Trace](spec/requirements-trace.md) - requirements traceability matrix
- [Validation vs Benchmarking](spec/validation-vs-benchmarking.md) - separation rules
- [Phase Plan](spec/phase-plan.md) - detailed phase breakdown
- [Non-Goals](spec/non-goals.md) - explicit non-goals
- [Open Questions](spec/open-questions.md) - unresolved design questions
- [Fairness Rules](spec/fairness-rules.md) - fairness constraints
- [Load Tools](spec/load-tools.md) - load tool contract
- [Future Workload Families](spec/future-workload-families.md) - WebTransport, MASQUE
- [Database Workloads Decision](spec/database-workloads-decision.md)
- [Docker Target Execution](spec/docker-target-execution.md)
- [Docker Resource Controls](spec/docker-resource-controls.md)
- [Load Generator Metrics](spec/load-generator-metrics.md)
- [Docker Target Metrics](spec/docker-target-metrics.md)
- [Runtime Counter Capture](spec/runtime-counter-capture.md)
- [Kestrel HTTP/3 Proof](spec/kestrel-http3-proof.md)
- [Caddy HTTP/3 Target](spec/caddy-http3-target.md)
- [nginx HTTP/3 Target](spec/nginx-http3-target.md)
- [Raw QUIC Load Generator Contract](spec/raw-quic-load-generator-contract.md)

## Releases

- [v1-local-operational](releases/protocol-lab-v1-local-operational.md) - release notes

## Analysis

Public analysis reports are not published in this repository. Public report
publication bundles are documented separately under [Reports](#reports).
