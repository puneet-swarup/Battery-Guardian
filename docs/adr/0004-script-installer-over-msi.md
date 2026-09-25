# 0004: Ship a script-based installer instead of an MSI

- **Status:** Accepted
- **Date:** 2026-09-23

## Context

Users needed a clean install/uninstall experience: files in the right place, a
Start Menu shortcut, and the ability to remove the app from Windows Settings.

Three approaches were evaluated:

1. **Visual Studio Installer Project** (`.vdproj`) — deprecated, and it looped
   on the .NET runtime check, prompting users to install .NET even when it was
   already present.
2. **WiX Toolset MSI** — the industry-standard installer for Windows. Produces
   a proper MSI but requires Windows Installer ICE validation and its associated
   rules. Multiple ICE errors (`ICE38`, `ICE43`, `ICE57`, `ICE64`) surfaced
   during development and each required non-trivial workarounds. The 25 MB
   attachment limit on GitHub Releases and the complexity of a UI-less MSI also
   added friction.
3. **Script-based installer** (`install.ps1` / `uninstall.ps1`, invoked by
   `install.bat` / `uninstall.bat`) — installs per-user to
   `%LocalAppData%\Programs\BatteryGuardian\`, creates a Start Menu shortcut,
   registers in Settings → Apps via HKCU, and requires no admin rights.

## Decision

Ship a **script-based installer**.

The scripts are included in the release ZIP. Users extract the ZIP and
double-click `install.bat`. Uninstall is triggered from Settings → Apps, which
runs `uninstall.bat` in the install folder.

## Consequences

**Easier:**
- No admin privileges required — per-user install by design.
- No MSI packaging tooling, no ICE validation rules to work around.
- The scripts are human-readable and easy to debug when a user reports a problem.
- Installer can stop a running instance of the app before overwriting files.

**Harder:**
- No native Windows installer UI (no Next-Next-Finish wizard).
- Users accustomed to MSI installers may find a `.bat` file unusual.
- Antivirus tools occasionally flag unsigned `.bat` scripts.

**Given up:**
- Windows Installer repair/diagnostics.
- Group Policy deployment scenarios.
- Proper MSI signing (when SignPath approval completes, the signed artifacts
  will be the .exe inside the ZIP, not the script wrapper).

## Retrospective Note

This ADR was written retroactively on 2026-09-25 to document a decision made
earlier in the project. It reflects the reasoning as it existed at the time.