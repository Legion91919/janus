# Quality

## Definition of done
- Acceptance criteria met and demonstrated.
- Unit tests for rules, integration tests for each adapter, one end-to-end test per key flow.
- Contract tests green (legacy-via-adapter equals new) before any flag moves past Shadow.
- CI green: build, tests, lint, dependency and secret scan, web (vitest + build).
- Docs in `.agentic/` updated in the same change.

## Service levels
| SLI | SLO |
|---|---|
| Facade success rate (non-5xx) | 99.9% over 30 days |
| p95 latency, Legacy mode | < 300 ms |
| p95 latency, New mode | < 50 ms |
| Added facade overhead | p95 < 20 ms |
| Flag change takes effect | next request (< 1 s) |
| Shadow mismatches before a flip to New | 0 over 7 days, >= 1 000 comparisons |
| Legacy breaker open time | < 5 min per day |
| Personal data in logs | none (tested) |
