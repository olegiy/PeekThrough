# PeekThrough

PeekThrough is a lightweight Windows tray utility that lets you make the window under the cursor semi-transparent by holding the mouse button down for one second, allowing you to view the contents of other windows and the desktop beneath it. It is designed for quick interaction with content behind a window without needing to minimize anything.

[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-0078d7.svg)](https://www.microsoft.com/windows)
[![.NET Framework: 4.0](https://img.shields.io/badge/.NET_Framework-4.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework/net40)

## Overview

PeekThrough runs in the notification area and stays idle until you trigger Ghost Mode.

When Ghost Mode activates, the root window currently under the cursor receives:

- `WS_EX_LAYERED` for alpha transparency
- `WS_EX_TRANSPARENT` so mouse clicks pass through it

The app ignores core shell windows such as the desktop and taskbar, restores modified window styles on exit, and keeps a small JSON settings file in `%APPDATA%\PeekThrough\settings.json`.

## Current Behavior

- Long press the activation input for 1 second to activate Ghost Mode.
- After Ghost Mode activates, you can release the activation key or button and it stays active until you cancel it.
- While Ghost Mode is active, normal mouse interaction goes to the windows behind the ghosted window.
- Press the activation key briefly again to deactivate Ghost Mode when keyboard activation is enabled.
- Press `Esc` at any time during Ghost Mode for an immediate exit.
- Use `Ctrl+Shift+Up` and `Ctrl+Shift+Down` to cycle opacity profiles.

## Activation Options

PeekThrough supports two activation modes:

- Keyboard activation
- Mouse activation

### Keyboard activation

Available keyboard activation keys are intentionally limited to non-modifier keys and the Windows keys. This prevents the app from breaking standard shortcuts in other programs such as `Ctrl+C`, `Ctrl+V`, and `Ctrl+Z`.

Current selectable keyboard keys include:

- `Left Win`, `Right Win`
- `Caps Lock`, `Tab`, `Space`, `Escape`, `` ` ``
- `Insert`, `Delete`, `Home`, `End`, `Page Up`, `Page Down`
- `0`-`9`
- `F1`-`F12`

Modifier-only activation keys are rejected and normalized back to `Left Win`:

- `Ctrl`
- `Shift`
- `Alt`

### Mouse activation

Supported mouse buttons:

- Middle button
- Right button
- X1 button
- X2 button

## Opacity Profiles

PeekThrough ships with nine built-in opacity presets and persists the currently selected one in settings:

- `10%`
- `20%`
- `30%`
- `40%`
- `50%`
- `60%`
- `70%`
- `80%`
- `90%`

The default active profile is `10%` opacity.

## Tray Menu

The tray icon exposes the current runtime configuration:

- `Activation Key`
- `Activation Method`
- `Exit`

There is no main window. The notification icon is the primary UI.

## Architecture

The application is a small WinForms/Win32 hybrid built from focused single-purpose classes:

- `Program.cs` - startup, tray menu, settings load/save, activation switching
- `GhostController.cs` - central coordinator for activation, deactivation, tooltips, and profile changes
- `ActivationStateManager.cs` - hold timers, activation state, suppression timing
- `WindowTransparencyManager.cs` - Win32 style changes and restoration for ghosted windows
- `KeyboardHook.cs` - low-level keyboard hook and keyboard hotkey handling
- `MouseHook.cs` - low-level mouse hook for mouse-based activation
- `ProfileManager.cs` - active opacity profile and profile cycling
- `HotkeyManager.cs` - `Ctrl+Shift+Up/Down` profile shortcuts
- `SettingsManager.cs` - JSON settings load/save and legacy settings migration
- `NativeMethods.cs` - P/Invoke declarations and Win32 constants
- `DebugLogger.cs` - file logging to `peekthrough_debug.log`

## Settings Format

Settings are stored in JSON v2 format:

```json
{
  "Version": 2,
  "Activation": {
    "Type": "keyboard",
    "KeyCode": 91,
    "MouseButton": 4
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

Legacy line-based settings are migrated automatically and backed up as `.bak`.

## Build

There is no `.csproj` or solution file in the repository. The supported build path in this repo is `compile.bat`.

### Quick build

```bat
compile.bat
```

### Manual build

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /out:PeekThrough.exe /win32icon:resources\icons\icon.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Program.cs NativeMethods.cs KeyboardHook.cs MouseHook.cs GhostController.cs ActivationStateManager.cs WindowTransparencyManager.cs TooltipService.cs SettingsManager.cs ProfileManager.cs OpacityProfilePresets.cs HotkeyManager.cs DebugLogger.cs Models\Settings.cs Models\Profile.cs Models\GhostWindowState.cs
```

## Regression Test

The repository includes a small standalone regression test for keyboard hook behavior:

- `KeyboardHookRegressionTest.cs`

It currently verifies:

- activation-state ordering inside `KeyboardHook`
- rejection of modifier-only activation keys

Run it with the .NET Framework compiler:

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:KeyboardHookRegressionTest.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll KeyboardHookRegressionTest.cs NativeMethods.cs KeyboardHook.cs MouseHook.cs GhostController.cs ActivationStateManager.cs WindowTransparencyManager.cs TooltipService.cs SettingsManager.cs ProfileManager.cs OpacityProfilePresets.cs HotkeyManager.cs DebugLogger.cs Models\Settings.cs Models\Profile.cs Models\GhostWindowState.cs && KeyboardHookRegressionTest.exe
```

Expected result:

```text
PASS
```

## Installation

### End users

1. Download `PeekThrough.exe` from the Releases page or build it locally.
2. Launch the executable.
3. Optionally add a shortcut to Windows Startup.

### Developers

1. Clone the repository.
2. Build with `compile.bat`.
3. Run `PeekThrough.exe`.

## Known Limitations

- The repository currently tracks generated files such as `PeekThrough.exe`, `PeekThrough.pdb`, and `peekthrough_debug.log`.
- The app relies on low-level global hooks and Win32 style manipulation, so it is Windows-only.
- There is no installer, updater, or full automated test suite yet.
- No `LICENSE` file is currently tracked in the repository root.

Created by [olegiy](https://github.com/olegiy)
