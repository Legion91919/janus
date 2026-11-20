# Handover

What the client needs to own this without us.

- [ ] Source, infrastructure-as-code and pipelines in the client's own accounts
- [ ] Every secret rotated and owned by the client
- [ ] Architecture and decisions explained (`architecture.md`, `decisions/`)
- [ ] Runbook rehearsed by a client engineer
- [ ] Known issues and next steps listed below
- [ ] AI agent setup documented so their team can keep using it

## Known issues
- Customer ACL title-cases names; prefixes like Mc/van need an exception table before real data.
- Customers store is in memory; shadow counters reset on restart.
- No authentication yet (internal network assumed).

## Recommended next steps
- Slice 2 persistence and backfill; then move schedules with the same five steps (docs/DESIGN.md section 8).