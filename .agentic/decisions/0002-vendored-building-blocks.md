# 0002. Vendored building blocks

- Status: Accepted
- Date: 2026-11-20

## Context
Janus uses `Agentic.Blocks.Core` (Result/Error), `Agentic.Blocks.Web` (ProblemDetails, correlation id, health) and, in tests, `Agentic.Blocks.Testing` (ManualTimeProvider, KestrelTestHost). They are not on nuget.org, and the build must work for the client without a private feed.

## Decision
Vendor the three `.nupkg` files (0.1.0) into `eng/packages/`. `nuget.config` lists that folder as a local source mapped to `Agentic.Blocks.*`, plus nuget.org for everything else (xUnit in CI).

## Alternatives considered
- Private GitHub Packages feed: needs a token on every machine and in CI; the client would inherit our account.
- Copy the source into the repo: loses the version boundary and makes upgrades a merge exercise.

## Consequences
- Clone and build anywhere. Upgrading a block is replacing a file and bumping the version.
- Binary files in git (about 100 KB in total); acceptable.
