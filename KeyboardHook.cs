using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace PeekThrough
{
    internal class KeyboardHook : IDisposable
    {
        private NativeMethods.LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private SynchronizationContext _syncContext;
        private bool _disposed = false;
        private IActivationHost _activationHost;

        // Modifier key tracking
        private bool _ctrlPressed;
        private bool _shiftPressed;
        private bool _altPressed;

        // Other key tracking
        private HashSet<int> _pressedKeys = new HashSet<int>();
        private bool _keyPressedAfterActivation = false;
        private bool _isActivationKeyDown = false;

        public event Action OnActivationKeyDown;
        public event Action OnActivationKeyUp;
        public event Action OnOtherKeyPressedBeforeActivation;

        public KeyboardHook(IActivationHost activationHost)
        {
            _activationHost = activationHost;
            _proc = HookCallback;
            _syncContext = SynchronizationContext.Current;
            if (_syncContext == null)
                _syncContext = new SynchronizationContext();
            _hookID = SetHook(_proc);
            DebugLogger.Log(string.Format("KeyboardHook initialized, hook ID: {0}", _hookID));
        }

        public void Dispose()
        {
            IntPtr hookToDispose = IntPtr.Zero;

            lock (this)
            {
                if (!_disposed && _hookID != IntPtr.Zero)
                {
                    hookToDispose = _hookID;
                    _hookID = IntPtr.Zero;
                    _disposed = true;
                }
            }

            if (hookToDispose != IntPtr.Zero)
            {
                DebugLogger.Log("KeyboardHook.Dispose: Unhooking keyboard hook");
                NativeMethods.UnhookWindowsHookEx(hookToDispose);
            }

            GC.SuppressFinalize(this);
        }

        private IntPtr SetHook(NativeMethods.LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, proc,
                    NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = (NativeMethods.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.KBDLLHOOKSTRUCT));
                int vkCode = hookStruct.vkCode;
                int flags = hookStruct.flags;

                // Skip our own injected inputs
                if (hookStruct.dwExtraInfo == NativeMethods.INJECTED_BY_US)
                {
                    return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                bool isKeyDown = (wParam == (IntPtr)NativeMethods.WM_KEYDOWN);
                bool isKeyUp = (wParam == (IntPtr)NativeMethods.WM_KEYUP);

                // Track modifier keys
                UpdateModifierKeys(vkCode, isKeyDown, isKeyUp);

                // Try hotkey processing first (always active)
                bool hotkeyConsumed = TryProcessHotkey(vkCode, isKeyDown);
                if (hotkeyConsumed)
                {
                    return (IntPtr)1; // Suppress the key
                }

                // Process activation key
                int activationKey = NativeMethods.VK_LWIN;
                if (_activationHost != null)
                    activationKey = _activationHost.ActivationKeyCode;
                bool isActivationKey = (vkCode == activationKey);

                if (isActivationKey)
                {
                    return ProcessActivationKey(nCode, wParam, lParam, vkCode);
                }
                else
                {
                    return ProcessOtherKey(nCode, wParam, lParam, vkCode);
                }
            }
            return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void UpdateModifierKeys(int vkCode, bool isKeyDown, bool isKeyUp)
        {
            if (vkCode == NativeMethods.VK_CONTROL || vkCode == NativeMethods.VK_LCONTROL || vkCode == NativeMethods.VK_RCONTROL)
                _ctrlPressed = isKeyDown;
            if (vkCode == NativeMethods.VK_SHIFT || vkCode == NativeMethods.VK_LSHIFT || vkCode == NativeMethods.VK_RSHIFT)
                _shiftPressed = isKeyDown;
            if (vkCode == NativeMethods.VK_LMENU || vkCode == NativeMethods.VK_RMENU)
                _altPressed = isKeyDown;
        }

        private bool TryProcessHotkey(int vkCode, bool isKeyDown)
        {
            if (!isKeyDown || _activationHost == null)
                return false;

            // Don't process hotkeys if we're in the middle of activation key handling
            if (_isActivationKeyDown)
                return false;

            return _activationHost.ProcessHotkey(vkCode, isKeyDown, _ctrlPressed, _shiftPressed, _altPressed);
        }

        private IntPtr ProcessActivationKey(int nCode, IntPtr wParam, IntPtr lParam, int vkCode)
        {
            DebugLogger.Log(string.Format("HookCallback: Activation key detected (vkCode={0}), wParam={1}", vkCode, wParam));

            Action handler = null;

            if (wParam == (IntPtr)NativeMethods.WM_KEYDOWN)
            {
                _isActivationKeyDown = true;
                _keyPressedAfterActivation = false;

                if (_pressedKeys.Count > 0)
                {
                    DebugLogger.Log(string.Format("HookCallback: Other keys pressed before activation ({0}), blocking", _pressedKeys.Count));
                    var handlerBlocked = OnOtherKeyPressedBeforeActivation;
                    PostHandler(handlerBlocked, "OtherKey handler error");
                }
                else
                {
                    handler = OnActivationKeyDown;
                }
            }
            else if (wParam == (IntPtr)NativeMethods.WM_KEYUP)
            {
                _isActivationKeyDown = false;
                bool keyPressedAfterActivation = _keyPressedAfterActivation;

                if (keyPressedAfterActivation)
                {
                    DebugLogger.Log("HookCallback: Key pressed after activation - blocking");
                    var handlerBlocked = OnOtherKeyPressedBeforeActivation;
                    PostHandler(handlerBlocked, "OtherKey handler error on release");
                }
                else
                {
                    handler = OnActivationKeyUp;
                }

                _keyPressedAfterActivation = false;
            }

            // Check if we should suppress the activation key
            bool shouldSuppress = _activationHost != null && _activationHost.ShouldSuppressActivationKey;
            DebugLogger.Log(string.Format("HookCallback: ShouldSuppressWinKey = {0}", shouldSuppress));

            if (shouldSuppress)
            {
                DebugLogger.Log("HookCallback: SUPPRESSING activation key event!");

                PostHandler(handler, "Hook handler error");

                return (IntPtr)1;
            }

            // Not suppressed - fire handler normally
            PostHandler(handler, "Hook handler error");

            return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private IntPtr ProcessOtherKey(int nCode, IntPtr wParam, IntPtr lParam, int vkCode)
        {
            if (wParam == (IntPtr)NativeMethods.WM_KEYDOWN)
            {
                _pressedKeys.Add(vkCode);
                DebugLogger.Log(string.Format("HookCallback: Other key DOWN, vkCode={0}, total: {1}", vkCode, _pressedKeys.Count));

                if (_isActivationKeyDown)
                {
                    _keyPressedAfterActivation = true;
                    DebugLogger.Log("HookCallback: Key pressed AFTER activation key");
                }

                // Escape during Ghost Mode - quick exit
                if (_activationHost != null && _activationHost.IsGhostModeActive && vkCode == NativeMethods.VK_ESCAPE)
                {
                    DebugLogger.Log("HookCallback: Escape pressed while Ghost Mode active");
                    PostHandler(_activationHost.RequestDeactivate, "DeactivateGhostMode error");
                }
            }
            else if (wParam == (IntPtr)NativeMethods.WM_KEYUP)
            {
                _pressedKeys.Remove(vkCode);
                DebugLogger.Log(string.Format("HookCallback: Other key UP, vkCode={0}, remaining: {1}", vkCode, _pressedKeys.Count));
            }

            return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void PostHandler(Action handler, string label)
        {
            if (handler == null)
                return;

            _syncContext.Post(state =>
            {
                try { handler(); }
                catch (Exception ex) { DebugLogger.Log(string.Format("{0}: {1}", label, ex.Message)); }
            }, null);
        }
    }
}
