# Components (traffic console)

| Component | Props | Loading | Empty | Error | Success |
|---|---|---|---|---|---|
| `CapabilityCard` | `name`, `mode`, `shadow: {matched, mismatched}`, `onModeChange(mode)` | skeleton bar | n/a (three fixed capabilities) | inline "Could not change flag" with retry | segmented Legacy/Shadow/New control, shadow counts, "ready to flip" hint |
| `BreakerPill` | `state: Closed/Open/HalfOpen` | grey "checking" | n/a | "status unavailable" | text + glyph pill |
| `RequestBar` | `onSend(path)`, `busy` | button disabled with "Sending" | n/a | n/a | preset buttons + free path input |
| `RequestLog` | `rows: LogRow[]`, `onSelect(row)` | "Waiting for first response" | "No requests yet. Send one above." | rows with status >= 500 flagged with text "failed" | table: time, path, status, served by badge, mode, ms |
| `ServedByBadge` | `side: legacy/new/unknown` | n/a | n/a | `? UNKNOWN` | `▤ LEGACY` or `◆ NEW` |
| `ResponsePreview` | `row` | n/a | "Select a row to see the body" | ProblemDetails title | JSON pretty-printed or raw fixed-width text in monospace |
