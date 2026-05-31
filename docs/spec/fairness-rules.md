# Fairness Rules

Phase 1 records the fairness contract before broad benchmarking exists. These rules apply to future benchmark acceptance and reporting.

- Every implementation must pass validation before benchmark data is accepted.
- Unsupported scenarios must be marked unsupported with a reason.
- Benchmark endpoints must return equivalent status codes, headers, and bodies.
- Full wire responses must not be pre-rendered as one giant static byte array unless the scenario explicitly allows static wire response testing.
- Static response bodies may be reused only when the scenario permits it.
- JSON scenarios must serialize JSON per request unless the scenario is explicitly marked cached or static.
- Compression is disabled unless explicitly being tested.
- TLS is enabled for HTTP/2 and HTTP/3 unless the scenario explicitly states otherwise.
- HTTP/3 results must include explicit negotiated-protocol proof. HTTP/1.1 or
  HTTP/2 fallback must fail HTTP/3 validation even when the endpoint response
  body is correct.
- Local HTTP/3 proof depends on platform MsQuic support, an HTTPS certificate,
  and a proof client such as curl `--http3-only` or managed .NET
  `HttpClient` with exact HTTP/3 version policy.
- HTTP/3 validation proof is not benchmark evidence. H3 benchmark metrics
  require an H3-capable load generator such as an `h2load` build with `--h3`
  or the managed local-lab `managed-httpclient-h3-load` tool.
- `h2load --h3` is the preferred external-reference H3 benchmark path when the
  installed build proves support for `--h3`.
- Docker-provisioned `h2load --h3` is acceptable only when the container image
  is known, runnable, and its help output proves `--h3`; Docker availability by
  itself is not enough.
- The repo-owned h2load image must be built from explicit source tags or other
  pinned inputs, and `check` must still prove `--h3` from the image rather than
  trusting the tag name.
- Docker load tools that reach local host-process targets must record any
  `host.docker.internal` URL rewrite, SNI value, certificate mode, and effective
  benchmark URL.
- Docker target execution must record target image, container name, Docker
  command, inspect output, network mode, published/internal ports, and local
  container evidence warnings.
- Shared Docker network target execution must record generated network name,
  network id when available, aliases, h2load `--connect-to` routing, SNI,
  cleanup status, and cleanup warnings. Host-published ports may still be used
  for managed exact H3 proof validation.
- Docker target H3 readiness must not rely on TCP-only checks. The target must
  still pass exact H3 validation before any benchmark data is accepted.
- Optional Caddy HTTP/3 results must follow the same validation-first,
  shared-network, certificate/SNI, resource-control, and metrics rules as
  Kestrel and Incursa. Caddy being a production-style server does not make
  local Docker results publishable.
- Optional nginx HTTP/3 results must also prove the selected nginx build has
  HTTP/3 module support, such as `nginx -V` output containing
  `--with-http_v3_module`, before exact H3 validation or benchmarking is
  accepted. A running nginx container is not enough.
- A Docker target does not make a local run publishable. Host-published ports,
  shared Docker networks, local image tags, missing resource limits, missing
  CPU isolation, missing memory limits, shared-host Docker networking, and
  local certificate mode are evidence warnings.
- Optional Docker CPU and memory limits for target and load-tool containers
  may reduce local variance, but they remain local Docker controls. They must
  be recorded as requested/effective settings and must not upgrade evidence to
  publishable.
- Docker load-generator metrics may replace missing CPU-capture warnings only
  when raw Docker stats samples were actually captured from the load-tool
  container during the benchmark. Missing, partial, or single-sample telemetry
  must remain visible as warnings.
- Docker target-container metrics may replace broad unresolved target-resource
  warnings only when raw Docker stats samples were actually captured from the
  target container during benchmark load. They are container-level Docker
  diagnostics, not .NET runtime counters and not publishable evidence.
- `managed-httpclient-h3-load` results are valid local lab measurements, but
  must be reported as `managed-lab` rather than external-reference results.
- The managed proof client and the managed load generator are separate; proof
  success alone never creates benchmark metrics.
- Benchmark evidence must record an evidence class and comparability status.
  `managed-lab` and `external-reference-local` runs must stay separate when
  comparing or ranking results.
- Runs must include warmup.
- Runs should support repetitions. A single repetition is a smoke sample, not a stable benchmark.
- Publishable comparisons should use at least three repetitions and report median, best, and worst values.
- Reports should show median, best, and worst where repetitions exist.
- Results must record both requested load shape and effective load shape.
- HTTP/1.1 results must not imply multiplexed streams. Any HTTP/1.1 `streamsPerConnection` request is not applicable and must be warned about.
- Load-tool command lines, modes, parser IDs, and versions must be recorded where practical.
- Docker load-tool command lines and container exit codes must be recorded
  where practical.
- Localhost runs and client/server-on-same-host runs must be reported as comparability warnings.
- Load-generator saturation must be detectable or reported as unknown. Possible
  saturation is a diagnostic warning and does not by itself fail a validated
  benchmark run.
- Host rewrite to `host.docker.internal`, single-repetition runs, missing
  target process or container metrics, and missing qlog/protocol-counter review
  are comparability warnings, not publication proof.
- Hardware, OS, Docker version, CPU count, memory limit, and network mode must be captured where practical.
- Missing target CPU or memory metrics must be visible as a warning when benchmark data is otherwise accepted.
- Runtime CPU/allocation/GC counters must be attached only to a resolved
  diagnostic server process. A `dotnet run` wrapper PID must not be reported as
  if it were the server process.
- Counter capture is optional diagnostic evidence. Missing or failed counter
  capture lowers evidence quality but does not by itself invalidate a validated
  benchmark run.
- Results must include errors and timeouts next to throughput.
- Any skipped scenario must explain why.
- Incursa-specific optimizations are allowed only in Incursa implementation containers, not in the neutral runner.
- Incursa HTTP/3 validation through the local process target is a protocol and
  endpoint proof only. It becomes a local performance baseline only when an
  accepted H3-capable load generator runs and preserves raw artifacts.
- Local shared-host results are useful for regression and profiling direction,
  but they are not publishable benchmark evidence.
- ProtocolLab v1 acceptance is a reproducibility and local-regression gate. It
  must not be described as isolated-host or publishable benchmark evidence.
- Bootstrap and acceptance scripts may fail fast on missing required local
  prerequisites, but they must preserve the harness rule that unavailable tools
  produce honest unavailable, unsupported, skipped, or warning output rather
  than invented metrics.
