using System;
using System.Collections.Generic;

namespace GhostThrough
{
    internal enum HotkeyAction
    {
        NextProfile,
        PreviousProfile
    }

    /// <summary>
    /// Detects hotkey combinations and triggers actions
    /// Hardcoded: Ctrl+Shift+Up (NextProfile), Ctrl+Shift+Down (PreviousProfile)
    /// </summary>
    internal class HotkeyManager
    {
        private readonly HashSet<int> _pressedKeys = new HashSet<int>();

        public event Action<HotkeyAction> OnHotkey;

        public bool ProcessKey(int vkCode, bool isDown, bool ctrl, bool shift, bool alt)
        {
            // Track pressed keys for combo detection
            if (isDown)
                _pressedKeys.Add(vkCode);
            else
                _pressedKeys.Remove(vkCode);

            // Only process on key down
            if (!isDown)
                return false;

            // Check for profile hotkeys (Ctrl+Shift+Up/Down)
            if (ctrl && shift && !alt)
            {
                if (vkCode == NativeMethods.VK_UP)
                {
                    DebugLogger.Log("HotkeyManager: Detected Ctrl+Shift+Up (NextProfile)");
                    if (OnHotkey != null)
                        OnHotkey(HotkeyAction.NextProfile);
                    return true; // Key consumed
                }
                if (vkCode == NativeMethods.VK_DOWN)
                {
                    DebugLogger.Log("HotkeyManager: Detected Ctrl+Shift+Down (PreviousProfile)");
                    if (OnHotkey != null)
                        OnHotkey(HotkeyAction.PreviousProfile);
                    return true; // Key consumed
                }
            }

            return false; // Not a hotkey
        }

        public void Reset()
        {
            _pressedKeys.Clear();
        }
    }
}
