# Runbook

## Run locally
```bash
dotnet run --project src/Janus.LegacyTerminal          # fake old terminal, http://localhost:5207
dotnet run --project src/Janus.Facade                  # facade, http://localhost:5206 (Development: runtime flags on)
cd web && npm install && npm run dev                   # console, http://localhost:5173 (proxies to 5206)
```
Restore uses `nuget.config`: `eng/packages` (vendored `Agentic.Blocks.*`, ADR-0002) plus nuget.org.

## Tests
```bash
dotnet test Janus.sln                                   # CI (needs nuget.org for xUnit)
# offline workspace:
/home/claude/tools/run-xunit-offline.sh tests/Janus.Tests tests/Janus.TestSupport/Janus.TestSupport.csproj
cd web && npx vitest run && npm run build
```

## Flip a capability
- Production: set `Bridge:Capabilities:customers:Mode` to `Legacy`, `Shadow` or `New` in `appsettings.json` (reloaded on save) or the environment (`Bridge__Capabilities__customers__Mode`, needs restart).
- Development: `curl -X PUT localhost:5206/bridge/flags/customers -H 'Content-Type: application/json' -d '{"mode":"Shadow"}'`; `DELETE` clears the override.
- Check: `curl localhost:5206/bridge/status` (modes, breaker, shadow matched/mismatched).

## Deploy
One container per service (facade, and the real terminal stays where it is). Promote with the flag set to Legacy; move to Shadow, then New, per `docs/DESIGN.md` section 8.

## Monitor and alerts
- `/health/live` (process), `/health/ready` (Degraded but 200 when the terminal breaker is open).
- Alert: breaker Open > 5 min in business hours; any shadow mismatch on a capability in New.

## Common incidents
| Symptom | Action |
|---|---|
| 503 `bridge.legacy_unavailable` | old terminal down or slow; check it; breaker retries a trial call every 30 s |
| Shadow mismatches | read `lastMismatchFields` in `/bridge/status`; fix the ACL or data; do not flip |
| Wrong data after a flip | set mode back to `Legacy` (rollback plan, DESIGN.md section 8) |
| Demo: simulate outage | `curl -X PUT localhost:5207/TERM/FAULT/ERROR` (or `SLOW`, `NONE`) |
