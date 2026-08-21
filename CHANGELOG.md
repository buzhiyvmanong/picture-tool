# Changelog

All notable changes to PictureTool are documented in this file.

## [2.0.0] - 2026-08-21

### Fixed
- Auto-scroll no longer repeatedly stitches the same viewport when the page cannot advance
- Full-frame similarity detection stops capture when scrolling produces no real movement
- Auto-scroll falls back to real mouse-wheel input after synthetic PostMessage fails
- Reject false large advances caused by overlap search latching onto near-identical frames

### Changed
- Auto-scroll matching is stricter (no loose ControlledScroll "Added" path)

## [1.0.7] - 2026-08-20

### Added
- Squirrel-based auto-update for installed clients (download, apply, restart prompt)
- MSIX package and AppInstaller distribution channel
- CI automatic signing for EXE, MSIX, and Squirrel artifacts via GitHub Secrets
- Release scripts: `scripts/build-squirrel.ps1`, `scripts/build-msix.ps1`, `scripts/sign-artifacts.ps1`

## [1.0.6] - 2026-08-20

### Changed
- Welcome wizard now reappears on each version upgrade (`LastSeenWelcomeVersion`)
- Skip or "don't show again" dismisses the guide for the current version only

## [1.0.5] - 2026-08-20

### Added
- Multi-page welcome wizard on first launch (intro, quick start, features, tips)
- Usage guide accessible from main panel and tray menu at any time
- "Don't show again" option for the welcome screen

## [1.0.4] - 2026-08-20

### Added
- Optional Authenticode signing in `build.ps1` (see `docs/SIGNING.md`)
- Tray balloon notifications for copy/save/capture completion
- Hotkey conflict detection with alternative suggestions
- Settings: startup with Windows, update check toggle, history max items
- Export formats: PNG, JPEG, WebP
- Scroll capture directions: up, down, left, right
- CHANGELOG and GitHub Issue templates

### Fixed
- Startup crash when window icon path was invalid (v1.0.3)

## [1.0.3] - 2026-08-20

### Fixed
- Fix startup crash caused by invalid MainWindow icon resource path

## [1.0.2] - 2026-08-20

### Added
- App icon for exe, tray, and windows
- First-run welcome guide
- Startup update check against GitHub Releases

## [1.0.1] - 2026-08-20

### Added
- GitHub Actions automated release workflow
- Runtime guard with Chinese error messages
- README download instructions

## [1.0.0] - 2026-08-20

### Added
- Area capture, scroll capture, annotation, pin-to-screen
- OCR text extraction, capture history
- Global hotkeys and system tray integration

