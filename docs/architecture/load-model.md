---
title: "Load Model"
---

# Load Model

The public load model defines declarative load intent. It does not define a
repository-owned load generator or wrapper.

## Public Documents

- load profiles describe intensity and repetition shape
- scenarios describe behavior under test
- suites group scenarios for a purpose
- run plans select the package-pinned components that an implementation will
  use

Current public load profiles live under [`../../load-profiles/`](../../load-profiles/).
They are generic profile IDs with protocol-specific intensity settings for
HTTP/1, HTTP/2, HTTP/3, and QUIC where useful. The `http1`, `http2`, `http3`,
and `quic` settings do not create separate behavior semantics; scenarios own
behavior.

## Implementation Responsibilities

An implementation chooses how to translate public load intent into traffic and
measurements. It must preserve the requested load profile, record effective
load shape, and report unsupported or unavailable load behavior explicitly.
