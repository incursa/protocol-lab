# ProtocolLab Schemas

Schemas define public ProtocolLab document contracts. JSON Schema is the
default for JSON documents. OpenAPI or YAML contracts may be added for HTTP
control-plane surfaces when needed.

## Organization

- [`adapter/v1/`](adapter/v1/) - Adapter Contract v1 control-plane payloads.
- [`artifact/v1/`](artifact/v1/) - artifact references, artifact bundles, and redaction state.
- [`load-profile/`](load-profile/) - declarative load profile documents, with
  protocol-specific intensity settings such as `http1`, `http2`, `http3`, and
  `quic`.
- [`measurement/v1/`](measurement/v1/) - measurement profiles, telemetry bundles, samples,
  summaries, collector descriptors, and comparability classes.
- [`package/v2/`](package/v2/) - component package manifests.
- [`public-report/v1/`](public-report/v1/) - evidence reports, publication manifests, report
  indexes, and artifact indexes.
- [`run-plan/v1/`](run-plan/v1/) - immutable run-plan selection documents.
- [`suite/v1/`](suite/v1/) - declarative suite selection documents.
- [`test-executor/v1/`](test-executor/v1/) - Test Executor Contract v1 control-plane payloads.
- [`validation/v1/`](validation/v1/) - optional per-cell structured validation
  check outcomes used as inputs to specification coverage evidence.
- [`scenario.schema.json`](scenario.schema.json) - scenario definition
  documents for public families such as `http1.*`, `http2.*`, `http3.*`, and
  `quic.*`.
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
