# Janus

> Harbourline Lending is a fictional company. All names, documents and numbers in this repo are invented.

Harbourline's loan servicing runs on an ageing terminal application only two people understand, so every change is slow and risky. Janus, named after the gatekeeper who faces both ways, sits in front of the old terminal and the new modules and moves traffic one capability at a time.

## See it in action

![Janus traffic console: requests served by the old terminal, the customers capability switched to Shadow and then New at runtime with the served-by badge changing, and an old-terminal outage returning a clean 503 while migrated customers keep working](docs/media/demo.gif)

| Everything on legacy | Shadow comparison | Old terminal down |
|---|---|---|
| ![Requests served by the old terminal](docs/media/legacy.png) | ![Customers in Shadow mode: 10 comparisons, no mismatches, ready to flip](docs/media/shadow.png) | ![A 503 for an unmigrated capability while customers is served by new](docs/media/outage.png) |

Full-quality video: [docs/media/demo.mp4](docs/media/demo.mp4). Recorded from the running services with `demo/` (`cd demo && npm install && npm run record`); it starts the fake terminal, the facade and the console, and uses the terminal's demo fault switch for the outage.

## Design at a glance
**Brief:** move customer details off the old terminal with no downtime, prove old and new agree, and make every move reversible with a flag.

**Architecture:** a strangler fig in front of a modular monolith. `Janus.Facade` is a small reverse proxy (custom middleware over `HttpClient`, no YARP) that reads a flag per capability on every request: **Legacy** forwards to the old terminal, **Shadow** forwards and compares with the new module in the background, **New** serves from the new module. Each response says who served it (`X-Served-By`). The new `Janus.Customers` module owns an anti-corruption layer that turns fixed-width legacy records into clean JSON, and contract tests prove both sides return the same data. The advisor ranked a modular monolith first for a small team with several business areas and a legacy system; microservices lost on operating cost and a rewrite lost on risk. Details: [`docs/DESIGN.md`](docs/DESIGN.md).

```mermaid
flowchart LR
  staff[Servicing team and integrations] --> janus[Janus facade :5206<br/>flags: Legacy / Shadow / New]
  console[Traffic console :5173] --> janus
  janus -->|Legacy, Shadow| terminal[Old terminal :5207<br/>fixed-width text]
  janus -->|New, Shadow compare| customers[Janus.Customers module<br/>+ anti-corruption layer]
```


## Run it in GitHub Codespaces

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Legion91919/janus?quickstart=1)

The dev container installs the SDKs, restores packages and runs every test once on first start. The same setup works locally with VS Code's Dev Containers extension.

| What | Command | Port |
|---|---|---|
| Legacy terminal (fake old system) | `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://0.0.0.0:5207 dotnet run --project src/Janus.LegacyTerminal` | 5207 |
| API (Janus) | `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://0.0.0.0:5206 dotnet run --project src/Janus.Facade` | 5206 |
| Web app | `cd web && npm run dev` | 5173 |
| All tests (same as CI) | `scripts/test-all.sh` | - |

## What is in the repo
| Path | What |
|---|---|
| `src/Janus.Facade` | routing middleware, flags (config reload + Development override endpoint), timeouts, retries with jitter, circuit breaker, shadow comparison, `/bridge/status` |
| `src/Janus.Customers` | the first migrated capability: customer directory and the legacy record translator |
| `src/Janus.LegacyTerminal` | a fake of the old system: fixed-width text, upper-case codes, CYYMMDD dates, errors as HTTP 200 |
| `tests/Janus.Tests` | acceptance, contract, translator, resilience, routing and architecture tests |
| `web/` | React traffic console: flip flags, send requests, see which side served each one |

## Run locally
```bash
dotnet run --project src/Janus.LegacyTerminal   # http://localhost:5207
dotnet run --project src/Janus.Facade           # http://localhost:5206
cd web && npm install && npm run dev            # http://localhost:5173
curl -i http://localhost:5206/customers/10042   # X-Served-By: legacy
```
More in `.agentic/runbook.md`.

## How this project is organised
Project knowledge lives in the hidden `.agentic/` folder (start with `.agentic/README.md`). AI agents read `AGENTS.md` first.

Built with the Baton model from the Agentic Engineering Kit: frame → map → decide → sketch → slice loop → pass the baton.
