# Security Policy

## Supported Versions

Only the latest released version of Battery Guardian receives security updates.

| Version | Supported |
|---|---|
| Latest release | ✅ |
| Older releases | ❌ |

## Reporting a Vulnerability

If you discover a security issue in Battery Guardian, please **do not open a public GitHub issue**.

Instead, report it privately via GitHub's [private vulnerability reporting](https://github.com/puneet-swarup/Battery-Guardian/security/advisories/new) feature.

Please include:

- A clear description of the issue.
- Steps to reproduce it.
- The version of Battery Guardian and the version of Windows you are using.
- Any relevant logs (enable diagnostic logging in Settings first).

## Scope

Battery Guardian is a local, offline utility. It does not:

- Transmit any data to external servers (except a public HTTPS request to GitHub for update checking).
- Collect telemetry, analytics, or usage data.
- Require elevated privileges.
- Access the network beyond the update check.

Security issues we care about most:

- Remote code execution.
- Privilege escalation.
- Bypassing the update-check to serve malicious code.
- Supply chain issues in our dependencies.

## Disclosure

Once a report is received, we aim to:

1. Acknowledge receipt within 5 days.
2. Investigate and confirm the issue within 14 days.
3. Release a patched version as soon as practical.
4. Credit the reporter (unless you prefer to remain anonymous).

Thank you for helping keep Battery Guardian safe.