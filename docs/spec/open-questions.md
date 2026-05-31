# Open Questions

## Runner and Orchestration

1. Should Phase 1 start targets as local processes only, Docker containers only, or support both behind a small abstraction?
2. What should the default local artifact root be when `--output` is omitted?
3. Should `validate` require an already-running target in Phase 1, or should it own target startup from the manifest?
4. Which CLI library should be used for command parsing?

## Load Tools

1. Is `h2load` expected to be available on the primary development machines?
2. Should `oha` be included in Phase 1 only if it is already installed, or should Phase 1 only model adapter discovery?
3. What is the minimum acceptable behavior when no load tool is present: fail the run command, emit an unavailable result, or both?

## Kestrel Baseline

1. Should Phase 1 Kestrel run HTTP/3 by default, or start with HTTP/1.1 and HTTP/2 while keeping the manifest named `kestrel-http3`?
2. How should development certificates be provisioned for local HTTP/2 and HTTP/3 runs?
3. Should `/json` use `System.Text.Json` minimal API serialization directly, or an explicit per-request serialization path for benchmark fairness?

## Scenario and Manifest Format

1. Should scenario and manifest YAML be schema-validated in Phase 1, or should strong model parsing plus unit tests be enough initially?
2. Should IDs use dots exactly as scenario names, such as `http.core.plaintext`, and file names stay readable?
3. Should network profile be required on every scenario or default to `clean`?

## Incursa Integration

1. What future Incursa image names should manifests reserve?
2. Which artifacts should Incursa images export first: qlog, SSL key logs, protocol counters, or all three?
3. Should Incursa expose custom metrics through files, HTTP endpoints, stdout, or sidecar exporters?

## Reporting

1. What metrics should be mandatory for a result to appear in summary tables?
2. Should failed validation cells appear in the same aggregate JSON as benchmark cells?
3. Should reports group first by implementation, scenario, protocol, or workload family?

## Deferred Workloads

1. What database workload family, if any, should be added after transport and HTTP protocol execution are real?
2. Which dataset, driver, validation, and cleanup contracts would make database workload results comparable instead of anecdotal?
