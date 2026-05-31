# quic-go Placeholder

This directory reserves future quic-go benchmark targets.

The current placeholder covers only the HTTP/3 reservation manifest at
`implementations/quic-go-http3.yaml`. It does not define a runnable server,
does not claim HTTP/3 or raw QUIC support, and does not provide benchmark
evidence. The manifest intentionally has empty protocol, workload-family, and
capability lists so current runner flows classify scenarios as unsupported
instead of producing fake validation or benchmark results.

Before this target can be enabled, a future slice must add:

- the concrete quic-go server or load-generator source and image contract
- TLS and certificate material handling
- endpoint mappings or raw QUIC scenario behavior, depending on the scheduled
  workload family
- readiness behavior
- raw stdout and stderr preservation
- any qlog, SSL key log, or protocol metric export contract that can be
  collected honestly
