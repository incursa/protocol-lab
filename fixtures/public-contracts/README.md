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
```
