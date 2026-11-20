# Decision: traffic console

Scored 1 (poor) to 5 (great). Reviewed at 1440px and 390px widths.

| Variant | Main job speed | Clarity | Accessibility | Tone | Total |
|---|---|---|---|---|---|
| A: Dense request log (table-first) | 5 | 4 | 4 | 4 | 17 |
| B: Capability cards (summary-first) | 3 | 5 | 5 | 4 | 17 |
| C: Cut-over checklist (workflow-first) | 2 | 4 | 4 | 3 | 13 |

**Pick: B's capability cards on top of A's request log.** The cards make the flag and the shadow count for each
capability obvious (the decision an operator makes), and A's log answers "who served that request?" without a click.
C's checklist is a good runbook but too slow to use during a cut-over; its "ready to flip" hint (zero mismatches) moves into the card.
Badges always carry text plus a glyph (`▤ LEGACY`, `◆ NEW`), never colour alone.
