using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace PeekThrough
{
    internal class KeyboardHook : IDisposable
    {
        private NativeMethods.LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private SynchronizationContext _syncContext;
        private bool _disposed = false;
        private GhostLogic _ghostLogic;
        
        // Отслеживание нажатых клавиш (кроме клавиши активации)
        private HashSet<int> _pressedKeys = new HashSet<int>();
        
        // Флаг: была ли нажата другая клавиша ПОСЛЕ клавиши активации
        private bool _keyPressedAfterActivation = false;

        public event Action OnActivationKeyDown;
        public event Action OnActivationKeyUp;
        
        // Событие для уведомления о нажатии другой клавиши перед клавишей активации
        public event Action OnOtherKeyPressedBeforeActivation;

        public KeyboardHook(GhostLogic ghostLogic)
        {
            _ghostLogic = ghostLogic;
            _proc = HookCallback;
            _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();
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
                // Используем PtrToStructure для безопасного доступа к полям структуры
                var hookStruct = (NativeMethods.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.KBDLLHOOKSTRUCT));
                int vkCode = hookStruct.vkCode;
                int flags = hookStruct.flags;
                
                // Skip processing our own injected inputs (tagged with INJECTED_BY_US)
                if (hookStruct.dwExtraInfo == NativeMethods.INJECTED_BY_US)
                {
                    return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
                }
                
                // Получаем текущую клавишу активации
                int activationKey = _ghostLogic != null ? _ghostLogic.ActivationKeyCode : NativeMethods.VK_LWIN;
                bool isActivationKey = (vkCode == activationKey);
                
                if (isActivationKey)
                {
                    DebugLogger.Log(string.Format("HookCallback: Activation key detected (vkCode={0}), wParam={1}", vkCode, wParam));

                    // Безопасное копирование события для проверки null
                    Action handler = null;

                    if (wParam == (IntPtr)NativeMethods.WM_KEYDOWN)
                    {
                        // Сбрасываем флаг при новом нажатии клавиши активации
                        _keyPressedAfterActivation = false;

                        // Если нажата другая клавиша до клавиши активации - не активируем Ghost Mode
                        if (_pressedKeys.Count > 0)
                        {
                            DebugLogger.Log(string.Format("HookCallback: Other keys pressed before activation key ({0}), blocking Ghost Mode", _pressedKeys.Count));
                            var handlerBlocked = OnOtherKeyPressedBeforeActivation;
                            if (handlerBlocked != null)
                            {
                                _syncContext.Post(state =>
                                {
                                    try { handlerBlocked(); }
                                    catch (Exception ex) { DebugLogger.Log(string.Format("OtherKey handler error: {0}", ex.Message)); }
                                }, null);
                            }
                        }
                        else
                        {
                            handler = OnActivationKeyDown;
                        }
                    }
                    else if (wParam == (IntPtr)NativeMethods.WM_KEYUP)
                    {
                        // Capture the flag value BEFORE resetting it (fixes combo bug)
                        bool keyPressedAfterActivation = _keyPressedAfterActivation;
                        
                        // При отпускании клавиши активации проверяем, была ли нажата клавиша после
                        if (keyPressedAfterActivation)
                        {
                            DebugLogger.Log("HookCallback: Activation key released but key was pressed after - blocking Ghost Mode");
                            // Блокируем активацию Ghost Mode
                            var handlerBlocked = OnOtherKeyPressedBeforeActivation;
                            if (handlerBlocked != null)
                            {
                                _syncContext.Post(state =>
                                {
                                    try { handlerBlocked(); }
                                    catch (Exception ex) { DebugLogger.Log(string.Format("OtherKey handler error on activation key release: {0}", ex.Message)); }
                                }, null);
                            }
                        }
                        else
                        {
                            handler = OnActivationKeyUp;
                        }

                        // Сбрасываем флаг после обработки
                        _keyPressedAfterActivation = false;
                        
                        // Подавление клавиши активации:
                        // Если Ghost Mode активен или в задержке после деактивации — подавляем всё
                        bool shouldSuppressGhost = _ghostLogic != null && _ghostLogic.ShouldSuppressWinKey;
                        DebugLogger.Log(string.Format("HookCallback: ShouldSuppressWinKey = {0}", shouldSuppressGhost));

                        if (shouldSuppressGhost)
                        {
                            DebugLogger.Log("HookCallback: SUPPRESSING activation key event (Ghost Mode)!");
                            return (IntPtr)1;
                        }
                        
                        // No standalone KEYUP suppression - let the system handle short Win press
                        // (Start menu will open naturally for short presses)
                    }
                    
                    // Вызов обработчика с обработкой исключений через syncContext
                    if (handler != null)
                    {
                        _syncContext.Post(state =>
                        {
                            try { handler(); }
                            catch (Exception ex) { DebugLogger.Log(string.Format("Hook handler error: {0}", ex.Message)); }
                        }, null);
                    }
                }
                else
                {
                    // Отслеживание нажатия/отпускания других клавиш
                    if (wParam == (IntPtr)NativeMethods.WM_KEYDOWN)
                    {
                        _pressedKeys.Add(vkCode);
                        DebugLogger.Log(string.Format("HookCallback: Other key DOWN, vkCode={0}, total pressed: {1}", vkCode, _pressedKeys.Count));

                        // Если клавиша активации сейчас нажата, отмечаем что клавиша нажата ПОСЛЕ
                        if (_ghostLogic != null && _ghostLogic.IsLWinPressed)
                        {
                            _keyPressedAfterActivation = true;
                            DebugLogger.Log("HookCallback: Key pressed AFTER activation key - will block Ghost Mode");
                        }

                        // Если нажата любая другая клавиша и Ghost Mode активен,
                        // отключаем Ghost Mode и пропускаем клавишу для стандартной обработки
                        if (_ghostLogic != null && _ghostLogic.IsGhostModeActive)
                        {
                            DebugLogger.Log("HookCallback: Other key pressed while Ghost Mode active, deactivating");
                            _syncContext.Post(state =>
                            {
                                try
                                {
                                    _ghostLogic.DeactivateGhostMode();
                                }
                                catch (Exception ex)
                                {
                                    DebugLogger.Log(string.Format("DeactivateGhostMode error: {0}", ex.Message));
                                }
                            }, null);
                        }
                    }
                    else if (wParam == (IntPtr)NativeMethods.WM_KEYUP)
                    {
                        _pressedKeys.Remove(vkCode);
                        DebugLogger.Log(string.Format("HookCallback: Other key UP, vkCode={0}, remaining: {1}", vkCode, _pressedKeys.Count));
                    }
                }
            }
            return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
    }
}
