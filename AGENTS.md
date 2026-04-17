# PeekThrough — Project Context

## Overview
PeekThrough is a C# WinForms utility for Windows that makes any window semi-transparent ("ghost mode") while a hotkey is held. Useful for seeing through overlaid windows without minimizing them.

## Architecture
- **Program.cs** — Entry point, tray icon, settings load/save, activation key/mode switching
- **GhostLogic.cs** — Core state machine: activate/deactivate ghost mode, manage transparency via Win32 API
- **KeyboardHook.cs** — Low-level keyboard hook, fires activation key events
- **MouseHook.cs** — Low-level mouse hook, supports middle/right/X1/X2 button activation
- **NativeMethods.cs** — P/Invoke declarations (Win32 API: SetWindowLong, GetWindowLong, SetLayeredWindowAttributes, etc.)
- **DebugLogger.cs** — Simple file-based debug logger (writes to `peekthrough_debug.log`)

## Key Concepts
- Ghost Mode = WS_EX_LAYERED style + SetLayeredWindowAttributes for alpha transparency
- Activation types: Keyboard (default: Left Win) or Mouse buttons
- Settings stored in `%APPDATA%/PeekThrough/settings.json` (key:value format, not real JSON)
- Single-instance via named Mutex "PeekThroughGhostModeApp"

## Build
No .csproj file found. Likely compiled as a single-file C# script or via command line:
```bash
csc /target:winexe /out:PeekThrough.exe Program.cs GhostLogic.cs KeyboardHook.cs MouseHook.cs NativeMethods.cs DebugLogger.cs /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll
```

## Conventions
- Language: C# with Russian comments
- Framework: .NET Framework + WinForms
- No tests, no package manager, no solution file
- Resources: `resources/icons/icon.ico`, `icon.svg`, `icon.cdr`

## Known Issues
- `PeekThrough.exe` and `PeekThrough.pdb` are tracked in repo (should be in .gitignore)
- `peekthrough_debug.log` is tracked (covered by *.log in .gitignore but may be committed already)
