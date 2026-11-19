# Product brief

> Harbourline Lending is a fictional company. All names, documents and numbers in this repo are invented.

## Problem
Harbourline's loan servicing runs on an ageing terminal application only two people understand, so every change is slow and risky.

## Users
Servicing team (daily); developers (change requests).

## Outcomes
First capability (customer details) moved off the old system with no downtime; changes ship weekly instead of quarterly.

## Constraints
The old system must keep running; both sides must agree during migration.

## Out of scope
Anything not needed for the outcomes above in the first three slices.

## Traits (for the architecture advisor)
- LegacySystems: "a terminal-based servicing app with a nightly file export"
- SeveralBusinessAreas: "customer details, payment schedules, arrears"
- ExternalPartnerIntegrations: "bank payment files and a credit bureau"
- SmallTeam: "two engineers"
