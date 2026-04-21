# PeekThrough Refactoring Design

Date: 2026-04-21
Topic: Refactoring core application structure without changing user-visible behavior

## Summary

This refactoring keeps PeekThrough behavior unchanged while improving maintainability, reducing unnecessary hook recreation, lowering coupling between hooks and the controller, and removing synchronous file I/O from low-level input hot paths.

The selected scope corresponds to the previously approved "Variant B":

1. Remove duplication and excessive orchestration from `Program.cs`.
2. Reconfigure activation settings in place instead of recreating low-level hooks.
3. Replace direct `GhostController` dependency in hooks with a minimal interface.
4. Move activation callback execution out of `ActivationStateManager` locks.
5. Make `DebugLogger` buffered and asynchronous.
6. Apply small structural cleanup around activation catalogs, tray menu code, and settings helpers.

## Goals

1. Keep application behavior identical for keyboard and mouse activation flows.
2. Reduce fragility in startup and reconfiguration code.
3. Make hook classes easier to reason about and test in isolation.
4. Remove blocking disk writes from keyboard and mouse hook callback paths.
5. Keep compatibility with the current build flow based on `compile.bat`.

## Non-Goals

1. No migration to .NET 8 or a new runtime.
2. No redesign of Ghost Mode semantics.
3. No removal of legacy v1 settings migration.
4. No changes to the external settings file shape beyond internal helper usage.
5. No broad dependency injection framework introduction.

## Current Problems

### `Program.cs` owns too much orchestration

`Program.cs` currently loads settings, constructs controllers and hooks, builds tray menus, handles activation settings changes, and manages shutdown. This mixes bootstrap logic with runtime behavior.

### Reconfiguration recreates hooks unnecessarily

Changing activation type or activation key currently disposes and recreates `GhostController`, `KeyboardHook`, and `MouseHook`. This is more invasive than required because:

1. `KeyboardHook` already reads the current activation key dynamically.
2. `MouseHook` already supports changing the selected mouse button via `SetSelectedMouseButton`.
3. The activation type already exists as mutable state in `ActivationStateManager`.

### Hooks are tightly coupled to `GhostController`

Both hook classes reach into `GhostController` for multiple unrelated concerns: activation key lookup, suppression state, ghost mode state, hotkey processing, and deactivation requests. This makes the hook layer depend on too much controller surface area.

### Activation callback executes under lock

`ActivationStateManager.OnActivationTimerTick` invokes `OnGhostModeShouldActivate` while holding its lock. The callback performs non-trivial work such as Win32 calls, tooltip updates, and sound playback. Holding the state lock during that work increases the chance of input-state contention and future bugs.

### Logger performs synchronous disk writes on hot path

`DebugLogger.Log` currently uses `File.AppendAllText` for every message. Since low-level input hooks log frequently, this introduces avoidable blocking I/O on a sensitive path.

## Proposed Architecture

### High-level structure

The refactoring introduces a thin application composition layer and narrows the contract between hooks and the runtime controller.

```text
Program.cs
  -> AppContext
       -> GhostController
       -> KeyboardHook
       -> MouseHook
       -> SettingsManager
       -> ProfileManager
  -> TrayMenuController

KeyboardHook / MouseHook
  -> IActivationHost
       -> GhostController
            -> ActivationStateManager
            -> WindowTransparencyManager
            -> TooltipService
            -> HotkeyManager
```

### New types

#### `IActivationHost`

Minimal runtime contract that the hooks need. It replaces direct hook knowledge of the full `GhostController` type.

Expected members:

1. `void OnActivationInputDown()`
2. `void OnActivationInputUp()`
3. `void OnOtherInputBeforeActivation()`
4. `bool ShouldSuppressActivationKey { get; }`
5. `bool IsGhostModeActive { get; }`
6. `int ActivationKeyCode { get; }`
7. `bool ProcessHotkey(int vkCode, bool isDown, bool ctrl, bool shift, bool alt)`
8. `void RequestDeactivate()`

The interface stays intentionally small and targeted to hook behavior.

#### `ActivationKeyCatalog`

Static catalog that owns:

1. The list of selectable activation keys.
2. Human-readable display names for the tray menu.

This removes menu-specific key metadata from `Program.cs`.

#### `TrayMenuController`

Owns `NotifyIcon`, tray menu creation, and activation-setting menu actions. It becomes the only place responsible for runtime tray UI.

#### `AppContext`

Owns application-wide runtime objects and their shutdown order. It provides a small API for activation reconfiguration and settings persistence.

## Detailed Design

### 1. Thin bootstrap in `Program.cs`

`Program.cs` becomes responsible only for:

1. Single-instance mutex.
2. Settings path calculation.
3. WinForms startup calls.
4. Creating `AppContext`.
5. Creating `TrayMenuController`.
6. Entering `Application.Run()`.
7. Coordinated shutdown.

All runtime behavior moves out of `Program.cs`.

### 2. In-place activation reconfiguration

Instead of recreating the controller and hooks, runtime activation updates will modify the existing live objects.

`AppContext.Reconfigure(...)` will:

1. Deactivate Ghost Mode if it is currently active.
2. Normalize and store the activation key.
3. Update `GhostController.ActivationKeyCode`.
4. Update `GhostController.CurrentActivationType`.
5. Update `MouseHook.SetSelectedMouseButton(...)`.
6. Persist the updated settings.
7. Reset transient activation state if needed so stale pressed-state does not survive a mode change.

This preserves behavior while removing churn and duplicated lifecycle code.

### 3. Hook decoupling through `IActivationHost`

`KeyboardHook` and `MouseHook` will depend only on the new interface.

This keeps existing behavior but narrows knowledge of the controller to the minimum needed for:

1. Activation input events.
2. Suppression decisions.
3. Ghost mode escape handling.
4. Global hotkey routing.

No business logic moves into the hooks; they remain input translation components.

### 4. ActivationStateManager callback discipline

`ActivationStateManager` will stop invoking activation handlers while holding its internal lock.

Updated flow for timer expiration:

1. Take the lock.
2. Determine whether activation is still requested.
3. Mark an internal transient flag such as `activationPending`.
4. Release the lock.
5. Invoke the activation callback.
6. Reacquire the lock.
7. If activation succeeded, finalize `_ghostModeActive` and `_timerFired`.
8. If activation failed, clear pending state and apply temporary suppression for keyboard activation.

This preserves state correctness while avoiding expensive work under lock.

#### Race-handling requirement

The updated design must prevent a key-up or mouse-up event from deactivating a mode that is only pending and not yet active. The intermediate pending state must be treated separately from `_ghostModeActive`.

### 5. Buffered asynchronous logging

`DebugLogger` will be changed to a producer-consumer model:

1. `Log(...)` formats and enqueues a line.
2. A background writer thread flushes queued messages to `peekthrough_debug.log`.
3. A flush method is called during shutdown.

Additional requirements:

1. Logging failures still fail silently.
2. The implementation remains dependency-free.
3. Existing call sites continue to compile with minimal edits.
4. Log verbosity can be gated by a simple setting such as `PEEKTHROUGH_LOG_LEVEL`.

Default behavior remains debug-friendly, but hook callbacks no longer wait on disk I/O.

### 6. Small API and cleanup changes

#### `GhostController`

Changes:

1. Implement `IActivationHost`.
2. Keep activation methods but adapt names to the interface contract.
3. Remove public properties that expose internal managers when they are not needed externally.
4. Extract repeated tooltip formatting into one helper.

#### `MouseHook`

Changes:

1. Keep current runtime-selected button support.
2. Extract mouse-message decoding into a helper method.
3. Extract repeated `SynchronizationContext.Post` error-handling code into one helper.

#### `KeyboardHook`

Changes:

1. Switch constructor dependency from `GhostController` to `IActivationHost`.
2. Extract repeated `SynchronizationContext.Post` handling into a helper.
3. Keep hotkey, activation, and escape behavior unchanged.

#### Activation type conversion

The persisted JSON field remains string-based (`"keyboard"` / `"mouse"`) for compatibility. Conversion helpers will map this to `ActivationInputType` inside runtime code instead of scattering string comparisons across the codebase.

## File-Level Plan

### New files

1. `IActivationHost.cs`
2. `ActivationKeyCatalog.cs`
3. `TrayMenuController.cs`
4. `AppContext.cs`
5. `ActivationTypeExtensions.cs` or equivalent helper file for string/enum conversion

### Updated files

1. `Program.cs`
2. `GhostController.cs`
3. `ActivationStateManager.cs`
4. `KeyboardHook.cs`
5. `MouseHook.cs`
6. `DebugLogger.cs`
7. `compile.bat`
8. `.gitignore`
9. `KeyboardHookRegressionTest.cs` if constructor wiring requires it

## Testing Strategy

### Required verification

1. Build with `compile.bat`.
2. Build and run `KeyboardHookRegressionTest.exe`.
3. Manual smoke test:
   1. Keyboard activation with default Left Win.
   2. Short press cancellation after activation.
   3. `Esc` deactivation while ghost mode is active.
   4. Mouse activation mode using each supported mouse button.
   5. Profile switching with `Ctrl+Shift+Up/Down`.
   6. Tray menu changes for activation key and activation method.

### Focus areas for regression

1. Start-menu suppression during keyboard activation.
2. No stale activation state after changing activation mode.
3. No loss of settings persistence.
4. No crash or deadlock during rapid press/release around the 1-second timer threshold.

## Risks and Mitigations

### Risk: activation race around pending state

Moving callback execution outside the lock introduces the main behavioral risk. Mitigation:

1. Add an explicit pending flag.
2. Keep the state transitions minimal and explicit.
3. Verify fast press-release sequences with the regression test and manual checks.

### Risk: logger loses messages on shutdown

Mitigation:

1. Add explicit `Flush()`.
2. Call it during coordinated shutdown.
3. Keep the writer thread background-only but drain the queue before exit.

### Risk: in-place reconfiguration leaves stale input state

Mitigation:

1. Reset transient activation state during reconfiguration.
2. Always deactivate active ghost mode before switching activation configuration.

## Rollout Sequence

Recommended implementation order:

1. Clean `.gitignore` and tracked generated artifacts.
2. Extract `ActivationKeyCatalog`.
3. Introduce `IActivationHost` and adapt hooks.
4. Simplify `GhostController` public surface.
5. Refactor `ActivationStateManager` lock/callback flow.
6. Introduce `AppContext` and in-place reconfiguration.
7. Extract `TrayMenuController` and shrink `Program.cs`.
8. Refactor `DebugLogger` to buffered async writes.
9. Add activation type conversion helpers.
10. Update build script and regression test wiring.

## Success Criteria

The refactoring is complete when:

1. User-visible behavior remains unchanged.
2. Activation updates no longer recreate low-level hooks.
3. Hooks no longer depend on the full `GhostController` type.
4. Activation callbacks do not execute under the state-manager lock.
5. Logging no longer performs synchronous file writes in hook callbacks.
6. The project still builds through the existing workflow and passes the regression test.
