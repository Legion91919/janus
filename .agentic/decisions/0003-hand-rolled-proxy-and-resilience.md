# 0003. Hand-rolled forwarding proxy and resilience (refines 0001)

- Status: Accepted
- Date: 2026-11-20

## Context
ADR-0001 named YARP for the routing layer and the build skill names Polly for resilience. Neither can be restored here (no package feed beyond the vendored blocks), and the facade only needs three prefixes, GET-first traffic and one upstream.

## Decision
`Janus.Facade` is a small reverse-proxy middleware over `HttpClient`: it matches a capability by path prefix, reads that capability's flag from `IOptionsMonitor<BridgeOptions>` on every request (Legacy / Shadow / New), forwards to the old terminal, and adds `X-Served-By` and `X-Bridge-Mode`. Timeouts (per attempt, `TimeProvider`), retries with full jitter (GET/HEAD only) and a consecutive-failure circuit breaker are about 150 lines in `Legacy/Resilience.cs`, all unit-tested.

## Alternatives considered
- YARP: right choice once there are many routes, load balancing, or WebSockets. Swap in by replacing `BridgeMiddleware`; the flags, adapter and contract tests stay.
- Polly / Microsoft.Extensions.Http.Resilience: richer policies (hedging, rate limiting, telemetry). Swap in behind `ResilientCaller`.

## Consequences
- No third-party runtime dependencies; every behaviour is visible and tested.
- We own edge cases a library would cover (hop-by-hop headers, streaming large bodies). Only a header allow-list is forwarded; bodies are buffered. Revisit before write capabilities move.
