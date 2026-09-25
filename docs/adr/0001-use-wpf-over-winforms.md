
# 0001: Use WPF (not WinForms) for the UI

- **Status:** Accepted
- **Date:** 2026-08-07

## Context

The application needs a desktop UI on Windows. The two mainstream choices for
.NET desktop development are Windows Presentation Foundation (WPF) and Windows
Forms (WinForms).

WinForms is older, simpler, and lighter, but its layout and styling capabilities
are limited. WPF offers a retained-mode rendering model, data binding, vector
graphics, and richer styling.

## Decision

Use **WPF** for all user-visible windows (main window, settings, about). Use
WinForms only for the system tray icon, because the WPF ecosystem historically
lacked a first-class tray icon library until Hardcodet.NotifyIcon.Wpf matured
(see [ADR 0002](0002-hardcodet-notifyicon-for-tray.md)).

The project targets `net10.0-windows10.0.17763.0` with both `<UseWPF>` and
`<UseWindowsForms>` enabled.

## Consequences

**Easier:**
- Rich UI with minimal effort.
- Vector-based rendering scales cleanly with high-DPI displays.
- WPF is actively maintained as part of the .NET platform.

**Harder:**
- Mixing WPF and WinForms in one process introduces occasional type-name
  collisions (`MessageBox`, `MenuItem`, `ContextMenu`). These are resolved by
  fully qualifying `System.Windows.` or `System.Windows.Forms.` where needed.
- WPF has a larger startup cost and larger memory footprint than WinForms.

**Given up:**
- The simplicity of WinForms-only.
- The smaller binary size of a WinForms application.

## Retrospective Note

This ADR was written retroactively on 2026-09-25 to document a decision made
earlier in the project. It reflects the reasoning as it existed at the time.