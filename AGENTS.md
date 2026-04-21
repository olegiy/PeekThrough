# GhostThrough — Project Context

## Overview
GhostThrough is a C# WinForms tray utility for Windows that makes the window under the cursor semi-transparent and click-through after a deliberate activation hold. Ghost Mode remains active after the initial long press and can be cancelled with a short activation-key press or `Esc`.

## Architecture
- **Program.cs** — Entry point, single-instance guard, settings-path resolution and legacy-settings migration
- **AppContext.cs** — Composes runtime services, hooks, controller, and persisted settings
- **TrayMenuController.cs** — Owns the tray icon and tray menu actions
- **GhostController.cs** — Main coordinator for activation/deactivation flow, profile changes, tooltip updates, sound feedback
- **ActivationStateManager.cs** — Activation timer, suppress window, activation state machine for keyboard and mouse modes
- **WindowTransparencyManager.cs** — Applies/restores layered transparent window styles and tracks modified windows
- **TooltipService.cs** — Small tooltip form shown on activation/profile changes
- **KeyboardHook.cs** — Low-level keyboard hook, fires activation key events
- **MouseHook.cs** — Low-level mouse hook, supports middle/right/X1/X2 button activation
- **ProfileManager.cs** — Manages opacity profiles and current active profile
- **OpacityProfilePresets.cs** — Default opacity preset list and legacy-profile normalization
- **HotkeyManager.cs** — Hardcoded `Ctrl+Shift+Up/Down` profile cycling hotkeys
- **SettingsManager.cs** — Loads/saves v2 JSON settings and migrates legacy line-based settings
- **ActivationKeyCatalog.cs** — Allowed activation-key list and display names used by the tray menu
- **ActivationTypeExtensions.cs** — Conversion helpers between persisted setting values and runtime activation enums
- **IActivationHost.cs** — Shared contract used by keyboard and mouse hooks
- **NativeMethods.cs** — P/Invoke declarations (Win32 API: SetWindowLong, GetWindowLong, SetLayeredWindowAttributes, etc.)
- **DebugLogger.cs** — Simple file-based debug logger (writes to `ghostthrough_debug.log`)
- **KeyboardHookRegressionTest.cs** — Standalone regression test for keyboard hook ordering and modifier-key normalization

## Key Concepts
- Ghost Mode = WS_EX_LAYERED style + SetLayeredWindowAttributes for alpha transparency
- Click-through = WS_EX_TRANSPARENT on the ghosted window so mouse input reaches windows behind it
- Activation types: Keyboard (default: Left Win) or Mouse buttons
- Keyboard activation uses a 1 second hold; after activation, Ghost Mode stays active until cancelled
- Mouse activation uses a 1 second hold; releasing the selected mouse button deactivates Ghost Mode
- `Esc` deactivates Ghost Mode immediately; `Ctrl+Shift+Up/Down` switches opacity profiles
- Modifier-only activation keys (`Ctrl`, `Shift`, `Alt`) are intentionally rejected to avoid breaking global shortcuts in other apps
- Settings stored in `%APPDATA%/GhostThrough/settings.json` as real JSON (v2); old line-based settings are migrated automatically with `.bak` backup
- On first run after the rename, settings are copied from `%APPDATA%/PeekThrough/settings.json` if the new GhostThrough settings file does not exist yet
- Single-instance via named Mutex "GhostThroughApp"

## Build
No `.csproj` file is present. Build with `compile.bat` or call the .NET Framework compiler directly:
```bash
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /out:GhostThrough.exe /win32icon:resources\icons\icon.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Program.cs NativeMethods.cs KeyboardHook.cs MouseHook.cs GhostController.cs ActivationStateManager.cs WindowTransparencyManager.cs TooltipService.cs SettingsManager.cs ProfileManager.cs OpacityProfilePresets.cs HotkeyManager.cs DebugLogger.cs IActivationHost.cs ActivationKeyCatalog.cs ActivationTypeExtensions.cs AppContext.cs TrayMenuController.cs Models\Settings.cs Models\Profile.cs Models\GhostWindowState.cs
```

Regression test build/run:
```bash
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:KeyboardHookRegressionTest.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll KeyboardHookRegressionTest.cs NativeMethods.cs KeyboardHook.cs MouseHook.cs GhostController.cs ActivationStateManager.cs WindowTransparencyManager.cs TooltipService.cs SettingsManager.cs ProfileManager.cs OpacityProfilePresets.cs HotkeyManager.cs DebugLogger.cs IActivationHost.cs ActivationKeyCatalog.cs ActivationTypeExtensions.cs AppContext.cs TrayMenuController.cs Models\Settings.cs Models\Profile.cs Models\GhostWindowState.cs && KeyboardHookRegressionTest.exe
```

## Conventions
- Language: C# with Russian comments
- Framework: .NET Framework + WinForms
- No package manager, no solution file
- A small standalone regression test exists, but there is no broad automated test suite yet
- Resources: `resources/icons/icon.ico`, `icon.svg`, `icon.cdr`
- Manual QA checklist and rename-focused smoke plan: `docs/qa/ghostthrough-rename-test-plan.md`

## Current Plan
- Stabilize UI behavior around tray interactions and Ghost Mode activation/deactivation flows
- Expand automated and manual test coverage for Ghost Mode scenarios

## Known Issues
- `GhostThrough.exe` and `GhostThrough.pdb` are tracked in repo (should be in .gitignore)
- `ghostthrough_debug.log` is tracked (covered by *.log in .gitignore but may be committed already)
- Documentation outside `README.md` / `README.ru.md` may lag behind behavior if future hook/activation changes are not mirrored here
