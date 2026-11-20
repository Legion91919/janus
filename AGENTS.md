# AGENTS.md

Janus: Harbourline's loan servicing runs on an ageing terminal application only two people understand, so every change is slow and risky.

## Read first
Everything about this project lives in the hidden `.agentic/` folder.
Start with `.agentic/README.md`; it says which file answers which question.
Load only the files your task needs.

## Hard rules
- Work in thin vertical slices: one ticket from `.agentic/backlog.md` at a time.
- No change without a test that fails first. Run the full test suite before you finish.
- A decision that is hard to reverse gets an ADR in `.agentic/decisions/`.
- Never commit secrets, real client data or personal data. Use `.env.example`.
- Do not grow this file. New knowledge goes in the right `.agentic/` file; history goes in `.agentic/changelog.md`.
- When you learn something that would help on the next project, add it to `.agentic/lessons.md`.
## What is live
- Slice 1: `Janus.Facade` (5206) routes `/customers`, `/schedules`, `/arrears` to the fake old terminal (5207) or the `Janus.Customers` module per flag (Legacy / Shadow / New); React console in `web/`. Tests: `.agentic/runbook.md`.
