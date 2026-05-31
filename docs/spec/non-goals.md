# Non-Goals

## Phase 0 Non-Goals

- No implementation code.
- No solution or project files.
- No generated benchmark results.
- No validation execution.
- No server implementation.
- No YAML scenario or manifest files.
- No custom SpecTrace schema import.

## Phase 1 Non-Goals

- No raw QUIC benchmark execution.
- No custom QUIC load generator.
- No custom H3 load generator.
- No direct Incursa protocol-code dependency.
- No nginx, Caddy, or quic-go implementation work beyond placeholders during Phase 1.
- No WebTransport implementation.
- No MASQUE implementation.
- No database benchmarks.
- No full network simulation.
- No broad integration test suite.
- No fabricated benchmark numbers.

## Project-Level Non-Goals Until Revisited

- Replacing interop test suites.
- Declaring one implementation universally faster without comparable validation, load, host, and artifact evidence.
- Hiding implementation-specific optimizations in the neutral runner.
- Treating parser success as a prerequisite for preserving raw benchmark artifacts.
- Requiring every implementation to expose every optional protocol metric.
- Treating optional Caddy local Docker evidence as publishable benchmark data.
