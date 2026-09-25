# Architecture Decision Records

This folder contains Architecture Decision Records (ADRs) for Battery Guardian.
Each ADR documents a significant architectural decision, its context, and its consequences.

Format inspired by [Michael Nygard's original proposal](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions).

## Index

| # | Title | Status |
|---|---|---|
| [0001](0001-use-wpf-over-winforms.md) | Use WPF (not WinForms) for the UI | Accepted |
| [0002](0002-hardcodet-notifyicon-for-tray.md) | Use Hardcodet.NotifyIcon.Wpf for the tray icon | Accepted |
| [0003](0003-native-windows-toasts.md) | Use native Windows Action Center toasts | Accepted |
| [0004](0004-script-installer-over-msi.md) | Ship a script-based installer instead of an MSI | Accepted |
| [0005](0005-two-release-variants.md) | Ship both self-contained and framework-dependent builds | Accepted |
| [0006](0006-minver-for-versioning.md) | Derive version from Git tags via MinVer | Accepted |