# 0007 - Threshold signals drive the consumers

## Status

Accepted - 2026-11-22

## Context

The application has two consumers of a battery reading: the alerts (toast and
speech) and, later, the smart plug. Each was written as its own pipeline. Each
compared a percentage against its own thresholds, kept its own state machine,
and was called from the same method in the Windows window:

```
RefreshBatteryStatus()
  ├─ EvaluateAlerts(percent, charging, onAc)   → alert state on the window
  ├─ EvaluateSmartPlugAsync(percent)           → plug state in a controller
  └─ RecordHistory(percent, onAc)
```

Two consequences showed up in production, both reported by users:

1. The plug did not switch at the low level. The decision refused to act unless
   it had first read the relay state successfully, and the state read fails
   exactly when the plug has been idle for hours - which is the case at the low
   level and never the case at the high level. Cutting power worked; restoring
   it did not.
2. Fixing it took several attempts, each landing in a different corner, because
   there was no single answer to "what does this reading mean".

A third problem was structural: the alert state lived as fields on a 1,364-line
window and the plug state lived in a controller, so the two could not be
reasoned about together or tested together.

## Decision

Introduce a pure `ThresholdMonitor` that classifies a reading against one pair
of levels and returns a `ThresholdSignal` carrying both halves of what the
consumers need:

- **Band** - a level: `High`, `Low`, `InBand`, `Unknown`.
- **Changed** - an edge: whether this reading is the one that crossed.

Alerts consume the band and the edge; the plug consumes the band alone, because
a switch must be correct even if it missed the crossing. Both applications use
the same type, the same semantics and the same test table.

## Consequences

**Good**

- One place compares percentages with thresholds. A consumer cannot disagree
  with another about what a reading meant.
- The plug became level-driven and idempotent, which removes the whole class of
  "the app was not running at the moment of crossing" failures.
- Each consumer is testable against a signal instead of through a window.
- The two platforms now have the same shape, so a change in one is visible in
  the other.

**Costs**

- The pure core gained a concept (`BatteryBand`) that has to be understood
  before changing a consumer.
- Consumer tests now pass a band rather than a percentage, so a reader has to
  follow one indirection.
- An invalid pair of levels produces `Unknown` rather than a best guess, so a
  broken configuration is silent instead of arbitrary. Validation on the
  settings screen is what stops that reaching a user.

**Not done here**

The consumers still live inside the platform shells - the WPF window and the
Android service. Extracting them into standalone classes is a separate change,
and it touches the alert wiring, which has no automated coverage today.
