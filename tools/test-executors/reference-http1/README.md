# ProtocolLab Reference HTTP/1 Test Executor

This is a deliberately small Test Executor v1 control plane for the public
HTTP/1 core smoke lane. It runs selected HTTP scenarios against a prepared
implementation endpoint supplied by the lab. It does not select packages,
start targets, or know about any implementation brand.

## Supported Selections

The executor supports only protocol `h1` and these exact test/scenario IDs:

| Test ID | Scenario ID | Request |
| --- | --- | --- |
| `http.core.plaintext` | `http.core.plaintext` | `GET /plaintext` |
| `http.core.json` | `http.core.json` | `GET /json` |
| `http.payload.bytes.1kb` | `http.payload.bytes.1kb` | `GET /bytes/1024` |

Any other protocol, test ID, scenario ID, or missing `primary` target endpoint
returns a structured `unsupported` prepare result.

## Build

```powershell
dotnet publish tools\test-executors\reference-http1\src\ReferenceHttp1TestExecutor.csproj -c Release -r win-x64 --self-contained false -o .artifacts\reference-http1-test-executor\bin\win-x64
```

Use the matching runtime identifier for Linux packages:

```powershell
dotnet publish tools\test-executors\reference-http1\src\ReferenceHttp1TestExecutor.csproj -c Release -r linux-x64 --self-contained false -o .artifacts\reference-http1-test-executor\bin\linux-x64
```

## Package Metadata

The public conformance fixture lives at:

```text
fixtures\public-contracts\packages\reference-http1-test-executor
```

Validate the package manifest and Test Executor v1 entry manifest:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance package --package fixtures\public-contracts\packages\reference-http1-test-executor
```

When creating a real `.plabpkg`, copy the fixture root to a staging directory
and place the published executable under the manifest entrypoint path, for
example `bin/win-x64/protocol-lab-reference-http1-test-executor.exe`.

## Run Locally

Start the executor control plane:

```powershell
dotnet run --project tools\test-executors\reference-http1\src\ReferenceHttp1TestExecutor.csproj --urls http://127.0.0.1:5088
```

Probe it with the public conformance command against an already running HTTP/1
target endpoint:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance test-executor --base-url http://127.0.0.1:5088 --test-id http.core.plaintext --scenario-id http.core.plaintext --scenario-version 1.0 --protocol h1 --target-scheme http --target-host 127.0.0.1 --target-port 5080 --target-path /
```

The executor writes `validation.json`, `result.json`,
`load-tool.stdout.txt`, `load-tool.stderr.txt`, and
`load-tool-execution.json` under `PROTOCOL_LAB_ARTIFACTS_DIR` when that
environment variable is set, otherwise under the current working directory.
Those artifacts include executor ID `protocol-lab-reference-http1-test-executor`
and version `0.1.0`.
