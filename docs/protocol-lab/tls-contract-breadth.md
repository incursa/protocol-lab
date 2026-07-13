# TLS Contract Breadth

This contract expansion keeps TLS variation bounded through reusable profile
identities. A profile fixes protocol version, cipher suite, group, signature,
peer authentication, certificate shape, and required comparison evidence. A
scenario fixes lifecycle and measured-window behavior. Load profiles apply
scenario-neutral operation pressure.

## Cryptographic and Authentication Profiles

- `plab-tls13-aes128gcm-p256-server-auth-v2` is the TLS 1.3 AES-GCM baseline.
- `plab-tls13-chacha20-p256-server-auth-v2` changes only the TLS 1.3 cipher.
- `plab-tls12-aes128gcm-p256-server-auth-v2` is compatibility-only.
- `plab-tls13-aes128gcm-p256-mutual-auth-v2` requires both server and client
  certificate proof.

The profile IDs are comparison keys. Results from different profile IDs are
never members of the same comparison group.

## Security-Sensitive Lifecycles

Accepted and rejected 0-RTT use the same replay-safe deterministic 1 KiB
client-to-server payload. Acceptance and rejection are separate expected
outcomes. Rejection succeeds only when the application operation is performed
exactly once after the handshake; duplicated effects fail validation.

KeyUpdate is diagnostic-only. It requests one client-initiated update, requires
proof that traffic secrets changed without publishing secret material, and
requires deterministic post-update traffic to complete.

## Record Coverage

`plab-tls-record-coverage-v1` defines six cases: 1 KiB, 64 KiB, and 1 MiB in
both client-to-server and server-to-client directions. Sizes are plaintext
application-data totals. The contract does not prescribe TLS record
segmentation, socket write boundaries, or implementation buffering.

TLS 1.2 compatibility, expected-rejection 0-RTT, KeyUpdate, and record coverage
are non-publishable diagnostic cells. Existing TLS 1.3 comparison scenarios
remain separate until executor support and evidence schemas cover the expanded
fields.
