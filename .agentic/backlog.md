# Backlog

Vertical slices, smallest valuable first.

## Now
- [ ] Slice 1: Customer details are read through the new API, served by the old system behind an adapter
  - Given the routing layer, when /customers is called, then it is served by the legacy adapter with identical data
  - Given a contract test suite, then old and new implementations return the same results
  - Given a flag switch, then traffic moves to the new module without a deploy
  - Test notes: one acceptance test per criterion; seeded invented data.

## Next
- [ ] Sketch leg: tokens + three variants of the slice-1 screen (`sketch-ui`)
- [ ] CI green with all gates before the first feature commit

## Later
