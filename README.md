# Refresh — Windows Rewarded Ads QA

Refresh is a standalone Windows desktop utility for inspecting installed/running applications and opening the official Windows advertising privacy settings.

## What it does
- Discovers installed Microsoft Store (AppX/MSIX) packages for the current Windows user.
- Lists running desktop processes.
- Lets you select an application and generate a local diagnostic report.
- Opens Windows' official advertising ID privacy settings.
- Builds a self-contained Windows x64 executable and a click-through installer.

## Important technical boundary
Windows does not provide a universal external API for replacing an advertising identifier already cached inside another running application's memory, clearing a private ad SDK session, or forcing an arbitrary closed-source app to reload a rewarded ad. Refresh does not inject into processes, modify another process's memory, rotate identifiers to evade ad-network controls, or claim to force new production inventory.

Resetting the Windows advertising ID is a Windows-level privacy action, not a per-application action, and may affect multiple apps. Use the Windows settings page opened by Refresh to review the available controls. Live rewarded-ad reload requires the app/SDK to expose a supported mechanism.

## Build
Push to `main` or manually run **Build Refresh Windows Installer** under GitHub Actions. The workflow publishes:
- `Refresh.exe` — self-contained x64 app
- `Refresh_Setup.exe` — Inno Setup installer

Download both from the workflow run's **Artifacts** section.
