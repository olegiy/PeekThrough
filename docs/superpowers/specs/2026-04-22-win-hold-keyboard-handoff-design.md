# Win-Hold Keyboard Handoff Design

Date: 2026-04-22
Topic: Cancel keyboard activation and immediately drop Ghost Mode when another key is pressed during Win-hold

## Summary

This change makes keyboard activation yield cleanly back to Windows and the foreground app when the user starts using the keyboard for something else while still holding the activation key.

When the configured activation key is held and any non-activation key is pressed during that hold, GhostThrough will treat that as keyboard handoff. The pending activation must be cancelled immediately, the activation key must stop being suppressed, and Ghost Mode must be deactivated immediately if it has already become active during the same hold.

## Goals

1. Prevent Ghost Mode from activating if the user begins typing or triggering a shortcut while holding the activation key.
2. Immediately deactivate Ghost Mode if it is already active and another key is pressed during the same activation-key hold.
3. Ensure the non-activation key is passed through to Windows and the target application without suppression.
4. Preserve existing mouse activation behavior.
5. Preserve existing keyboard toggle behavior outside the new handoff case.

## Non-Goals

1. No changes to tray UI, settings shape, or activation-key selection.
2. No changes to mouse activation semantics.
3. No attempt to replay or synthesize suppressed non-activation keys.
4. No broad hook architecture refactor beyond what is needed for this behavior.

## Current Behavior

`KeyboardHook` already tracks whether another key was pressed after the activation key went down (`KeyboardHook.cs`). However, the current path reuses the generic "other key before activation" callback and does not distinguish between:

1. Blocking activation before the hold really starts.
2. Cancelling a pending hold because the user handed keyboard control back to Windows.
3. Immediately dropping active Ghost Mode during the same hold.

`ActivationStateManager` currently has `BlockActivation()` and `ForceDeactivate()` but no single operation that models keyboard handoff as one coherent state transition (`ActivationStateManager.cs`).

## Recommended Approach

Use an explicit keyboard-handoff cancellation path spanning `KeyboardHook`, `GhostController`, and `ActivationStateManager`.

### Why this approach

1. The hook remains responsible only for detecting input patterns.
2. The state manager remains responsible for pressed-state, timer, and suppression transitions.
3. The controller remains responsible for user-visible Ghost Mode deactivation.
4. The new behavior is explicit and testable without overloading older callback names.

## Rejected Alternatives

### 1. Put all handoff logic directly into `KeyboardHook`

This would let the hook inspect state and directly force deactivation through the activation host. It is faster to wire, but it mixes low-level input translation with domain state transitions and makes future activation changes harder to reason about.

### 2. Reuse `BlockActivation()` for both pending and active states

This would keep the surface area smaller, but `BlockActivation()` is currently aimed at denying activation, not at a combined "cancel pending activation and deactivate current Ghost Mode" flow. Extending it implicitly would blur behavior and make regressions around toggle and suppression more likely.

## Detailed Design

### 1. `KeyboardHook`: detect handoff once per activation-key hold

During keyboard activation:

1. When the activation key goes down, reset a per-hold handoff flag.
2. When any other key goes down while the activation key is still down, mark the hold as handed off.
3. Fire a dedicated callback for keyboard handoff on the first such key-down in the hold.
4. Do not suppress the non-activation key event.
5. On activation-key up, if handoff already happened, do not run the normal activation-key release path again.

This keeps the second key fully owned by Windows and prevents duplicate controller/state transitions on activation-key release.

### 2. `ActivationStateManager`: add explicit handoff cancellation

Add a dedicated method for keyboard handoff cancellation.

Responsibilities:

1. Stop the activation timer.
2. Clear `_isActivationKeyDown`.
3. Clear any pending activation markers such as `_timerFired` if activation has not completed yet.
4. If Ghost Mode is active, switch `_ghostModeActive` to false and request deactivation.
5. Clear keyboard suppression immediately so the activation key is no longer intentionally swallowed after handoff.
6. Leave mouse-related state untouched unless shared reset logic already requires clearing it safely.

This method becomes the single authority for converting "user pressed another key during Win-hold" into the correct runtime state.

### 3. `GhostController`: route handoff into state + UI deactivation

`GhostController` should expose a dedicated handler for keyboard handoff and wire it from `KeyboardHook`.

Handler behavior:

1. Ignore the callback when the current activation type is not keyboard.
2. Ask `ActivationStateManager` to cancel the hold via the new handoff method.
3. Let the state manager decide whether deactivation is needed.
4. Preserve the existing deactivation pipeline so transparent window state, tooltip state, and sound behavior remain consistent with other deactivation paths.

### 4. Suppression behavior

The key rule is:

- The non-activation key that triggered handoff must never be suppressed by GhostThrough.
- After handoff, the activation key must no longer remain in a suppression-only state caused by Ghost Mode activation/deactivation bookkeeping.

That means keyboard handoff must explicitly clear suppression state instead of relying on the ordinary delayed suppression timer used for normal deactivation.

### 5. Release behavior after handoff

After keyboard handoff has already been processed for the current hold:

1. Releasing the activation key should not trigger the normal short-press deactivation logic.
2. Releasing the activation key should not re-enter the block/cancel path.
3. The release should simply pass through.

This prevents duplicate deactivation requests and avoids reintroducing suppression after the handoff already completed.

## Data Flow

```text
Activation key down
  -> KeyboardHook starts hold tracking
  -> ActivationStateManager starts activation timer

Other key down while activation key still held
  -> KeyboardHook marks handoff for this hold
  -> KeyboardHook raises dedicated handoff callback
  -> GhostController handles keyboard handoff
  -> ActivationStateManager cancels pending activation and clears suppression
  -> if Ghost Mode active: controller deactivation pipeline runs immediately
  -> Other key continues to Windows/app unsuppressed

Activation key up after handoff
  -> KeyboardHook ignores normal activation release handling
  -> Win key up continues to Windows/app unsuppressed
```

## Testing Strategy

Add regression coverage to `KeyboardHookRegressionTest.cs`.

Required cases:

1. Press activation key down, press another key before timer completion, verify the dedicated handoff path runs and pending activation state is cancelled.
2. Press activation key down, simulate Ghost Mode already active during the same hold, press another key, verify the handoff path immediately deactivates Ghost Mode.
3. After handoff, releasing the activation key must not trigger a second deactivation/block callback.
4. Existing test for `_keyPressedAfterActivation` tracking should be updated or replaced so it validates the new explicit handoff contract instead of only the internal flag.

The regression tests can continue using reflection-based access patterns already present in the test project.

## Risks and Mitigations

### Risk: duplicate deactivation

If both the handoff callback and later activation-key-up path try to deactivate, the controller can see redundant requests.

Mitigation: track handoff per hold inside `KeyboardHook` and bypass the normal activation-key-up handler after handoff.

### Risk: accidental Win-key suppression after handoff

If the existing delayed suppression timer remains active, Windows may still lose the activation-key release event or open the Start menu unexpectedly.

Mitigation: make the handoff cancellation path explicitly stop suppression timers and clear `_suppressActivationKey` immediately.

### Risk: regressions in non-keyboard activation

Mitigation: keep the new behavior behind the keyboard activation flow only and leave mouse codepaths unchanged.

## Success Criteria

1. Holding the activation key and then pressing another key never leaves Ghost Mode active.
2. If Ghost Mode activates just before the second key press, it is immediately turned off.
3. The second key is delivered to Windows and the foreground application.
4. Releasing the activation key after handoff does not trigger duplicate state transitions.
5. Existing build and regression tests continue to pass.
