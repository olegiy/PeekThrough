# PeekThrough Refactoring Design

**Date:** 2026-04-17  
**Topic:** GhostLogic.cs Decomposition + Profile/Hotkey Features  
**Status:** Approved, pending implementation plan

---

## 1. Overview

Refactor `GhostLogic.cs` (770 lines, 15+ responsibilities) into focused, single-responsibility classes. This enables easy addition of profile management and hotkey features while maintaining testability and clarity.

**Scope:**
- Decompose `GhostLogic.cs` into 4 core classes
- Add `ProfileManager` for opacity profiles (Min/Med/Max)
- Add `HotkeyManager` for profile switching shortcuts
- Migrate settings from pseudo-JSON to real JSON
- Preserve all existing functionality (no behavioral changes)

---

## 2. Architecture

### 2.1 Component Diagram

```
┌─────────────────────────────────────┐
│         GhostController             │  ← Main coordinator, replaces GhostLogic
│  (event wiring, high-level flow)   │
└──────┬──────────────┬───────────────┘
       │              │
       ▼              ▼
┌─────────────┐  ┌─────────────────────┐
│ Window      │  │ ActivationState     │
│ Transparency│  │ Manager             │
│ Manager     │  │ - Timers            │
│ - Win32 API │  │ - Flags (_isLWinDown│
│ - Opacity   │  │   _ghostModeActive) │
│   apply     │  │ - Events            │
└─────────────┘  └─────────────────────┘
       │                  │
       │    ┌─────────────┘
       │    ▼
       │  ┌─────────────────────┐
       │  │ TooltipService      │
       │  │ - Form/Label        │
       │  │ - Show/Hide         │
       │  └─────────────────────┘
       │
       ▼
┌─────────────────────────────────────┐
│  ProfileManager (NEW)               │
│  - 3 fixed profiles:                │
│    * min: 38 (15% - current)        │
│    * med: 128 (50%)                 │
│    * max: 204 (80%)                 │
│  - SwitchToNext/PreviousProfile()   │
│  - CurrentOpacity property          │
└─────────────────────────────────────┘
       │
       ▼
┌─────────────────────────────────────┐
│  HotkeyManager (NEW)                │
│  - NextProfile: Ctrl+Shift+Up       │
│  - PrevProfile: Ctrl+Shift+Down     │
│  - Registration via KeyboardHook      │
└─────────────────────────────────────┘
```

### 2.2 Component Responsibilities

| Component | Lines (est.) | What It Does | What It Does NOT Do |
|-----------|--------------|--------------|---------------------|
| `GhostController` | 100-150 | Wires events, coordinates flow, holds component references | No Win32 calls, no timer logic |
| `WindowTransparencyManager` | 150-200 | Applies/restores transparency via Win32 API | No state tracking, no event decisions |
| `ActivationStateManager` | 150-200 | Tracks key states, manages timers, fires activation events | No Win32 calls, no UI |
| `TooltipService` | 50-80 | Shows/hides tooltip Form | No business logic |
| `ProfileManager` | 80-100 | Manages 3 profiles, cycles between them | No hotkey handling |
| `HotkeyManager` | 60-80 | Detects hotkey combos, triggers profile switch | No profile storage |

---

## 3. Data Flow

### 3.1 Ghost Mode Activation (existing behavior preserved)

```
1. KeyboardHook.HookCallback(WM_KEYDOWN, VK_LWIN)
   └── GhostController.OnActivationKeyDown()
       └── ActivationStateManager.StartActivationTimer()
           └── (1 sec later) OnGhostModeShouldActivate event
               └── GhostController.OnGhostModeShouldActivate()
                   ├── WindowTransparencyManager.ApplyTransparency(hwnd, ProfileManager.CurrentOpacity)
                   ├── TooltipService.Show()
                   └── NativeMethods.Beep()
```

### 3.2 Profile Switching (new feature)

```
1. KeyboardHook detects Ctrl+Shift+Up
   └── GhostController.HotkeyManager.OnHotkey("NextProfile")
       └── ProfileManager.SwitchToNextProfile()
           └── CurrentOpacity: 38 → 128 → 204 → 38
       └── If GhostMode active:
           GhostController.RefreshCurrentWindowTransparency()
               └── WindowTransparencyManager.ApplyTransparency(hwnd, newOpacity)
```

---

## 4. Key Interfaces

### 4.1 WindowTransparencyManager

```csharp
public class WindowTransparencyManager : IDisposable
{
    public void ApplyTransparency(IntPtr hwnd, byte opacity);
    public void RestoreWindow(IntPtr hwnd);
    public void RestoreAllWindows();
    public bool IsWindowValid(IntPtr hwnd);
    
    // Tracks which windows we've modified
    private List<GhostWindowState> _ghostWindows;
}

public class GhostWindowState
{
    public IntPtr Hwnd { get; set; }
    public int OriginalExStyle { get; set; }
    public bool WasAlreadyLayered { get; set; }
}
```

### 4.2 ActivationStateManager

```csharp
public class ActivationStateManager : IDisposable
{
    // Events
    public event Action OnGhostModeShouldActivate;
    public event Action OnGhostModeShouldDeactivate;
    public event Action OnActivationBlocked;  // other key pressed
    
    // State (thread-safe via lock)
    public bool IsGhostModeActive { get; }
    public bool IsActivationKeyPressed { get; }
    public ActivationType CurrentActivationType { get; set; }
    
    // Input methods called by hooks
    public void OnActivationKeyDown();
    public void OnActivationKeyUp();
    public void OnOtherKeyPressed();  // blocks activation
    public void DeactivateGhostMode();  // force deactivate
}
```

### 4.3 ProfileManager

```csharp
public class ProfileManager
{
    public IReadOnlyList<Profile> Profiles { get; }
    public Profile ActiveProfile { get; private set; }
    public byte CurrentOpacity => ActiveProfile.Opacity;
    
    public void SwitchToNextProfile();    // cycles: min→med→max→min
    public void SwitchToPreviousProfile();
    public void SetActiveProfile(string profileId);
}

public class Profile
{
    public string Id { get; }
    public string Name { get; }
    public byte Opacity { get; }  // 0-255
}
```

### 4.4 HotkeyManager

```csharp
public class HotkeyManager
{
    public event Action<HotkeyAction> OnHotkey;
    
    public bool ProcessKey(int vkCode, bool isDown, bool ctrl, bool shift, bool alt);
    // returns true if key was consumed as hotkey
}

public enum HotkeyAction
{
    NextProfile,
    PreviousProfile
}
```

---

## 5. Settings Migration

### 5.1 Current Format (pseudo-JSON)

```
ActivationKeyCode: 91
ActivationType: 0
MouseButton: 4
```

### 5.2 New Format (JSON)

```json
{
  "version": 2,
  "activation": {
    "type": "keyboard",
    "keyCode": 91,
    "mouseButton": 4
  },
  "profiles": {
    "list": [
      { "id": "min", "name": "Minimum", "opacity": 38 },
      { "id": "med", "name": "Medium", "opacity": 128 },
      { "id": "max", "name": "Maximum", "opacity": 204 }
    ],
    "activeId": "min"
  },
  "hotkeys": {
    "nextProfile": { "ctrl": true, "shift": true, "key": "Up" },
    "prevProfile": { "ctrl": true, "shift": true, "key": "Down" }
  }
}
```

### 5.3 Migration Strategy

```csharp
public class SettingsManager
{
    public Settings LoadSettings()
    {
        // Check for v1 format (lines with colons)
        if (IsV1Format(settingsPath))
        {
            var v1 = ParseV1Format(settingsPath);
            var v2 = ConvertToV2(v1);
            File.Move(settingsPath, settingsPath + ".bak");
            SaveV2Settings(v2);
            return v2;
        }
        return LoadV2Settings();
    }
}
```

---

## 6. Error Handling

| Scenario | Handling |
|----------|----------|
| Window closes during Ghost Mode | `WindowTransparencyManager.RestoreWindow()` catches exception, logs, removes from list |
| Settings file corrupt | Load defaults, backup corrupt file, show tray notification |
| Profile hotkey during no Ghost Mode | Profile switches, no window affected (applies on next activation) |
| Multiple keys pressed with activation key | `ActivationStateManager.OnOtherKeyPressed()` blocks activation |
| Hook callback exception | Caught at `GhostController` level, logged, next hook continues |

---

## 7. Testing Strategy

### 7.1 Unit Tests (where possible)

```csharp
[Test]
public void ProfileManager_CyclesThroughProfiles()
{
    var pm = new ProfileManager(new[] { 
        new Profile("min", 38),
        new Profile("med", 128),
        new Profile("max", 204)
    });
    
    Assert.AreEqual(38, pm.CurrentOpacity);
    pm.SwitchToNextProfile();
    Assert.AreEqual(128, pm.CurrentOpacity);
    pm.SwitchToNextProfile();
    Assert.AreEqual(204, pm.CurrentOpacity);
    pm.SwitchToNextProfile();
    Assert.AreEqual(38, pm.CurrentOpacity); // cycles back
}

[Test]
public void ActivationStateManager_BlocksActivation_WhenOtherKeyPressed()
{
    var asm = new ActivationStateManager();
    bool activated = false;
    asm.OnGhostModeShouldActivate += () => activated = true;
    
    asm.OnActivationKeyDown();
    asm.OnOtherKeyPressed();  // simulate another key during hold
    asm.OnActivationKeyUp();
    
    Assert.IsFalse(activated);
}
```

### 7.2 Manual Integration Tests

| Test | Expected Result |
|------|----------------|
| Long Win press (1s) | Ghost Mode activates, tooltip shows, beep sounds |
| Short Win press while active | Ghost Mode deactivates, window restored |
| Escape while active | Immediate deactivation |
| Ctrl+Shift+Up while active | Opacity increases (38→128→204→38), tooltip updates |
| Ctrl+Shift+Down while active | Opacity decreases |
| App restart | Previous activation key and active profile preserved |
| Old settings file | Auto-migrated to new format, .bak created |

---

## 8. File Structure After Refactoring

```
PeekThrough/
├── Program.cs                    (entry point, DI wiring)
├── GhostController.cs            (replaces GhostLogic, ~150 lines)
├── WindowTransparencyManager.cs  (Win32 operations, ~150 lines)
├── ActivationStateManager.cs     (state & timers, ~150 lines)
├── TooltipService.cs             (tooltip UI, ~60 lines)
├── ProfileManager.cs             (opacity profiles, ~80 lines)
├── HotkeyManager.cs              (hotkey detection, ~60 lines)
├── SettingsManager.cs            (load/save/migrate, ~100 lines)
├── KeyboardHook.cs               (refactored to use GhostController)
├── MouseHook.cs                  (refactored to use GhostController)
├── NativeMethods.cs              (unchanged)
├── DebugLogger.cs                (unchanged)
├── Models/
│   ├── Profile.cs
│   ├── GhostWindowState.cs
│   └── Settings.cs
└── docs/superpowers/specs/2026-04-17-refactoring-design.md (this file)
```

---

## 9. Migration Phases

**Phase 1:** Create new classes alongside existing GhostLogic (no changes to Program.cs yet)
**Phase 2:** Switch Program.cs to use GhostController, keep GhostLogic as backup
**Phase 3:** Remove GhostLogic.cs once validated
**Phase 4:** Add ProfileManager and HotkeyManager
**Phase 5:** Settings migration (v1→v2)

---

## 10. Success Criteria

- [ ] All existing behaviors preserved (activation, deactivation, toggle, escape)
- [ ] `GhostLogic.cs` deleted, functionality distributed to new classes
- [ ] Profile switching works (Ctrl+Shift+Up/Down)
- [ ] Settings migrate automatically from old format
- [ ] No regressions in `peekthrough_debug.log` event sequence
- [ ] Code coverage: each new class < 200 lines, single responsibility

---

## 11. Open Questions / Decisions

1. **Profile persistence:** Should we save last active profile or always start with "min"?
   - Decision: Save to settings, restore on startup.

2. **Hotkey customization:** Phase 1 uses hardcoded Ctrl+Shift+Up/Down. Customization in future phase.

3. **Opacity values:** 38/128/204 (15%/50%/80%) or different? Using current 38 as baseline.

---

**Approved by:** user  
**Next step:** Create implementation plan via `writing-plans` skill
