# Refresh — Windows Advertising ID QA

Refresh is a standalone Windows desktop utility for inspecting installed/running applications and verifying Windows Advertising ID changes.

## Features
- Discovers installed Microsoft Store (AppX/MSIX) packages for the current Windows user.
- Lists running desktop processes and provides application diagnostics.
- Reads the Windows Advertising ID through the documented `Windows.System.UserProfile.AdvertisingManager.AdvertisingId` read-only API.
- Displays a short SHA-256 fingerprint instead of exposing the raw advertising identifier in the UI.
- Captures a before-reset fingerprint and compares it with a later reading to verify whether Windows generated a different ID.
- Opens the official Windows advertising privacy settings.
- Generates separate local QA GUIDs for applications that explicitly support test-ID configuration. These are not Windows Advertising IDs and are not injected into other apps.
- Builds a self-contained Windows x64 executable and a click-through installer.

## Windows Advertising ID reset workflow
1. Open the **Advertising ID QA** tab and select **Read current Windows Advertising ID**.
2. Select **Capture current value as baseline**.
3. Open Windows privacy settings.
4. Turn the advertising ID option off and then on again. Windows documents that turning it back on resets the ID.
5. Return to Refresh and select **Read again and compare with baseline**.
6. A different fingerprint confirms that the Windows API now returns a different ID.

The reset is deliberately user-controlled. The Windows Advertising ID is a per-user, per-device value shared by apps that use this Windows API; it is not a separate per-app identifier. Turning it off/on can affect multiple apps.

## Important technical boundary
Microsoft documents the Advertising ID API as read-only. Refresh does not assign arbitrary values, inject code into other processes, modify process memory, rotate identifiers to evade ad-network controls, or interfere with ad delivery.

A changed Windows Advertising ID does **not** prove that a running third-party app's private advertising SDK has discarded a cached value or reloaded its ad session. An arbitrary closed-source app cannot be forced to do that through a supported universal Windows API. Live SDK refresh requires an application/provider-supported interface. The local QA GUID is only for an app or SDK test configuration that explicitly accepts it.

## Build
Push to `main` or manually run **Build Refresh Windows Installer** under GitHub Actions. The workflow publishes:
- `Refresh.exe` — self-contained x64 app
- `Refresh_Setup.exe` — Inno Setup installer

Download both from the workflow run's **Artifacts** section.
