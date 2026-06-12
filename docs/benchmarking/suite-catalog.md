# ProtocolLab Suite Catalog

This catalog lists the suite files that are used by the local scripts in this
repository or selected by package-backed run submissions.

## Package-Backed Selector Suites

These suites select scenarios and load profiles without pinning implementation
or test executor packages.

| Suite ID | Protocol | Target mode | Load tool | Purpose | Runner |
| --- | --- | --- | --- | --- | --- |
| `http1-core-smoke` | `h1` | package-backed or local override | selected test executor | Fast package-neutral HTTP/1 smoke validation for plaintext, JSON, and 1KB payload scenarios. | package/controller submission or explicit local command options |

## Benchmark Suites

Use `scripts/benchmarking/Invoke-ProtocolLabBenchmarkSet.ps1` for a selected
subset, or `scripts/benchmarking/Invoke-ProtocolLabBenchmarkAll.ps1` for the
full benchmark suite catalog. The benchmark wrappers expose `Quick`,
`Regression`, and `Comparison` profiles:

- `Quick` runs the smallest public-report artifact proof.
- `Regression` uses the local-regression load shape for selected suites.
- `Comparison` preserves the full local-comparison behavior and is expected to
  take longer.

| Suite ID | Protocol | Target mode | Load tool | Purpose | Runner |
| --- | --- | --- | --- | --- | --- |
| `ci-public-report` | `h3` | process | `managed-httpclient-h3-load` | Small local regression bundle used by the public-report workflow. | `Invoke-ProtocolLabBenchmarkSet.ps1` |
| `h3-local-v1-comparison` | `h3` | process | `managed-httpclient-h3-load` | Full stable local HTTP/3 comparison coverage across core, payload, headers, and upload scenarios. | `Invoke-ProtocolLabBenchmarkSet.ps1` |
| `quic-transport-v1-comparison` | `quic` | package-backed | `quic-go-raw-load` test executor package | Raw QUIC comparison input. Multiplex and duplex are enabled in the public package contract; implementations enter through package v2/private overlays. | package/controller submission |

## Acceptance Suites

Use `scripts/acceptance/Invoke-ProtocolLabAcceptance.ps1` for these suites.

| Suite ID | Protocol | Target mode | Load tool | Purpose | Runner |
| --- | --- | --- | --- | --- | --- |
| `h3-local-v1` | `h3` | process | `managed-httpclient-h3-load`, `h2load` | Phase 2L local H3 acceptance input. | `Invoke-ProtocolLabAcceptance.ps1` |
| `h3-local-v1-docker-target` | `h3` | docker | `managed-httpclient-h3-load`, `h2load` | Local H3 Docker target acceptance with host-published ports. | `Invoke-ProtocolLabAcceptance.ps1` |
| `h3-local-v1-docker-target-shared-network` | `h3` | docker | `h2load` | Local H3 Docker target acceptance with a generated shared Docker network. | `Invoke-ProtocolLabAcceptance.ps1` |
| `h3-local-v1-docker-target-shared-network-limited` | `h3` | docker | `h2load` | Shared-network Docker target acceptance with example CPU and memory limits. | `Invoke-ProtocolLabAcceptance.ps1` |
| `h3-local-docker-target-caddy` | `h3` | docker | `h2load` | Optional Caddy HTTP/3 Docker target suite. | `Invoke-ProtocolLabAcceptance.ps1` |
| `h3-local-docker-target-nginx` | `h3` | docker | `h2load` | Optional nginx HTTP/3 Docker target suite. | `Invoke-ProtocolLabAcceptance.ps1` |
| `h3-local-docker-target-baselines` | `h3` | docker | `h2load` | Local Docker baseline comparison suite for Kestrel, Caddy, and nginx public reference targets. | `Invoke-ProtocolLabAcceptance.ps1` |

## Notes

- The benchmark suites are local-regression and local-comparison evidence
  paths. They are not publishable benchmark claims by themselves.
- Raw QUIC suites are package-backed; use package v2/controller submission
  rather than the runner-only benchmark wrapper.
- The acceptance suites are documented inputs for the acceptance workflow.
- Docker behavior is encoded in the suite metadata and acceptance runner
  options, so you do not need to guess which environment to use.
