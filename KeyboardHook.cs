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
        
        // Отслеживание нажатых клавиш (кроме Win)
        private HashSet<int> _pressedKeys = new HashSet<int>();

        public event Action OnLWinDown;
        public event Action OnLWinUp;
        
        // Событие для уведомления о нажатии другой клавиши перед Win
        public event Action OnOtherKeyPressedBeforeWin;

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
            if (!_disposed)
            {
                if (_hookID != IntPtr.Zero)
                {
                    DebugLogger.Log("KeyboardHook.Dispose: Unhooking keyboard hook");
                    NativeMethods.UnhookWindowsHookEx(_hookID);
                    _hookID = IntPtr.Zero;
                }
                _disposed = true;
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
                
                if (vkCode == NativeMethods.VK_LWIN)
                {
                    DebugLogger.Log(string.Format("HookCallback: LWin detected, wParam={0}", wParam));
                    
                    // Безопасное копирование события для проверки null
                    Action handler = null;
                    
                    if (wParam == (IntPtr)NativeMethods.WM_KEYDOWN)
                    {
                        // Если нажата другая клавиша до Win - не активируем Ghost Mode
                        if (_pressedKeys.Count > 0)
                        {
                            DebugLogger.Log(string.Format("HookCallback: Other keys pressed before Win ({0}), blocking Ghost Mode", _pressedKeys.Count));
                            Action otherKeyHandler = OnOtherKeyPressedBeforeWin;
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
                            handler = OnLWinDown;
                        }
                    }
                    else if (wParam == (IntPtr)NativeMethods.WM_KEYUP)
                    {
                        handler = OnLWinUp;
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
                    
                    // Подавляем стандартное поведение Win клавиши только если активен Ghost Mode
                    bool shouldSuppress = _ghostLogic != null && _ghostLogic.ShouldSuppressWinKey;
                    DebugLogger.Log(string.Format("HookCallback: ShouldSuppressWinKey = {0}", shouldSuppress));
                    
                    if (shouldSuppress)
                    {
                        DebugLogger.Log("HookCallback: SUPPRESSING Win key event!");
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
