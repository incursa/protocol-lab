# Public Contract Fixtures

These fixtures are neutral examples for third-party implementers. They are
not production adapters, not production test executors, and not benchmark
targets.

Use them to inspect the expected package v2 layout and to smoke-test the
public conformance command:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance package --package fixtures\public-contracts\packages\neutral-test-executor
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance package --package fixtures\public-contracts\packages\neutral-adapter-implementation
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance package --package fixtures\public-contracts\packages\neutral-scenario-pack
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance package --package fixtures\public-contracts\packages\http1-core-scenario-pack
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance package --package fixtures\public-contracts\packages\http2-core-scenario-pack
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance package --package fixtures\public-contracts\packages\neutral-toolchain
```

Run plan examples live under `run-plans/valid`, `run-plans/invalid`, and
`run-plans/incompatible`. They prove that run plan v1 is a selector and
provenance document: valid plans pin package bytes and select
package-provided IDs, schema-invalid plans try to omit package hashes, omit
work selection, or inline scenario behavior, and incompatible plans are
schema-valid but fail package selector compatibility.

Validate a resolved run plan against its package set before job creation:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance run-plan `
  --run-plan fixtures\public-contracts\run-plans\valid\http1-conformance-smoke-reference.json `
  --package fixtures\public-contracts\packages\http1-core-scenario-pack `
  --package fixtures\public-contracts\packages\reference-http1-test-executor `
  --package fixtures\public-contracts\packages\reference-http1-implementation
```

The HTTP/1 fixture set has separate run plans for `conformance-smoke` and
`benchmark-smoke`. Both select the same public test cases, but they carry
different suite/result metadata. Conformance answers whether behavior is
valid; benchmark answers how performance looks under the selected load
profile. A slow valid run is not a conformance failure, and a fast invalid run
is still invalid.

The HTTP/2 fixture set starts the next protocol family with the same
spec-first shape: `http2.core.plaintext`, `http2.core.json`, and
`http2.streaming.response` are defined in the scenario pack before controller,
site, or producer-package behavior can select them.

Invalid package fixtures live under `packages/invalid`. They are intentionally
not selectable examples; they exist to prove package upload and inventory
admission fail before bad metadata reaches a controller.
