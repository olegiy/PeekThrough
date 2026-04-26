# Tray Tooltip Version Design

## Goal

When the user hovers over the GhostThrough tray icon, Windows should show the application name together with the product version and build timestamp.

Expected tooltip format:

```text
GhostThrough 1.0.0 (2026-04-26 14:30)
```

## Current Behavior

`TrayMenuController` currently sets `NotifyIcon.Text` to the hardcoded string `GhostThrough Ghost Mode`.

`GhostThrough.csproj` already defines version metadata:

- `Version`: product version, currently `1.0.0`
- `AssemblyVersion`: currently `1.0.0.0`
- `FileVersion`: build timestamp in `yyyy.M.d.HHmm`
- `InformationalVersion`: build timestamp in `yyyy-MM-dd HH:mm`

## Design

`TrayMenuController` will set the tray icon tooltip from runtime assembly metadata instead of a hardcoded behavior label.

The tooltip builder will:

1. Read the executing assembly name and version.
2. Format the product version as `major.minor.patch`, avoiding the fourth assembly version component.
3. Read `AssemblyInformationalVersionAttribute` for the build timestamp.
4. Return `GhostThrough <version> (<timestamp>)` when both values are available.
5. Fall back to `GhostThrough <version>` if the timestamp is unavailable.
6. Fall back to `GhostThrough` if version metadata cannot be read.

The current project metadata means the first implementation will show:

```text
GhostThrough 1.0.0 (yyyy-MM-dd HH:mm)
```

## Scope

This change only affects the Windows tray icon hover tooltip. It does not change the tray menu labels, the in-app activation tooltip, build version generation, settings, activation behavior, or release packaging.

## Error Handling

The tooltip formatting must not throw during tray initialization. Any missing metadata should produce a short fallback string.

The formatted string must remain short enough for `NotifyIcon.Text`.

## Testing

Add a focused regression check for the tooltip formatter if the implementation extracts it into a testable method. At minimum, build both projects:

```powershell
dotnet build GhostThrough.csproj -c Release
dotnet build KeyboardHookRegressionTest.csproj -c Release
```
