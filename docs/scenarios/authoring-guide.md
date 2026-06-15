# Scenario Authoring Guide

Scenarios are declarative public test-case documents. They define behavior and
validation expectations without naming a concrete implementation or execution
environment.

## Rules

1. Name scenarios by behavior, not by implementation.
2. Do not include hostnames, ports, private paths, package hashes, package
   versions, implementation IDs, or test-executor IDs.
3. Use stable IDs once a scenario is published.
4. Keep QUIC transport scenarios separate from HTTP/3 application and protocol
   scenarios.
5. Mark future or incomplete surfaces as `placeholder` or `experimental`.
6. Put run selection and package pinning in run plans, not scenarios.

## Minimal Shape

```yaml
schemaVersion: "1.0"
id: http1.core.plaintext
title: HTTP/1 Plaintext
name: HTTP/1 Plaintext
family: http.application
description: Validate a fixed small plaintext HTTP response over HTTP/1.
status: stable
kind: workload
layer: application
protocol: h1
roles:
  - server
requires:
  capabilities:
    - http.server
  protocols:
    - h1
  roles:
    - server
trafficShape: request-response
validation:
  required: true
  checks:
    - status
    - protocol:h1
```

Add protocol-specific request, response, validation, and artifact expectations
when the scenario requires them.
