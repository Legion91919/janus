# User flows

## Flow 1: Read customer details through the bridge (slice 1)
1. Operator opens **Traffic console** (`screens/traffic-console.html`, React `web/src/App.tsx`).
2. Console loads flags, breaker state and shadow counts from `GET /bridge/status`.
3. Operator sends `GET /customers/{number}` (or `GET /customers`); the facade (port 5206) routes by the `customers` flag.
4. The request log row shows `X-Served-By` (legacy / new), `X-Bridge-Mode`, status and duration.

## Flow 2: Move the customers capability without a deploy (slice 1)
1. On **Traffic console**, operator switches `customers` from Legacy to Shadow (`PUT /bridge/flags/customers`, Development) or edits `Bridge:Capabilities:customers:Mode` in `appsettings.json` (reloaded on change).
2. Shadow: responses still come from legacy; the shadow panel shows matched / mismatched counts (`GET /bridge/status`).
3. With zero mismatches over the agreed window, operator switches to New; the next row shows `X-Served-By: new`.
4. Rollback: switch back to Legacy; the next request is served by legacy again.

## Flow 3: Not yet migrated capabilities (schedules, arrears)
1. `GET /schedules/{loan}` and `GET /arrears` are forwarded byte-for-byte to the old terminal (port 5207); the console shows the raw fixed-width body.
