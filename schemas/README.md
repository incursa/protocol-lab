# ProtocolLab Schemas

Schemas define public ProtocolLab document contracts. JSON Schema is the
default for JSON documents. OpenAPI or YAML contracts may be added for HTTP
control-plane surfaces when needed.

## Organization

- [`adapter/v1/`](adapter/v1/) - Adapter Contract v1 control-plane payloads.
- [`artifact/v1/`](artifact/v1/) - artifact references, artifact bundles, and redaction state.
- [`dns/v1/`](dns/v1/) - declarative canonical DNS wire fixtures, transport
  framing, runtime message-ID policy, and response normalization.
- [`dns/v2/`](dns/v2/) - versioned semantic DNS fixtures with DoH GET,
  classic UDP/TCP framing, truncation fallback, and broader response shapes.
- [`grpc/v1/`](grpc/v1/) - language-neutral deterministic gRPC service and message descriptors.
- [`grpc/v2/`](grpc/v2/) - the frozen-v1-compatible gRPC service breadth
  contract for all RPC shapes, expected terminal outcomes, gzip, and metadata.
- [`load-profile/load-profile.schema.json`](load-profile/load-profile.schema.json) -
  the legacy v1 load-profile contract.
- [`load-profile/v2/`](load-profile/v2/) - v2 load profiles, adding typed
  `http2`, `tls`, `grpc`, `dns`, and `websocket` intensity settings to the v1
  contract. HTTP/2 v2 keeps global concurrency, configured per-connection
  stream capacity, operation distribution, and observed topology distinct.
- [`measurement/v1/`](measurement/v1/) - measurement profiles, telemetry bundles, samples,
  summaries, collector descriptors, and comparability classes.
- [`measurement/v2/`](measurement/v2/) - canonical protocol metric definitions and
  normalized, snapshot-bound protocol execution results for typed scenarios.
- [`package/v2/`](package/v2/) - component package manifests.
- [`public-report/v1/`](public-report/v1/) - evidence reports, publication manifests, report
  indexes, and artifact indexes.
- [`run-plan/v1/`](run-plan/v1/) - immutable run-plan selection documents.
- [`run-plan/v2/`](run-plan/v2/) - run plans that additionally pin scenario,
  suite, and resolved load-profile contract snapshots.
- [`suite/v1/`](suite/v1/) - declarative suite selection documents.
- [`test-executor/v1/`](test-executor/v1/) - Test Executor Contract v1 control-plane payloads.
- [`test-executor/v2/`](test-executor/v2/) - self-contained preparation requests
  carrying hash-bound test, scenario, and resolved load-profile documents.
- [`tls/v1/`](tls/v1/) - frozen TLS profile v1 plus the record-coverage
  profile contract.
- [`tls/v2/`](tls/v2/) - TLS 1.2/1.3, cipher, and mutual-authentication
  profile variants introduced without changing profile v1.
- [`validation/v1/`](validation/v1/) - optional per-cell structured validation
  check outcomes used as inputs to specification coverage evidence.
- [`scenario.schema.json`](scenario.schema.json) - the legacy v1 scenario
  contract for documents whose `schemaVersion` is `1.0`.
- [`scenario/v2/`](scenario/v2/) - v2 scenarios, adding typed TLS, gRPC,
  secure DNS, execution-profile, availability, and binding-specific WebSocket
  contracts for documents whose `schemaVersion` is `2.0`.
- [`specification/v1/`](specification/v1/) - specification document and
  requirement identity, reviewed catalogs, check-level scenario mappings,
  named coverage profiles, and hash-pinned run evidence sidecars.

## Schema Policy

Schemas are language-neutral contracts. They define document shape and
validation constraints, not source code, SDKs, collectors, runners, or hosted
lab behavior.

Schemas that declare `$id` use `https://schemas.incursa.com/protocol-lab/`
followed by the repository-relative path under [`schemas/`](./). That keeps schema
identifiers stable without implying an implementation package or hosted
service dependency.

Schema changes should be paired with fixture updates and SpecTrace
relationships when the change affects a normative contract surface.

The repository validator selects scenario and load-profile schemas by each
document's `schemaVersion`. New protocol-family fields on either contract must
be introduced in v2 documents; the v1 schema files remain stable for existing
documents and fixtures.
