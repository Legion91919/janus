# Lessons

Generic, reusable lessons only. No client names, data or secrets.
- Strangler fig: put the anti-corruption layer in the new module, not in the facade; the facade knows routes, the module knows record layouts.
- Shadow comparison: log JSON paths and a hashed fingerprint, never values; write the log line before updating counters so tests polling the counters see the log too.
- Contract tests are cheapest as "two facades, two flags, same request, same response": they exercise the real routing too.
- Hand-rolled resilience is small and testable with an injectable TimeProvider and a random source; keep retries to idempotent methods.
