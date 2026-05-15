# Win Key Activation Variants Design

## Context

GhostThrough currently treats the selected keyboard activation key as a hold trigger. Pressing the key starts the activation timer, and Ghost Mode activates when the timer reaches `ActivationDelayMs`. For the default `Left Win` key, the hook suppresses the physical Win key while Ghost Mode is active or during short suppression windows, and it has handoff logic that replays Win shortcuts if the user presses another key during the activation hold.

The requested change is to keep the existing Win behavior available and add a second Win-specific choice. The activation key picker should contain both:

- `Win standard` - the existing behavior, where holding Win for the configured delay activates Ghost Mode.
- `Win reverse` - the new behavior, where a short Win press toggles Ghost Mode and holding Win for the configured delay performs the standard Windows Win-key action.

## Scope

In scope:

- Add separate user-selectable activation-key entries for standard Win behavior and reverse Win behavior.
- Preserve the current standard Win behavior as an explicit option.
- Apply the reverse behavior only when the new `Win reverse` option is selected.
- Preserve the current activation behavior for all non-Win keyboard activation keys.
- Preserve the current mouse activation behavior.
- Reuse the existing `ActivationDelayMs` setting as the hold threshold for both Win variants.
- Keep Win shortcut handoff behavior so combinations such as `Win+H` are passed through instead of triggering Ghost Mode.

Out of scope:

- Adding a separate Win hold threshold setting.
- Changing opacity profiles, hotkeys, mouse activation, or non-Win activation keys.
- Making reverse behavior the default for existing users.

## Settings Model

The activation key currently stores a virtual-key code. `Win standard` and `Win reverse` need to be distinguishable even though both use the physical Win key. The implementation should add an explicit persisted activation-key behavior field or an equivalent internal enum, for example:

- `Standard` - existing behavior for Win and all other supported keys.
- `WinReverse` - reverse behavior for the Win key only.

Existing settings without this field must migrate to `Standard` so current users keep the same behavior after upgrade.

The tray menu should present two Win entries in the keyboard activation key list:

- `Win standard`
- `Win reverse`

Both entries should map to the same physical Win key for hook matching, but they should set different activation behavior in the controller/settings.

## Behavior

When `Win standard` is selected:

1. On Win key down, GhostThrough follows the existing activation flow.
2. Holding Win until `ActivationDelayMs` activates Ghost Mode for the window under the cursor.
3. Short Win press behavior remains as it is today.
4. Existing Win shortcut handoff remains unchanged.

When `Win reverse` is selected:

1. On Win key down, GhostThrough suppresses the physical Win key event and starts the existing activation timer.
2. If the Win key is released before the timer reaches `ActivationDelayMs`, GhostThrough stops the timer and toggles Ghost Mode:
   - if Ghost Mode is inactive, activate it for the window under the cursor;
   - if Ghost Mode is active, deactivate it.
3. If the timer reaches `ActivationDelayMs` while Win is still held, GhostThrough does not activate Ghost Mode. Instead, it sends a synthetic standard Win press/release through `SendInput`, causing the Start menu or normal Windows Win behavior to happen at the threshold.
4. When the user releases the physical Win key after the threshold already fired, GhostThrough suppresses that release so there is no duplicate or stray Win behavior.
5. If another key is pressed while Win is held before the threshold, GhostThrough cancels the pending Ghost toggle and preserves the existing keyboard handoff path, replaying the full Win+key shortcut when needed.

When keyboard activation uses any non-Win key, or when mouse activation is selected, the current behavior remains unchanged.

## Architecture

The change should be isolated behind an activation-key behavior value so the existing activation state machine remains stable for all current inputs.

`ActivationKeyCatalog` should expose two user-facing Win choices while still allowing the hook to resolve both choices to the physical Win virtual-key code. The display name should make the difference obvious: `Win standard` for the current behavior and `Win reverse` for the new behavior.

`SettingsManager` and `Models.Settings` should persist the selected behavior and migrate missing values to `Standard`. This prevents older settings files from silently switching to reverse behavior.

`TrayMenuController` should save and restore both the physical key code and the behavior choice. The checked menu item should distinguish `Win standard` from `Win reverse`.

`KeyboardHook` remains responsible for low-level input suppression and replay. It should use the controller/host setting to decide whether Win is in standard or reverse mode. Standard mode should continue through the existing path. Reverse mode should route Win events to the new short-press/timer behavior.

`ActivationStateManager` should expose explicit methods for the Win reverse path rather than overloading the existing hold path implicitly. These methods should reuse the existing activation timer and `ActivationDelayMs`, but interpret the timer differently for reverse Win:

- key release before timer: toggle Ghost Mode;
- timer fired while held: request standard Win behavior instead of Ghost activation.

`GhostController` remains the coordinator for activating and deactivating windows. It should provide the action used by the reverse short-press toggle and the callback used when the timer decides to pass standard Win behavior through.

`NativeMethods` can continue to provide `SendInput` structures and constants. If a helper is added for sending the standalone Win key, it should follow the same injected-input marker pattern already used by keyboard handoff replay.

## Data Flow

Selecting `Win standard`:

1. The tray menu writes the physical Win key and `Standard` behavior to settings.
2. `GhostController` exposes the physical Win key to `KeyboardHook`.
3. Win key events follow the existing long-hold activation flow.

Selecting `Win reverse`:

1. The tray menu writes the physical Win key and `WinReverse` behavior to settings.
2. `GhostController` exposes the physical Win key and reverse behavior to `KeyboardHook`.
3. Win key events follow the reverse flow below.

Short press in `Win reverse`:

1. `KeyboardHook.ProcessActivationKey` sees Win down and suppresses it.
2. `ActivationStateManager` starts the timer and marks Win as held.
3. `KeyboardHook.ProcessActivationKey` sees Win up before the timer fires and suppresses it.
4. `ActivationStateManager` stops the timer and asks `GhostController` to toggle Ghost Mode.
5. `GhostController` activates the window under the cursor or deactivates the current ghosted window.

Long hold in `Win reverse`:

1. `KeyboardHook.ProcessActivationKey` sees Win down and suppresses it.
2. `ActivationStateManager` starts the timer.
3. The timer reaches `ActivationDelayMs` while Win is still held.
4. `ActivationStateManager` records that the timer fired and asks `GhostController` or `KeyboardHook` to send standard Win behavior.
5. A synthetic Win down/up sequence is injected and ignored by GhostThrough's hook because it carries `INJECTED_BY_US`.
6. The later physical Win up is suppressed.

Win shortcut handoff in either Win mode:

1. Win down is suppressed and the relevant timer/state starts.
2. Another key is pressed before the reverse threshold or during the standard activation hold.
3. The existing handoff callback cancels pending activation state.
4. The existing replay mechanism sends Win+other key and suppresses the physical duplicate where required.

## Error Handling

If Ghost activation fails in `Win reverse` because there is no valid target window or the target is ignored, the short press should not fall back to standard Win behavior. It should behave like a failed Ghost activation under the current model: no window is ghosted and the suppressed short Win press remains consumed.

If synthetic Win injection fails or sends zero inputs, the failure should be logged with `DebugLogger`. The physical Win key should remain suppressed for that cycle to avoid unpredictable duplicate behavior.

Handoff cancellation should clear all transient Win-reverse state so later Win presses start cleanly.

If settings contain an unknown behavior value, it should normalize to `Standard`.

## Testing

Regression coverage should be added to `KeyboardHookRegressionTest.cs` for the low-level hook behavior, settings migration, and activation state transitions:

- existing settings without a behavior field load as `Win standard` when the key is Win;
- selecting `Win standard` preserves the current long-hold Ghost activation behavior;
- selecting `Win reverse` makes a short `Left Win` press toggle Ghost activation instead of waiting for a long hold;
- long `Left Win` hold in `Win reverse` triggers synthetic standard Win behavior at `ActivationDelayMs` and does not activate Ghost Mode;
- physical Win key-up after long-hold pass-through in `Win reverse` is suppressed;
- `Win+other key` during the threshold still uses the existing handoff replay path;
- non-Win activation keys still use the existing behavior;
- unknown persisted behavior values normalize to `Standard`.

Build verification should include:

```powershell
dotnet build GhostThrough.csproj -c Release
dotnet build KeyboardHookRegressionTest.csproj -c Release
bin\Release\net8.0-windows\KeyboardHookRegressionTest.exe
```
