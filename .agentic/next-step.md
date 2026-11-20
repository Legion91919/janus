# Next step

- **Doing:** nothing in progress; slice 1 is complete.
- **Done so far:** Sketch leg; slice 1 (facade, fake terminal, Customers module + ACL, contract/acceptance/resilience/architecture tests, React console); ADR-0002, ADR-0003; docs/DESIGN.md.
- **Next action:** start slice 2: write the failing test for a backfill job that imports the nightly customer export through `ILegacyCustomerTranslator` into a persisted store.
- **Blocked by:** nothing (EF Core needs a package feed; CI has one).
