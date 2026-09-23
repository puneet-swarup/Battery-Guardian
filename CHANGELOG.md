# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.0] - 2026-09-23

### Added
- Estimated time remaining (when discharging) and time to full (when charging), shown in the main window.
- Tray icon tooltip now includes battery percentage and charging status at a glance.
- GitHub Issue templates (bug report, feature request) and Pull Request template.
- This `CHANGELOG.md` file.

### Changed
- README updated to document the new time-estimate and tooltip features.

## [1.1.0] - 2026-08-26

### Added
- Configurable repeat alert interval (default: 5 minutes).
- Voice message and beep now repeat until the user acts on the alert.

### Fixed
- Alert now stops immediately when the charger is plugged in or unplugged (uses AC line status instead of the charging flag).
- Prevented the reminder timer from being inadvertently stopped by the other alert's cleared condition.

## [1.0.0] - 2026-08-07

### Added
- Initial public release.
- Real-time battery percentage and charging status display.
- Automatic refresh every 30 seconds and manual refresh button.
- High (95%) and Low (15%) threshold alerts with voice, beep, and toast notification.
- Dynamic system tray icon (color-coded by battery level).
- Settings window for configuring thresholds.
- Auto-start registration with Windows.