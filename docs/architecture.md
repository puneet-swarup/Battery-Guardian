# Architecture

Battery Guardian is two applications - Windows (C#, WPF) and Android (Kotlin) -
that promise the same behaviour. This page describes the shape they share. It is
written for someone who has to change the code, not for someone deciding whether
to install it.

## The pipeline

```
              BatteryReading  (percent, on AC, charging)
                     │
                     ▼
              ThresholdMonitor          pure, one instance per pair of levels
                     │
        ThresholdSignal { Band, Previous, Changed }
                     │
      ┌──────────────┼───────────────┐
      ▼              ▼               ▼
 AlertConsumer   PlugConsumer   HistoryConsumer
 toast, speech   relay, retries  recording throttle
 repeat, snooze  intent/observed
```

Every consumer receives the same classification of the same reading. Nothing
downstream compares a percentage with a threshold - that happens in exactly one
place.

## Two halves of one signal

`ThresholdSignal` carries a **level** and an **edge**, because the two consumers
need different things and confusing them is how this feature broke repeatedly.

| | Band (level) | Changed (edge) |
|---|---|---|
| meaning | "at or above the high level right now" | "this is the reading that crossed" |
| wants it | the plug - it must end up correct even if it missed the crossing, for example while the app was starting or the machine was asleep | the alerts - a reminder fires once, not every thirty seconds |

An earlier version acted on edges only, so a plug that was not running at the
moment of crossing never acted at all. The plug is now level-driven and
idempotent; the alerts remain edge-driven.

## Patterns, and why each one is there

**Strategy - `ThresholdPair`.** One pair of levels drives everything. The alerts
and the plug once held their own, which allowed a reading to alert at 95 and
leave the charger running at 97 - the failure users reported. One pair, one
band, no possibility of disagreement.

**Hysteresis lives in the monitor.** Entering a band needs the level; leaving it
needs the level plus a margin (three points by default). A reading that wobbles
across the level - 95, 94, 96 - stays in the band instead of re-firing the alert
and clicking the relay. An *unusable* reading keeps the previous band rather
than clearing it: reporting `Unknown` there used to drop an active alert, which
then fired again on the next good reading.

**Observer / publish-subscribe - the consumers.** The reading is classified once
and handed to every consumer. Consumers do not know about each other, and adding
one does not touch the reading path.

**Ports and adapters - the edges.** `ThresholdMonitor`, the alert policy and the
plug decision are pure: no WPF, no Android APIs, no sockets. The adapters hold
the platform detail: WPF toast and `System.Speech` versus Android notifications
and `TextToSpeech`; the TuyaLan .NET package versus the vendored AAR.

**Orchestrator - `SmartPlugController`.** The plug is the one consumer with
state and a network, so it has a controller: one gate, one retry policy, one
place that records intent. There is a single way to change the relay.

## Connection state belongs to the library

A Tuya device accepts one session at a time, and a TCP connection can die
without either end being told: a router idle timeout, Wi-Fi power save, a device
reboot, or the host machine sleeping. The SDK owns that state, and 2.1.x makes it
say so:

- `IsConnected` / `isConnected` is authoritative. It turns false when the receive
  loop or the heartbeat sees the socket go.
- `EnsureConnectedAsync` / `ensureConnected` connects only if needed.
- There is **no automatic reconnection**; the host decides when to retry.
- `Disconnected` fires once per death.

The bug this replaced: both SDKs reported "the socket object exists", which stayed
true after the peer closed. A host that cached the device - the obvious reading of
the documentation - reused a dead connection for ever. Every command timed out,
nothing reconnected, and the device looked unreachable until the app restarted.
Battery Guardian carried exactly that workaround in its adapter for two releases.

## Observation is not intent

The plug distinguishes two facts that look alike:

- `Status.Relay` - what the plug last **reported**. Written only by a successful
  read.
- `IntendedRelay` - what this app last **asked for**. Written only by a send.

They were once the same field, which meant an unconfirmed command could later be
read back as though the device had confirmed it. Every sleep-path bug followed
from that confusion.

## What is deliberately not symmetric

| | Windows | Android |
|---|---|---|
| sleep / hibernate / shutdown | handled: the plug is cut, the state restored on return | not handled - a phone has no lid, and its suspend semantics are different |
| charge limit | vendor WMI on Lenovo, Dell and ASUS | not available |

Both are documented where they apply rather than pretended to be the same.

## Testing strategy

The pure core carries the weight:

| component | what it is tested for |
|---|---|
| `ThresholdMonitor` | both boundaries, between, above, below, unusable readings, invalid pairs, level versus edge |
| alert policy | which alert is active for a given band and power state |
| plug decision | the nine combinations of band and relay state |
| plug controller | a failed read does not stop a command; a command is never recorded as an observation; a busy plug is skipped, not queued |
| sleep policy | what is remembered, what is restored, what is not cut |

The tests for `ThresholdMonitor` are **the same table on both platforms**, in
`ThresholdMonitorTests.cs` and `ThresholdMonitorTest.kt`. That is intentional:
two apps that promise the same behaviour should be pinned by the same cases.

What is *not* covered by tests is the wiring: the WPF window, the Android
service, and anything that needs a real device. Those are covered by the
diagnostic log and, for the plug, by the "Read Smart Plug Now" and "Test Sleep
Handling" menu items.

## Where to change what

| change | where |
|---|---|
| what counts as high or low | `ThresholdPair`, and the monitor's tests |
| what an alert says or does | the alert policy; the consumer on each platform |
| what the plug does at a level | the plug decision |
| how the plug is talked to | the adapter; the protocol lives in the SDK |
| platform behaviour | the consumer's adapter, never the pure core |
