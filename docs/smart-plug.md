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

Key components (Windows): TuyaCrypto, TuyaMessageCodec, TuyaSession,
TuyaMessageCipher, TuyaSmartPlug, SmartPlugController, SmartPlugDecision,
TuyaCredentialStore.

Key components (Android): the same names under
com.puneet.batteryguardian.data.tuya and
com.puneet.batteryguardian.domain.smartplug, plus SmartPlugManager which builds
the controller from persisted settings and the Keystore-backed key.

## Protocol (Tuya 3.5)

The client speaks the Tuya local protocol 3.5, documented by the TinyTuya
project. No Python runtime or TinyTuya install is required; the protocol is
implemented natively in C# and Kotlin.

Wire framing (6699 codec):


prefix(4)=00 00 66 99 | totalLength(4) | seq(4) | command(4) | payload


totalLength counts seq + command + payload. All integers are big-endian.

Session-key negotiation:

1. Client sends 0x03 (SESS_KEY_NEG_START) with a 16-byte client nonce.
2. Device replies 0x04 with a 16-byte device nonce.
3. Client sends 0x05 with HMAC-SHA256(localKey, clientNonce + deviceNonce).
4. Device replies 0x06 with HMAC-SHA256(localKey, deviceNonce + clientNonce),
 which the client verifies in constant time.
5. Session key = AES-128-ECB-Encrypt(localKey, clientNonce XOR deviceNonce).

After negotiation, message payloads are AES-128-GCM encrypted with the session
key. A random 12-byte nonce is prepended and the 16-byte authentication tag is
appended; the associated data is the 4-byte big-endian packet length. Commands:
0x07 control (DP write), 0x08 status query.

## Configuration

### Windows

Open Settings from the tray menu. Under Smart Plug set: enable control, device
ID, device IP, Local Key (stored with DPAPI, CurrentUser scope), port (default
6668), relay datapoint (default 1), switch OFF at (high), switch ON at (low).

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

Run Windows tests with dotnet test BatteryGuardian.Tests. Run Android tests with
gradlew.bat :app:testDebugUnitTest.

## Troubleshooting

- Device unreachable: confirm the plug is on the same LAN and the IP is correct;
 check port 6668 is not blocked.
- Authentication failure: the local key is wrong or the device was reset;
 re-run the TinyTuya wizard for a fresh key.
- Relay state unknown: a status query failed; the app will not switch blindly.
- Plug never switches: ensure smart-plug control is enabled and that the battery
 percentage is being read.
