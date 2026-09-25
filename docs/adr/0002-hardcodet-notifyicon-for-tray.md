# 0002: Use Hardcodet.NotifyIcon.Wpf for the tray icon

- **Status:** Accepted
- **Date:** 2026-09-23

## Context

Battery Guardian lives in the system tray, so a tray icon library is essential.

Initially the project used `System.Windows.Forms.NotifyIcon` directly, drawing
a custom battery icon at runtime with `System.Drawing`. This worked in
development but crashed with `0xC000041D` (STATUS_FATAL_USER_CALLBACK_EXCEPTION)
in a user-profile install location. The root cause was a GDI handle lifetime
bug: `Icon.Clone()` returns a shallow copy sharing the native handle, so
disposing the temporary icon invalidated the handle still referenced by the
tray.

This class of bug is well known in WPF tray icon development and is the reason
multiple mature libraries exist.

## Decision

Use **Hardcodet.NotifyIcon.Wpf** (a widely-used, actively-maintained NuGet
library) for the tray icon instead of raw WinForms interop.

The icon is loaded once at startup from the embedded `Assets/app.ico` and never
mutated at runtime. No dynamic drawing, no GDI handle churn.

## Consequences

**Easier:**
- The library manages icon lifetimes correctly, eliminating the crash class.
- Native WPF integration (context menus are `System.Windows.Controls.ContextMenu`).
- The library is actively used across thousands of WPF projects, so security
  and compatibility issues are surfaced by a broader community.

**Harder:**
- One additional NuGet dependency.
- The library is not part of the .NET platform, so a future breaking change
  would require manual attention.

**Given up:**
- The dynamic multi-color tray icon we used to draw (green/orange/red battery).
  The static `app.ico` is used instead.
- The blinking tray icon during alerts. Windows 11 aggressively hides new tray
  icons inside the overflow menu, and blinking was not reliably visible there.

## Retrospective Note

This ADR was written retroactively on 2026-09-25 to document a decision made
earlier in the project. It reflects the reasoning as it existed at the time.