# GhostThrough

GhostThrough is a lightweight Windows tray utility that makes the window under the cursor semi-transparent and click-through after a deliberate activation hold. It is built for quick "peek behind this window" workflows without minimizing or rearranging anything.

Languages: [English](README.md) | [Русский](README.ru.md)

Synchronization note: `README.md` and `README.ru.md` should be updated together.

Project note: the original idea was inspired by Luke Payne's Peek Through project:
http://www.lukepaynesoftware.com/projects/peek-through/

[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-0078d7.svg)](https://www.microsoft.com/windows)
[![.NET Framework: 4.0](https://img.shields.io/badge/.NET_Framework-4.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework/net40)

## Overview

GhostThrough lives in the notification area and waits for an activation gesture.

When Ghost Mode activates, the root window under the cursor receives:

- `WS_EX_LAYERED` for alpha transparency
- `WS_EX_TRANSPARENT` so mouse input passes through it

The app ignores shell windows such as the desktop and taskbar, restores modified window styles on exit, and stores settings in `%APPDATA%\GhostThrough\settings.json`.

## Current Behavior

Activation uses a configurable hold delay. After that delay, the selected activation mode controls whether Ghost Mode stays on or only lasts while the activation input remains pressed.

### Keyboard mode

- Hold the activation key for the configured delay to activate Ghost Mode.
- In `Hold` mode, releasing the key keeps Ghost Mode active.
- In `Click` mode, releasing the key deactivates Ghost Mode.
- In `Hold` mode, press the activation key briefly again to deactivate Ghost Mode.
- Press `Esc` at any time during Ghost Mode for an immediate exit.

### Mouse mode

- Hold the selected mouse button for the configured delay to activate Ghost Mode.
- Releasing the selected mouse button deactivates Ghost Mode.
- If another mouse button is pressed before the selected one, activation is blocked.
- If another mouse button is pressed while the selected one is being held, that activation attempt is also blocked.

### While Ghost Mode is active

- Mouse input goes to the windows behind the ghosted window.
- `Ctrl+Shift+Up` and `Ctrl+Shift+Down` switch opacity profiles.
- If a profile changes while Ghost Mode is active, transparency is reapplied immediately.
- A small tooltip near the cursor shows the active state and current profile.
- A short beep plays on activation and deactivation.

## Activation Options

GhostThrough supports two activation input methods:

- Keyboard activation
- Mouse activation

It also supports two activation behavior modes:

- `Hold` - after the hold delay, Ghost Mode remains active until a short activation-key press or `Esc`.
- `Click` - after the hold delay, Ghost Mode remains active only until the activation key is released.

### Keyboard activation

Selectable activation keys are limited to non-modifier keys plus the Windows keys, so the app does not break common shortcuts in other programs.

Current tray-menu choices:

- `Left Win`, `Right Win`
- `Caps Lock`, `Tab`, `Space`, `Escape`, `Tilde (\`~)`
- `Insert`, `Delete`, `Home`, `End`, `Page Up`, `Page Down`
- `0`-`9`
- `F1`-`F12`

Modifier-only activation keys are intentionally rejected and normalized back to `Left Win`:

- `Ctrl`
- `Shift`
- `Alt`

### Mouse activation

Supported activation buttons:

- Middle button
- Right button
- X1 button
- X2 button

## Opacity Profiles

GhostThrough now ships with nine built-in opacity presets and persists the active preset in settings:

- `10%` (`26/255`)
- `20%` (`51/255`)
- `30%` (`76/255`)
- `40%` (`102/255`)
- `50%` (`128/255`)
- `60%` (`153/255`)
- `70%` (`178/255`)
- `80%` (`204/255`)
- `90%` (`230/255`)

The default active profile is `10%`.

Older three-profile defaults (`min` / `med` / `max`) are normalized automatically to the new nine-profile set, preserving the closest opacity.

## Tray Menu

There is no main window. The tray icon is the primary UI and currently exposes:

- `Activation Key`
- `Activation Method`
- `Activation Mode`
- `Activation Hold Time`
- `Exit`

`Activation Method` opens a submenu for:

- `Keyboard`
- `Mouse (Middle Button)`
- `Mouse (Right Button)`
- `Mouse (X1 Button)`
- `Mouse (X2 Button)`

`Activation Mode` opens a submenu for:

- `Hold`
- `Click`

`Activation Hold Time` opens a submenu for delay values from `0.3 s` to `1.5 s`.

## Architecture

The app is a small WinForms/Win32 utility split into focused classes:

- `Program.cs` - entry point, single-instance guard, application startup/shutdown
- `AppContext.cs` - composes runtime services, hooks, controller, and persisted settings
- `TrayMenuController.cs` - owns the `NotifyIcon` and tray menu actions
- `GhostController.cs` - coordinates activation, deactivation, tooltips, sounds, and profile changes
- `ActivationStateManager.cs` - tracks hold timers, suppression windows, and activation state
- `WindowTransparencyManager.cs` - applies/restores layered transparent window styles
- `KeyboardHook.cs` - low-level keyboard hook, activation-key handling, `Esc`, and profile hotkeys
- `MouseHook.cs` - low-level mouse hook with mouse-button conflict tracking
- `ProfileManager.cs` - manages active opacity profile and cycling
- `OpacityProfilePresets.cs` - shared default presets and legacy-profile normalization helpers
- `HotkeyManager.cs` - hardcoded `Ctrl+Shift+Up/Down` profile switching
- `SettingsManager.cs` - JSON load/save plus legacy line-based settings migration
- `ActivationKeyCatalog.cs` - list of selectable activation keys and display names
- `ActivationTypeExtensions.cs` - conversion helpers for persisted activation-mode values
- `IActivationHost.cs` - small contract used by global input hooks
- `TooltipService.cs` - small floating tooltip form shown near the cursor
- `NativeMethods.cs` - Win32 API declarations and constants
- `DebugLogger.cs` - asynchronous file logger for `ghostthrough_debug.log`
- `KeyboardHookRegressionTest.cs` - small standalone regression test executable

## Settings Format

Settings are stored as JSON v2 in `%APPDATA%\GhostThrough\settings.json`:

```json
{
  "Version": 2,
  "Activation": {
    "Type": "keyboard",
    "KeyCode": 91,
    "MouseButton": 4,
    "ActivationDelayMs": 1000,
    "Mode": "hold"
  },
  "Profiles": {
    "List": [
      { "Id": "p10", "Name": "10%", "Opacity": 26 },
      { "Id": "p20", "Name": "20%", "Opacity": 51 }
    ],
    "ActiveId": "p10"
  },
  "Hotkeys": {
    "NextProfile": { "Ctrl": true, "Shift": true, "Alt": false, "Key": "Up" },
    "PrevProfile": { "Ctrl": true, "Shift": true, "Alt": false, "Key": "Down" }
  }
}
```

Notes:

- Old line-based settings are migrated automatically and the original file is backed up as `.bak`.
- On first run after the rename, settings are copied from `%APPDATA%\PeekThrough\settings.json` to `%APPDATA%\GhostThrough\settings.json` if the new file does not exist yet.
- Legacy three-profile defaults are upgraded automatically to the new nine-profile preset list.
- The `Hotkeys` section is persisted for compatibility, but profile hotkeys are currently hardcoded in `HotkeyManager.cs`.

## Build

The repository now includes SDK-style MSBuild project files targeting `net8.0-windows`, and the supported build paths are `compile.bat` and `run-regression-test.bat`.

### Quick build

```bat
compile.bat
```

The quick build publishes a self-contained single-file executable to `bin\publish\GhostThrough.exe`. That executable can be copied by itself, for example to Desktop.

### Manual build

```bat
dotnet build GhostThrough.csproj -c Release
```

`dotnet build` produces framework-dependent output in `bin\Release\net8.0-windows`. Keep `GhostThrough.exe`, `GhostThrough.dll`, `GhostThrough.deps.json`, and `GhostThrough.runtimeconfig.json` together when running that build output.

## Regression Test

The repository includes a standalone regression test for keyboard-hook and activation support code:

- `KeyboardHookRegressionTest.cs`

It currently verifies:

- activation-type string conversions
- activation-mode string conversions and keyboard `Click` behavior
- activation-key catalog exposure
- `IActivationHost` access to the controller activation key
- keyboard-hook ordering around activation key handling
- rejection of modifier-only activation keys
- queued debug-log flushing

Build and run it with:

```bat
dotnet build KeyboardHookRegressionTest.csproj -c Release
bin\Release\net8.0-windows\KeyboardHookRegressionTest.exe
```

Expected result:

```text
PASS
```

## Logging

- Debug logs are written to `ghostthrough_debug.log` next to the executable.
- Set environment variable `GHOSTTHROUGH_LOG_LEVEL=INFO` to reduce verbose debug logging.
- `PEEKTHROUGH_LOG_LEVEL` is still accepted as a backward-compatible fallback.

## Installation

### End users

1. Download `GhostThrough.exe` from Releases or build it locally.
2. Launch the executable.
3. Optionally add a shortcut to Windows Startup.

### Developers

1. Clone the repository.
2. Build with `compile.bat`.
3. Run `bin\publish\GhostThrough.exe`.

## Known Limitations

- The app is Windows-only and depends on low-level global hooks plus Win32 style manipulation.
- The repository currently tracks generated files such as `GhostThrough.exe`, `GhostThrough.pdb`, and `ghostthrough_debug.log`.
- There is still no installer, updater, or broad automated test suite.
- The settings layer now uses `DataContractJsonSerializer`, and the projects are on SDK-style `net8.0-windows`.
- No `LICENSE` file is currently tracked in the repository root.

Created by [olegiy](https://github.com/olegiy)
