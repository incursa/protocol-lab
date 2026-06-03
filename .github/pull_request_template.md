## Summary

Describe the change and the user-visible impact.

## Verification

- [ ] `dotnet tool restore`
- [ ] `dotnet restore Incursa.ProtocolLab.sln`
- [ ] `dotnet build Incursa.ProtocolLab.sln --no-restore`
- [ ] `dotnet test Incursa.ProtocolLab.sln --no-build`
- [ ] `dotnet run --project src\Incursa.ProtocolLab.Cli -- check`
- [ ] Markdown link check passed
- [ ] Leak scan passed
- [ ] `workbench validate --profile core` passed, if available

## Public Release Guardrails

- [ ] Public docs avoid official, verified, or industry-standard claims
- [ ] Public/community vs internal/commercial boundary is explicit
- [ ] Any benchmark claim states the evidence class and its limitations

## Contribution Agreement

- [ ] I have read [CONTRIBUTOR-AGREEMENT.md](/CONTRIBUTOR-AGREEMENT.md)
      and, if required, signed the agreement on this pull request.

## Notes

Add anything a reviewer should know about tradeoffs, risks, or follow-up work.
