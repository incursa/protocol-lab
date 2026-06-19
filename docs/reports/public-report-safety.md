---
title: "Public Report Safety"
---

# Public Report Safety

Public report safety is a contract requirement. It is not tied to any specific
runner, upload utility, hosted lab, or storage provider.

Public report producers must keep the following out of public bundles:

- credentials and tokens
- private workspace paths
- internal hostnames and service URLs
- private repository references
- raw diagnostics that contain sensitive host or process data
- artifacts that are not explicitly public-safe

Public reports must preserve:

- validation failures
- unsupported and unavailable outcomes
- diagnostic-only labels
- non-publishable claim status
- measurement profile, provenance, and comparability limits
- package, scenario, suite, implementation, executor, and load-profile
  provenance

`Verified` and stronger benchmark claims require the matching provenance and
attestation. A public report must not infer those claims from throughput,
duration, implementation identity, or private lab knowledge.

Implementation-provided telemetry can improve diagnostic value, but it is
auxiliary evidence unless a run plan explicitly requires it. Runner-observed
request results remain the canonical benchmark timing evidence unless a
specific test-executor contract says otherwise.
