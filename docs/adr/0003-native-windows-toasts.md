# 0003: Use native Windows Action Center toasts

- **Status:** Accepted
- **Date:** 2026-09-23

## Context

The app needs to notify the user when the battery crosses a threshold.

The first implementation used a custom WPF `Window` positioned bottom-right
with a colored background. It worked but had significant drawbacks:
- It did not appear in the Action Center, so missed notifications were lost.
- It fought with other windows over z-order (`Topmost`).
- It was easily missed by users not watching the bottom-right corner.
- Custom rendering introduced its own bugs during the project.

WinForms balloon tips (`NotifyIcon.ShowBalloonTip`) were used next but are
deprecated by Microsoft and render inconsistently on Windows 11.

## Decision

Use **native Windows 10/11 Action Center toasts** via
`Microsoft.Toolkit.Uwp.Notifications` (v7.1.3).

This library was chosen over the deprecated `CommunityToolkit.WinUI.Notifications`
and the newer Windows App SDK `AppNotificationManager` because it works with
unpackaged, self-contained desktop applications without requiring the Windows
App Runtime to be installed.

## Consequences

**Easier:**
- Toasts render in the standard Windows 11 style, consistent with Outlook and
  Teams, so users already know how to interact with them.
- They persist in the Action Center for later review.
- Clicking a toast restores the main window (handled via `OnActivated`).

**Harder:**
- The target framework had to be bumped to `net10.0-windows10.0.17763.0` to
  access the Windows SDK APIs.
- `Microsoft.Windows.SDK.NET.dll` adds ~23 MB to the framework-dependent
  download — the single largest non-runtime file.

**Given up:**
- The fully-custom-styled popup window.
- A smaller download size.

## Retrospective Note

This ADR was written retroactively on 2026-09-25 to document a decision made
earlier in the project. It reflects the reasoning as it existed at the time.