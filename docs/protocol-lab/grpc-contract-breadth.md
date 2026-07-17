---
title: "gRPC over HTTP/2 Contract Breadth"
---

# gRPC over HTTP/2 Contract Breadth

The gRPC family is intentionally limited to TLS-protected gRPC over exact
HTTP/2 with ALPN `h2`. gRPC-Web and gRPC over HTTP/3 are not fallback paths and
remain deferred protocol families.

The canonical service descriptor is
[`schemas/grpc/v2/service-contract.schema.json`](../../schemas/grpc/v2/service-contract.schema.json).
Its valid fixture is
[`fixtures/public-contracts/grpc/v2/valid/echo-service-v2.json`](../../fixtures/public-contracts/grpc/v2/valid/echo-service-v2.json),
with canonical sorted-key compact-JSON SHA-256
`b7b987814f8af5cd4f15c03989b9c309c1c0ec643972ae32668304d71502120f`.
The v1 descriptor remains frozen for existing consumers.

## Protobuf Media-Type Interoperability

The canonical request and response representation remains
`application/grpc+proto`, while every protobuf scenario admits the two
standards-equivalent response values `application/grpc` and
`application/grpc+proto`. Evidence preserves which value the runtime actually
returned. `application/grpc+json`, gRPC-Web media types, and unrelated content
types do not satisfy this protobuf contract.

## Bounded Scenario Families

| Concern | Scenario IDs | Contract boundary |
| --- | --- | --- |
| RPC cardinality | `grpc.h2.unary.echo`, `grpc.h2.server-streaming.echo`, `grpc.h2.client-streaming.echo`, `grpc.h2.bidi-streaming.echo` | Unary is 1:1, server streaming is 1:100, client streaming is 100:1 after half-close, and bidirectional streaming is ordered 100:100. |
| Terminal outcomes | `grpc.h2.trailers-only-status`, `grpc.h2.deadline-exceeded`, `grpc.h2.client-cancellation` | INVALID_ARGUMENT trailers-only, client deadline, and client cancellation are distinct expected diagnostic outcomes. |
| Compression | `grpc.h2.unary.gzip` | The compressed flag and `grpc-encoding: gzip` are required. Validation decompresses and compares the protobuf SHA-256; encoder-specific gzip bytes are recorded but are not a cross-encoder comparison key. |
| Metadata | `grpc.h2.unary.fixed-metadata` | Exact ASCII and decoded binary request, initial-response, and trailing metadata come from `fixed-ascii-and-binary-metadata-v1`. |
| Message boundaries | `grpc.h2.unary.empty`, `grpc.h2.unary.large` | The empty proto3 bytes field is omitted, while the large case carries exactly 1 MiB of `0x4c`. |
| Channel lifecycle | `grpc.h2.unary.echo`, `grpc.h2.unary.echo-new-channel` | Reused-channel RPC latency and channel-establishment-plus-RPC latency are separate measurements and comparison groups. |

## Deterministic Identity-Coded Messages

For identity-coded messages, every scenario records the raw `bytes` field
length and hash, encoded protobuf length and hash, and complete five-byte gRPC
envelope plus protobuf length and hash. The current bounded payloads are:

| Payload | Protobuf bytes | gRPC envelope bytes | Payload generator |
| --- | ---: | ---: | --- |
| Empty | 0 | 5 | Empty proto3 default bytes field |
| 128 bytes | 131 | 136 | `0x47` repeated 128 times |
| 1 KiB | 1027 | 1032 | `0x42` repeated 1024 times |
| 1 MiB | 1048580 | 1048585 | `0x4c` repeated 1048576 times |

Hash comparison is per message. Message counts remain separate sequence
semantics; an implementation must not hash concatenated messages and claim the
per-message check passed.

## Expected Nonzero Statuses

Expected diagnostic statuses are not successful application RPCs, but they
are successful scenario outcomes when every terminal-condition check passes.
Executors must preserve the gRPC status and initiating event rather than
collapsing them into a generic failure:

- trailers-only: status 3 `INVALID_ARGUMENT`, message `plab invalid fixture`,
  no response DATA message;
- deadline: a 50 ms client deadline against a method that remains open for at
  least 250 ms, status 4 `DEADLINE_EXCEEDED`;
- cancellation: cancel 10 ms after `x-plab-ready: 1` initial metadata, status 1
  `CANCELLED` as observed by the client.

Retries, hedging, and wait-for-ready are disabled for all three diagnostics.

## Load And Comparison Boundaries

`grpc-h2-diagnostic` runs one bounded RPC on a reusable pre-established
channel. `grpc-h2-channel-churn` runs ten sequential operations and requires a
new TLS and HTTP/2 channel per operation. Gzip results are not comparable by
compressed byte count unless encoder identity and settings also match. Expected
terminal-status diagnostics and channel-churn diagnostics are non-publishable.
