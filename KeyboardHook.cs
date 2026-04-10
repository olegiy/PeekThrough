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
        
        // Флаг: нужно ли игнорировать KEYUP для клавиши активации, пришедший от SendInput
        private bool _ignoringInjectedActivationUp = false;

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
                int vkCode = Marshal.ReadInt32(lParam);
                
                // Проверяем, является ли событие инжектированным (от SendInput)
                int flags = Marshal.ReadInt32(lParam, 8); // KBDLLHOOKSTRUCT.flags — 3-е поле (смещение 8 байт)
                bool isInjected = (flags & 0x10) != 0; // LLKHF_INJECTED = 0x10
                
                // Получаем текущую клавишу активации
                int activationKey = _ghostLogic != null ? _ghostLogic.ActivationKeyCode : NativeMethods.VK_LWIN;
                bool isActivationKey = (vkCode == activationKey);
                
                if (isActivationKey)
                {
                    // Если это инжектированный KEYUP от нашего SendInput — пропускаем в систему
                    if (isInjected && wParam == (IntPtr)NativeMethods.WM_KEYUP && _ignoringInjectedActivationUp)
                    {
                        DebugLogger.Log(string.Format("HookCallback: Ignoring injected activation KEYUP from SendInput"));
                        _ignoringInjectedActivationUp = false;
                        return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
                    }
                    
                    DebugLogger.Log(string.Format("HookCallback: Activation key detected (vkCode={0}), wParam={1}, injected={2}", vkCode, wParam, isInjected));

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
                            Action otherKeyHandler = OnOtherKeyPressedBeforeActivation;
                            if (otherKeyHandler != null)
                            {
                                _syncContext.Post(state =>
                                {
                                    try { otherKeyHandler(); }
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
                        // При отпускании клавиши активации проверяем, была ли нажата клавиша после
                        if (_keyPressedAfterActivation)
                        {
                            DebugLogger.Log("HookCallback: Activation key released but key was pressed after - blocking Ghost Mode");
                            // Блокируем активацию Ghost Mode
                            Action otherKeyHandler = OnOtherKeyPressedBeforeActivation;
                            if (otherKeyHandler != null)
                            {
                                _syncContext.Post(state =>
                                {
                                    try { otherKeyHandler(); }
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
                    }

                    // Вызов обработчика с обработкой исключений
                    if (handler != null)
                    {
                        _syncContext.Post(state =>
                        {
                            try
                            {
                                handler();
                            }
                            catch (Exception ex)
                            {
                                DebugLogger.Log(string.Format("Hook handler error: {0}", ex.Message));
                            }
                        }, null);
                    }

                    // Подавление клавиши активации:
                    // 1. Если Ghost Mode активен или в задержке после деактивации — подавляем всё
                    bool shouldSuppressGhost = _ghostLogic != null && _ghostLogic.ShouldSuppressWinKey;
                    DebugLogger.Log(string.Format("HookCallback: ShouldSuppressWinKey = {0}", shouldSuppressGhost));

                    if (shouldSuppressGhost)
                    {
                        DebugLogger.Log("HookCallback: SUPPRESSING activation key event (Ghost Mode)!");
                        return (IntPtr)1;
                    }
                    
                    // 2. При одиночном нажатии KEYUP (без других клавиш) — подавляем
                    //    и отправляем Esc + KEYUP чтобы предотвратить нежелательное поведение
                    if (wParam == (IntPtr)NativeMethods.WM_KEYUP && !_keyPressedAfterActivation && _pressedKeys.Count == 0)
                    {
                        DebugLogger.Log("HookCallback: Single activation key KEYUP - suppressing");
                        // При нажатой клавише активации кратковременно нажимаем Esc, затем отпускаем
                        _ignoringInjectedActivationUp = true;
                        SendEscActivationUpToPreventUnwantedBehavior(activationKey);
                        return (IntPtr)1;
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
        
        // Кратковременно нажимаем Esc при нажатой клавише активации, затем отпускаем
        // Это прерывает нежелательное поведение (например, открытие меню Пуск для Win)
        private void SendEscActivationUpToPreventUnwantedBehavior(int activationKey)
        {
            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[3];
            
            // 1. Нажимаем Esc (при всё ещё нажатой клавише активации)
            inputs[0].type = NativeMethods.INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = NativeMethods.VK_ESCAPE;
            inputs[0].U.ki.dwFlags = 0;
            inputs[0].U.ki.time = 0;
            inputs[0].U.ki.dwExtraInfo = IntPtr.Zero;

            // 2. Отпускаем Esc
            inputs[1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = NativeMethods.VK_ESCAPE;
            inputs[1].U.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;
            inputs[1].U.ki.time = 0;
            inputs[1].U.ki.dwExtraInfo = IntPtr.Zero;

            // 3. Отпускаем клавишу активации
            inputs[2].type = NativeMethods.INPUT_KEYBOARD;
            inputs[2].U.ki.wVk = (ushort)activationKey;
            inputs[2].U.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;
            inputs[2].U.ki.time = 0;
            inputs[2].U.ki.dwExtraInfo = IntPtr.Zero;

            NativeMethods.SendInput(3, inputs, NativeMethods.INPUT.Size);
            DebugLogger.Log(string.Format("SendEscActivationUpToPreventUnwantedBehavior: Sent Esc+Key {0} UP sequence", activationKey));
        }
    }
}
