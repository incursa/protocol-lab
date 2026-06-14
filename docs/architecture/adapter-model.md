# Adapter Model

An adapter is an implementation-owned control plane that lets a runner or
hosted lab manage a target through public contract messages.

The public repository owns the Adapter Contract v1 schemas under
`schemas/adapter/v1/` and the supporting documentation in
`docs/architecture/adapter-contract-v1.md`.

Adapters may be implemented in any language or runtime. They must expose the
contracted behavior and preserve explicit unsupported, unavailable, malformed,
timeout, and infrastructure outcomes.
