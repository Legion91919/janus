# 0001. Architecture style

- Status: Accepted
- Date: 2026-11-19

## Context

Harbourline's loan servicing runs on an ageing terminal application only two people understand, so every change is slow and risky. Traits: LegacySystems, SeveralBusinessAreas, ExternalPartnerIntegrations, SmallTeam.

## Decision

Use a strangler fig: a YARP routing layer in front, an adapter around the old system, and a new modular monolith that takes over feature by feature.

## Alternatives considered

- Big-bang rewrite: months with no value and a risky cut-over.
- Wrap only, never replace: keeps the risk forever.

## Consequences

- Clear boundaries that tests can enforce.
- Room to extract a part later if its load demands it.
