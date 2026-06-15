# Scenario Authoring Guide

Scenarios are declarative public test-case documents. They define behavior and
validation expectations without naming a concrete implementation or execution
environment.

## Rules

1. Name scenarios by behavior, not by implementation.
2. Do not include hostnames, ports, private paths, package hashes, package
   versions, implementation IDs, or test-executor IDs.
3. Use stable IDs once a scenario is published.
4. Keep raw QUIC transport scenarios separate from managed HTTP/3 scenarios.
5. Mark future or incomplete surfaces as `placeholder` or `experimental`.
6. Put run selection and package pinning in run plans, not scenarios.

## Minimal Shape

```yaml
schemaVersion: protocol-lab.scenario.v1
id: http1.core.plaintext
title: HTTP/1 Plaintext
status: stable
category: http
protocol: h1
version: "1.0"
```

Add protocol-specific request, response, validation, and artifact expectations
when the scenario requires them.
