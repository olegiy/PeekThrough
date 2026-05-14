using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using GhostThrough.Models;

namespace GhostThrough
{
    /// <summary>
    /// Main coordinator that wires events between components and manages Ghost Mode flow
    /// Replaces GhostLogic (770 lines) with focused single-responsibility delegation
    /// </summary>
    internal class GhostController : IDisposable, IActivationHost
    {
        // Components
        private readonly WindowTransparencyManager _transparencyManager;
        private readonly ActivationStateManager _activationState;
        private readonly TooltipService _tooltipService;
        private readonly ProfileManager _profileManager;
        private readonly HotkeyManager _hotkeyManager;

        // Beep constants
        private const int BEEP_FREQUENCY_ACTIVATE = 1000;
        private const int BEEP_FREQUENCY_DEACTIVATE = 500;
        private const int BEEP_DURATION_MS = 50;

        // Current target window (under cursor when activated)
        private IntPtr _currentTargetHwnd = IntPtr.Zero;
        private int _activationKeyCode;
        private ActivationKeyBehavior _activationKeyBehavior;
        private bool _deactivationInProgress;

        // Disposable tracking
        private bool _disposed = false;

        public bool IsGhostModeActive
        {
            get { return _activationState.IsGhostModeActive; }
        }

        public bool ShouldSuppressActivationKey
        {
            get { return _activationState.ShouldSuppressActivationKey; }
        }

        public bool ShouldSuppressWinKey
        {
            get { return _activationState.ShouldSuppressActivationKey; }
        }

        public bool IsLWinPressed
        {
            get { return _activationState.IsActivationKeyPressed; }
        }

        public bool IsMouseActivationActive
        {
            get { return _activationState.IsMouseButtonPressed; }
        }

        public int ActivationKeyCode
        {
            get { return _activationKeyCode; }
            set { _activationKeyCode = NormalizeActivationKeyCode(value); }
        }

        public ActivationKeyBehavior ActivationKeyBehavior
        {
            get { return _activationKeyBehavior; }
            set { _activationKeyBehavior = value; }
        }

        public bool ShouldUseReverseWinKeyBehavior
        {
            get
            {
                return CurrentActivationType == ActivationInputType.Keyboard &&
                       _activationKeyBehavior == ActivationKeyBehavior.WinReverse &&
                       IsWinKey(ActivationKeyCode);
            }
        }

        public ActivationInputType CurrentActivationType
        {
            get { return _activationState.CurrentActivationType; }
            set { _activationState.CurrentActivationType = value; }
        }

        public int ActivationDelayMs
        {
            get { return _activationState.ActivationDelayMs; }
            set { _activationState.ActivationDelayMs = value; }
        }

        public ActivationMode CurrentActivationMode
        {
            get { return _activationState.CurrentActivationMode; }
            set { _activationState.CurrentActivationMode = value; }
        }

        public GhostController(ActivationInputType activationType, ProfileManager profileManager, int activationDelayMs = ActivationStateManager.DEFAULT_ACTIVATION_DELAY_MS, ActivationMode activationMode = ActivationMode.Hold)
        {
            _transparencyManager = new WindowTransparencyManager();
            _activationState = new ActivationStateManager(activationType, activationDelayMs, activationMode);
            _tooltipService = new TooltipService();
            _profileManager = profileManager ?? new ProfileManager((IEnumerable<Profile>)null);
            _hotkeyManager = new HotkeyManager();
            ActivationKeyCode = NativeMethods.VK_LWIN;
            ActivationKeyBehavior = ActivationKeyBehavior.Standard;

            WireEvents();
        }

        private void WireEvents()
        {
            // Activation state events
            _activationState.OnGhostModeShouldActivate += OnGhostModeShouldActivate;
            _activationState.OnGhostModeShouldDeactivate += OnGhostModeShouldDeactivate;
            _activationState.OnReverseWinShouldToggleGhostMode += OnReverseWinShouldToggleGhostMode;
            _activationState.OnReverseWinShouldPassThrough += OnReverseWinShouldPassThrough;

            // Profile change events
            _profileManager.OnProfileChanged += OnProfileChanged;

            // Hotkey events
            _hotkeyManager.OnHotkey += OnHotkey;
        }

        // Event handlers
        private bool OnGhostModeShouldActivate()
        {
            DebugLogger.Log("=== GhostController.OnGhostModeShouldActivate ===");
            return ActivateGhostMode();
        }

        private void OnGhostModeShouldDeactivate()
        {
            DebugLogger.Log("=== GhostController.OnGhostModeShouldDeactivate ===");
            CompleteDeactivation(false);
        }

        private void OnProfileChanged(Profile profile)
        {
            DebugLogger.Log(string.Format("GhostController: Profile changed to {0}", profile));

            // Refresh current window if Ghost Mode is active
            if (_activationState.IsGhostModeActive && _currentTargetHwnd != IntPtr.Zero)
            {
                RefreshCurrentWindowTransparency();
            }
        }

        private void OnHotkey(HotkeyAction action)
        {
            DebugLogger.Log(string.Format("GhostController: Hotkey received - {0}", action));

            switch (action)
            {
                case HotkeyAction.NextProfile:
                    _profileManager.SwitchToNextProfile();
                    break;
                case HotkeyAction.PreviousProfile:
                    _profileManager.SwitchToPreviousProfile();
                    break;
            }
        }

        // Public methods called by hooks
        public void OnActivationInputDown()
        {
            if (CurrentActivationType == ActivationInputType.Mouse)
                _activationState.OnMouseButtonDown();
            else
                _activationState.OnActivationKeyDown();
        }

        public void OnActivationInputUp()
        {
            if (CurrentActivationType == ActivationInputType.Mouse)
                _activationState.OnMouseButtonUp();
            else
                _activationState.OnActivationKeyUp();
        }

        public void OnOtherInputBeforeActivation()
        {
            _activationState.BlockActivation();
        }

        public void OnKeyboardHandoffDuringActivationHold()
        {
            if (CurrentActivationType != ActivationInputType.Keyboard)
                return;

            _activationState.CancelKeyboardActivationHoldForHandoff();
        }

        public void RequestDeactivate()
        {
            DeactivateGhostMode();
        }

        public void OnKeyDown()
        {
            if (CurrentActivationType != ActivationInputType.Keyboard)
                return;

            _activationState.OnActivationKeyDown();
        }

        public void OnKeyUp()
        {
            if (CurrentActivationType != ActivationInputType.Keyboard)
                return;

            _activationState.OnActivationKeyUp();
        }

        public void OnReverseWinKeyDown()
        {
            if (!ShouldUseReverseWinKeyBehavior)
                return;

            _activationState.OnReverseWinKeyDown();
        }

        public void OnReverseWinKeyUp()
        {
            if (!ShouldUseReverseWinKeyBehavior)
                return;

            _activationState.OnReverseWinKeyUp();
        }

        public void OnReverseWinKeyPassThrough()
        {
            SendStandaloneWinKey();
        }

        private void OnReverseWinShouldToggleGhostMode()
        {
            if (_activationState.IsGhostModeActive)
                DeactivateGhostMode();
            else
                ActivateGhostMode();
        }

        private void OnReverseWinShouldPassThrough()
        {
            SendStandaloneWinKey();
        }

        public void OnMouseButtonDown()
        {
            if (CurrentActivationType != ActivationInputType.Mouse)
                return;

            _activationState.OnMouseButtonDown();
        }

        public void OnMouseButtonUp()
        {
            if (CurrentActivationType != ActivationInputType.Mouse)
                return;

            _activationState.OnMouseButtonUp();
        }

        public void BlockGhostMode()
        {
            OnOtherInputBeforeActivation();
        }

        public void DeactivateGhostMode()
        {
            CompleteDeactivation(true);
        }

        private void CompleteDeactivation(bool syncActivationState)
        {
            bool shouldSyncActivationState = syncActivationState && _activationState.IsGhostModeActive;
            bool hasActiveWindow = _currentTargetHwnd != IntPtr.Zero || _transparencyManager.GhostWindows.Count > 0;
            if (!hasActiveWindow && !shouldSyncActivationState)
                return;

            if (_deactivationInProgress)
                return;

            _deactivationInProgress = true;
            DebugLogger.Log("=== GhostController.DeactivateGhostMode ===");

            try
            {
                if (shouldSyncActivationState)
                    _activationState.ForceDeactivate();

                if (hasActiveWindow)
                    _transparencyManager.RestoreAllWindows();

                _tooltipService.Hide();

                if (hasActiveWindow)
                    NativeMethods.Beep(BEEP_FREQUENCY_DEACTIVATE, BEEP_DURATION_MS);

                _currentTargetHwnd = IntPtr.Zero;
            }
            finally
            {
                _deactivationInProgress = false;
            }
        }

        public bool ProcessHotkey(int vkCode, bool isDown, bool ctrl, bool shift, bool alt)
        {
            return _hotkeyManager.ProcessKey(vkCode, isDown, ctrl, shift, alt);
        }

        public static int NormalizeActivationKeyCode(int vkCode)
        {
            return IsModifierKey(vkCode) ? NativeMethods.VK_LWIN : vkCode;
        }

        private static bool IsModifierKey(int vkCode)
        {
            return vkCode == NativeMethods.VK_CONTROL ||
                   vkCode == NativeMethods.VK_LCONTROL ||
                   vkCode == NativeMethods.VK_RCONTROL ||
                   vkCode == NativeMethods.VK_SHIFT ||
                   vkCode == NativeMethods.VK_LSHIFT ||
                   vkCode == NativeMethods.VK_RSHIFT ||
                   vkCode == NativeMethods.VK_LMENU ||
                   vkCode == NativeMethods.VK_RMENU;
        }

        private static bool IsWinKey(int vkCode)
        {
            return vkCode == NativeMethods.VK_LWIN || vkCode == NativeMethods.VK_RWIN;
        }

        private void SendStandaloneWinKey()
        {
            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[2];
            inputs[0].type = NativeMethods.INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = (ushort)ActivationKeyCode;
            inputs[0].U.ki.dwFlags = NativeMethods.KEYEVENTF_EXTENDEDKEY;
            inputs[0].U.ki.time = 0;
            inputs[0].U.ki.dwExtraInfo = NativeMethods.INJECTED_BY_US;

            inputs[1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = (ushort)ActivationKeyCode;
            inputs[1].U.ki.dwFlags = NativeMethods.KEYEVENTF_EXTENDEDKEY | NativeMethods.KEYEVENTF_KEYUP;
            inputs[1].U.ki.time = 0;
            inputs[1].U.ki.dwExtraInfo = NativeMethods.INJECTED_BY_US;

            uint sent = NativeMethods.SendInput(2, inputs, NativeMethods.INPUT.Size);
            if (sent != 2)
                DebugLogger.Log(string.Format("GhostController: SendStandaloneWinKey sent {0} inputs instead of 2", sent));
        }

        // Core Ghost Mode activation
        private bool ActivateGhostMode()
        {
            DebugLogger.Log("=== GhostController.ActivateGhostMode START ===");

            // Get window under cursor
            Point cursorPos;
            if (!NativeMethods.GetCursorPos(out cursorPos))
            {
                DebugLogger.Log("ActivateGhostMode: Failed to get cursor position");
                return false;
            }

            IntPtr hwnd = NativeMethods.WindowFromPoint(cursorPos);
            if (hwnd == IntPtr.Zero)
            {
                DebugLogger.Log("ActivateGhostMode: WindowFromPoint returned zero");
                return false;
            }

            // Get root window
            hwnd = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
            DebugLogger.Log(string.Format("ActivateGhostMode: Root window handle: {0}", hwnd));

            // Check for ignored window classes
            if (_transparencyManager.IsIgnoredWindowClass(hwnd))
            {
                DebugLogger.Log("ActivateGhostMode: Ignored system window");
                return false;
            }

            // ActivationStateManager marks the mode active before this method runs so
            // Win suppression stays in sync. Only treat it as an existing ghost window
            // once we actually have a tracked target window.
            bool hasTrackedGhostWindow = _activationState.IsGhostModeActive && _currentTargetHwnd != IntPtr.Zero;

            // Already active on this window
            if (hasTrackedGhostWindow && hwnd == _currentTargetHwnd)
            {
                DebugLogger.Log("ActivateGhostMode: Same window, showing tooltip");
                ShowTooltip(cursorPos);
                return true;
            }

            // Already active on different window
            if (hasTrackedGhostWindow)
            {
                DebugLogger.Log("ActivateGhostMode: Already active on different window, ignoring");
                ShowTooltip(cursorPos);
                return true;
            }

            _currentTargetHwnd = hwnd;

            // Inject synthetic key release for keyboard activation
            if (CurrentActivationType == ActivationInputType.Keyboard)
            {
                SendEscWinReleaseToPreventStartMenu();
            }

            // Apply transparency
            try
            {
                _transparencyManager.ApplyTransparency(_currentTargetHwnd, _profileManager.CurrentOpacity);
                ShowTooltip(cursorPos);
                NativeMethods.Beep(BEEP_FREQUENCY_ACTIVATE, BEEP_DURATION_MS);
                DebugLogger.Log("ActivateGhostMode: Window activated as ghost window");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Log(string.Format("ActivateGhostMode ERROR: {0}", ex.Message));
                _transparencyManager.RestoreWindow(_currentTargetHwnd);
                _currentTargetHwnd = IntPtr.Zero;
                return false;
            }
        }

        private void RefreshCurrentWindowTransparency()
        {
            if (_currentTargetHwnd == IntPtr.Zero)
                return;

            try
            {
                // Restore and re-apply with new opacity
                _transparencyManager.RestoreWindow(_currentTargetHwnd);
                _transparencyManager.ApplyTransparency(_currentTargetHwnd, _profileManager.CurrentOpacity);

                // Update tooltip with new profile name
                Point cursorPos;
                if (NativeMethods.GetCursorPos(out cursorPos))
                {
                    ShowTooltip(cursorPos);
                }

                DebugLogger.Log(string.Format("GhostController: Refreshed transparency with {0} opacity", _profileManager.CurrentOpacity));
            }
            catch (Exception ex)
            {
                DebugLogger.Log(string.Format("RefreshCurrentWindowTransparency ERROR: {0}", ex.Message));
            }
        }

        private void SendEscWinReleaseToPreventStartMenu()
        {
            DebugLogger.Log("GhostController: Sending Esc + Win UP to prevent Start menu");

            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[3];

            // Esc down
            inputs[0].type = NativeMethods.INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = NativeMethods.VK_ESCAPE;
            inputs[0].U.ki.dwFlags = 0;
            inputs[0].U.ki.time = 0;
            inputs[0].U.ki.dwExtraInfo = NativeMethods.INJECTED_BY_US;

            // Esc up
            inputs[1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = NativeMethods.VK_ESCAPE;
            inputs[1].U.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;
            inputs[1].U.ki.time = 0;
            inputs[1].U.ki.dwExtraInfo = NativeMethods.INJECTED_BY_US;

            // Activation key up
            inputs[2].type = NativeMethods.INPUT_KEYBOARD;
            inputs[2].U.ki.wVk = (ushort)ActivationKeyCode;
            inputs[2].U.ki.dwFlags = NativeMethods.KEYEVENTF_EXTENDEDKEY | NativeMethods.KEYEVENTF_KEYUP;
            inputs[2].U.ki.time = 0;
            inputs[2].U.ki.dwExtraInfo = NativeMethods.INJECTED_BY_US;

            NativeMethods.SendInput(3, inputs, NativeMethods.INPUT.Size);
        }

        private void ShowTooltip(Point cursorPos)
        {
            _tooltipService.Show(cursorPos, string.Format("Ghost Mode - {0}", _profileManager.ActiveProfile.Name));
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            if (_transparencyManager != null)
                _transparencyManager.Dispose();
            if (_activationState != null)
                _activationState.Dispose();
            if (_tooltipService != null)
                _tooltipService.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
