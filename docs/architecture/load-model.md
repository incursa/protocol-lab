# Load Model

The public load model defines declarative load intent. It does not define a
repository-owned load generator or wrapper.

## Public Documents

- load profiles describe intensity and repetition shape
- scenarios describe behavior under test
- suites group scenarios for a purpose
- run plans select the package-pinned components that an implementation will
  use

## Implementation Responsibilities

An implementation chooses how to translate public load intent into traffic and
measurements. It must preserve the requested load profile, record effective
load shape, and report unsupported or unavailable load behavior explicitly.
