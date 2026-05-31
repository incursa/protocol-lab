# ProtocolLab Public Repository Readiness

Status: Partially ready

## Summary

- The public tree is public-safe: searches for private checkout markers, private path markers, branding markers, secret/token/password patterns, and forbidden file globs found no leak indicators in the canonical public checkout.
- `dotnet restore`, `dotnet build`, `dotnet test`, and `workbench validate --profile core` passed in a disposable public worktree.
- The acceptance smoke lane failed in `Incursa H3 validation` with `Target did not become ready within 30 seconds` and `HttpClient.Timeout of 2 seconds`, so the public repo is not fully green yet.
- The canonical public checkout still has pre-existing local edits from the split work. I preserved them and did not use them as build inputs.

## Commands Run

```powershell
git status --short --branch
git remote -v
# private marker scan: no matches
# forbidden-file scan: no matches
dotnet tool restore
dotnet restore Incursa.ProtocolLab.sln
dotnet build Incursa.ProtocolLab.sln -v minimal
dotnet test Incursa.ProtocolLab.sln --no-build -v minimal
workbench validate --profile core
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\acceptance\Invoke-ProtocolLabAcceptance.ps1 -RunIdPrefix post-split-public-smoke -SkipExternal -SkipCounters -DurationSeconds 5 -WarmupSeconds 1 -Repetitions 1
```

## Results

- Git remotes: pass
- Git status: not clean because of pre-existing local edits in the canonical checkout
- Leak scan: pass
- Restore/build/test: pass
- Repo validation: pass
- Acceptance smoke: fail

## Blockers

- `Incursa HTTP/3` readiness timed out during the smoke acceptance lane.

## Next Actions

1. Retry the Incursa H3 acceptance lane after adjusting the readiness timeout or validating the local host conditions.
2. Re-run acceptance once the smoke lane is green.
