# Authoring Guide

This guide explains how to create and maintain scenario files in ProtocolLab.

In the public vocabulary, a **test case** is the implementation-neutral
behavior being specified, and a **scenario** is the current YAML artifact that
serializes that test case. See
[Test Case And Run Plan Model](../architecture/test-case-run-plan-model.md)
for the full grammar, including scenario packs, suites, load profiles, and
run plans.

## Authoring rules

1. **Scenarios define behavior, not implementation.** Do not reference adapter names, Docker, hostnames, ports, or local paths in scenario files.

2. **Scenarios do not contain hostnames or ports.** The runner resolves those from implementation manifests and CLI options.

3. **Scenario IDs are stable.** Once published, don't rename scenario IDs. Choose a clear, dotted identifier.

4. **Prefer parameters over duplicate scenario files.** If two scenarios differ only by a numeric value, use a parameter rather than creating two files.

5. **Every benchmarkable scenario needs a validation path.** Without validation, benchmark data is not accepted.

6. **Experimental and placeholder scenarios must be labeled.** Use `status: experimental` or `status: placeholder`. Placeholder scenarios will not run unless explicitly allowed.

7. **Load tools match traffic shape.** The `trafficShape` field helps the runner select compatible load tools.

8. **Protocol-specific proof belongs in validation artifacts.** Put protocol proof details in `validation.http`, `validation.http3`, or `validation.quic`, not in the runner.

9. **Scenarios are not run plans.** Do not put package references,
   implementation IDs, test executor IDs, SHA-256 values, controller
   placement, or repeatable job policy in scenario files.

## Scenario file template

```yaml
schemaVersion: "1.0"
id: http.plaintext
title: HTTP Plaintext
description: Validate and benchmark a small plaintext HTTP response.
status: stable
kind: workload
layer: application
protocol: h3
roles:
  - server
requires:
  capabilities:
    - http.server
  protocols:
    - h1
    - h2
    - h3
  roles:
    - server
trafficShape: request-response

endpoint:
  method: GET
  path: /plaintext
  expectedStatus: 200
  expectedHeaders:
    content-type: text/plain
  expectedBodyRule: exact
  expectedBody: Hello, World!
  expectedBodySize: 13

validation:
  required: true
  checks:
    - status
    - content-type
    - body
  http:
    expectedStatus: 200
    expectedBody: Hello, World!

benchmarkCompat:
  compatibleLoadShapes:
    - fixed-path-request-response
  primaryMetrics:
    - requestsPerSecond
    - latencyP50
    - latencyP95
    - latencyP99
    - errorRate

artifacts:
  required:
    - validation.json
    - result.json
  optional:
    - qlog
    - counters

networkProfile: clean
tags:
  - phase1
  - http
```

## Adding a new scenario

1. Choose the right protocol family directory under `scenarios/`.
2. Create a `.yaml` file with a dotted ID (e.g., `scenarios/http/my-scenario.yaml`).
3. Fill in all required v1 fields: `schemaVersion`, `id`, `title`, `status`, `kind`, `layer`, `protocol`, `roles`, `requires`, `trafficShape`, `validation`.
4. Set `status` to `stable` if the scenario has a working validator and load tool. Use `experimental` or `placeholder` otherwise.
5. Run `protocol-lab check` to validate the new scenario file.
6. Run `protocol-lab list scenarios` to confirm the scenario appears correctly.
7. Add adapter capabilities to existing implementation manifests as needed.

8. Add or update a run plan only when the goal is to pin exact package
   versions and selected IDs for repeatable execution. Do not encode that
   selection in the scenario itself.

## Status meanings

| Status | Can run by default? | Description |
|--------|---------------------|-------------|
| `draft` | Yes (treated as stable) | Under development |
| `stable` | Yes | Validated and benchmarkable |
| `experimental` | No (requires `--allow-experimental`) | Needs explicit opt-in |
| `deprecated` | Yes (with warning) | Scheduled for removal |
| `placeholder` | No (not runnable) | No validator or load tool exists |

## Capability naming

Use implementation-neutral capability names:

- `http.server` — HTTP server capability
- `h3.server` — HTTP/3 server capability
- `quic.server` — QUIC server capability
- `websocket.server` — WebSocket server capability
- `webtransport.server` — WebTransport server capability
- `masque.server` — MASQUE server capability

Capabilities should describe what the protocol provides, not which implementation supplies it.
