# Tuya Smart Plug Integration

BatteryGuardian can automatically control a Tuya-compatible smart plug based on
laptop battery level, so you never have to manually unplug or plug in the
charger. This document is the exhaustive reference for the feature on both the
Windows desktop app and the Android app.

## Overview

The smart plug acts as an external charging-power switch. BatteryGuardian
watches the battery and, when smart-plug control is enabled, switches the plug
OFF when the battery reaches the high threshold and ON when it drops to the low
threshold. Between the thresholds the plug keeps its current state (hysteresis),
which prevents rapid toggling.

| Battery condition | Desired plug state |
| --- | --- |
| >= high threshold (default 95 percent) | OFF |
| <= low threshold (default 15 percent) | ON |
| Between thresholds | No change |
| Unknown percentage | No change |

## Architecture

Both platforms share the same logical design:

1. Battery monitoring reads percentage, charging and AC state.
2. SmartPlugController applies threshold/hysteresis/safety rules. It knows
 nothing about AES, TCP or platform battery APIs; it talks to an ISmartPlug
 (C#) / SmartPlug (Kotlin) interface.
3. The Tuya LAN client handles TCP, session-key negotiation and AES-GCM.
4. The Tuya smart plug listens on TCP 6668 with relay datapoint 1.

Key components (Windows): TuyaSmartPlug (an adapter over the SDK),
SmartPlugController, SmartPlugDecision, TuyaCredentialStore, TuyaLocalKey.

Key components (Android): TuyaSmartPlug (an adapter over the SDK, like the C#
one), TuyaLocalKey, and the same controller classes under
com.puneet.batteryguardian.domain.smartplug, plus SmartPlugManager which builds
the controller from persisted settings and the Keystore-backed key.

## Protocol

The Tuya protocol is **not** implemented in this repository. Both apps use the
[Tuya LAN SDK](https://github.com/puneet-swarup/tuya-lan-sdk), which is verified
against real hardware and carries the parts that are easy to get wrong:

- the `0x00006699` frame, including the two reserved bytes, the 12-byte IV, the
  16-byte GCM tag and the `0x00009966` footer
- the three-message session-key handshake
- the 3.5 session key (GCM of the XORed nonces, not the 3.4 ECB derivation)
- control on `0x0D` with the body `{"protocol":5,"t":…,"data":{"dps":{…}}}`
- the associated data being the 14 bytes after the prefix, not the length field

`TuyaSmartPlug` (C#) and `TuyaSmartPlug.kt` are adapters. They keep the app's
`ISmartPlug` / `SmartPlug` shape, hold one connection open between polls, and
translate the SDK's exceptions into the app's error categories
(`SmartPlugError.Protocol`, `.Timeout`, `.Unreachable`).

### What this replaced

Until 2.3.0 both apps carried their own copy of the protocol, written from a
description that does not match real firmware:

| the old copy did | what 3.5 firmware actually does |
| --- | --- |
| frame `6699 │ len │ seq │ cmd │ nonce+ct+tag` | `6699 │ reserved(2) │ seq │ cmd │ len │ iv │ ct │ tag │ 009966` |
| AAD = the 4-byte length | AAD = the 14 bytes after the prefix |
| session key = AES-ECB(localKey, xor) | that is the 3.4 derivation; 3.5 uses GCM |
| control on `0x07`, body `{"dps":…}` | `0x0D`, body `{"protocol":5,"t":…,"data":{"dps":…}}` |
| status query on `0x08` | `0x08` is a status push; the query is `0x0A` |

It could not have switched a physical plug, and its unit tests passed because
they asserted the same wrong assumptions.

## Configuration

### Windows

Open Settings from the tray menu. Under Smart Plug set: enable control, device
ID, device IP, Local Key (stored with DPAPI, CurrentUser scope), port (default
6668), relay datapoint (default 1), switch OFF at (high), switch ON at (low).

### Sleep, lid close and shutdown

Windows turns a closed lid into a sleep or a shutdown, whichever your power plan
says, and Battery Guardian listens for both. When the machine goes away the plug
is **switched off**, and what it was doing is remembered. When it comes back - by
waking, or by starting again after a shutdown - the plug is put back the way it
was, and the threshold rules carry on from there.

Why remember rather than re-decide: a laptop that sleeps at 80% is between the
thresholds, where the rules say leave the relay alone. Without the remembered
state, the plug would stay off until the battery fell to 15%.

Three things worth knowing:

- **Nothing waits for the plug.** The app neither reads the plug nor blocks on
  the way out. The switch-off is handed to a background task and the handler
  returns immediately, so suspend and shutdown are never delayed and the machine
  is never held awake doing network work. If the machine sleeps before the
  command lands, the plug stays on - accepted, and written to the log.
- **A failed restore is retried.** If the plug does not answer on wake, the app
  tries again on the next reading, up to six times, and then tells you it gave
  up. It does not silently forget.
- **A lid set to "do nothing" raises no event**, because the machine never goes
  away. The threshold rules still cut the charger at your high level, as always.

Turn it off with the checkbox under Settings, Smart Plug.

### Snooze and the plug

Snoozing silences the alerts. It does **not** stop the plug: the app keeps
switching the charger while a snooze is running, so a meeting does not cost you
a night at 100%.

A failure is the exception. If the plug does not answer while a snooze is
running, the app tells you once - quietly, with no sound - because "keep
protecting me" is a promise it cannot keep silently.

If you want the plug to stop as well, turn smart-plug control off in Settings.
That is a deliberate second step, because "stop telling me" and "stop protecting
me" are different requests.

### Android

Open Settings and scroll to Smart Plug (Tuya). Enter the same fields. The Local
Key is encrypted with an Android Keystore AES-GCM key; only the ciphertext and
IV are stored in private preferences.

### Getting the device ID and local key

Use the TinyTuya wizard once to discover the device and obtain its device ID and
local key. These values are device-specific. Assign the plug a DHCP reservation
so its IP does not change.

## Safety rules

Verified by unit tests on both platforms:

- The actual relay state is authoritative, not the last stored state.
- Never send a command when the relay is already in the desired state.
- Never switch when the battery percentage is unknown.
- Query status first when the relay state is unknown.
- Never turn the plug OFF on app shutdown or when monitoring is disabled.
- If a command times out, query status before retrying.
- Manual changes are reconciled on the next evaluation.
- Low/high thresholds are validated: low must be below high.

## Security

The Tuya Local Key is a device-control credential and is never stored in
plaintext settings or logs:

- Windows: encrypted with DPAPI (ProtectedData.Protect, CurrentUser) in
 smartplug.key.bin, separate from Settings.json.
- Android: encrypted with an AES-GCM key held by the Android Keystore; only
 ciphertext + IV persist in a dedicated private preference file.

Diagnostic logs never include keys, session keys, nonces or ciphertext.

## Error handling

Failures are classified: not-configured, invalid-credentials, unreachable,
timeout, protocol and device-error. Retries use backoff (5s, 15s, 30s). A
failed status query never means the relay is OFF; the last known state is kept
as stale until a successful query.

## Testing

Pure, deterministic logic is unit tested without a device:

- Codec: encode/decode round-trip, 6699 prefix, bad prefix, short frame,
 fragmented reads, multiple frames per stream.
- Crypto: AES-GCM round-trip, tamper detection, wrong-key rejection, HMAC
 determinism, constant-time compare, local-key derivation.
- Decision: high/low threshold actions, intermediate retention, unknown
 battery, already-correct state, unknown relay query, config validation.

Run Windows tests with dotnet test Windows/BatteryGuardian.Tests. Run Android tests with
gradlew.bat :app:testDebugUnitTest.

## Troubleshooting

- Device unreachable: confirm the plug is on the same LAN and the IP is correct;
 check port 6668 is not blocked.
- Authentication failure: the local key is wrong or the device was reset;
 re-run the TinyTuya wizard for a fresh key.
- Relay state unknown: a status query failed; the app will not switch blindly.
- Plug never switches: ensure smart-plug control is enabled and that the battery
 percentage is being read.
