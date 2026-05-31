# IncursaBenchServer Placeholder

This directory preserves the ProtocolLab-side Incursa handoff notes without
embedding Incursa protocol code in the neutral runner. The runnable Incursa
HTTP/3 target is described by `implementations/incursa-http3.yaml` and is
implemented by the repo-owned adapter project.

Current contract:

- local process image label: `incursa/protocol-lab-incursa-http3-bench-server:local`
- runnable project: `src/Incursa.ProtocolLab.Adapters.IncursaHttp3/Incursa.ProtocolLab.Adapters.IncursaHttp3.csproj`
- supported runner role: `server`
- supported workload family: `http.application`
- supported protocol: `h3`
- day-one scenario contract: the existing HTTP core plaintext and JSON
  scenarios
- reserved artifact export path: standard stdout and stderr only, until the
  image contract is confirmed

This folder deliberately stays doc-only. qlog, SSL key log, and richer
protocol artifact exports are intentionally left for a later phase. That keeps
the handoff honest while still giving Incursa a concrete, neutral integration
point for future packaging and artifact export work.

No runnable server implementation lives in this ProtocolLab folder.

The detailed activation gate and artifact contract are recorded in
`docs/spec/incursa-http3-target-contract.md`.
