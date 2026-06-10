# quic-go Raw QUIC Test Executor

This is the public reference raw QUIC test executor used by the package v2
`protocol-lab-raw-quic-test-executor` package and local fixture workflows.

It is intentionally stored under `tools/test-executors` rather than a concrete
adapter or implementation tree. Hosted lab workers should consume it through a
package-local binary or source-backed package, not by depending on this checkout
layout.
