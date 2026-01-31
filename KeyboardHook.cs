using System;
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

        public event Action OnLWinDown;
        public event Action OnLWinUp;

        public KeyboardHook(GhostLogic ghostLogic)
        {
            _ghostLogic = ghostLogic;
            _proc = HookCallback;
            _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();
            _hookID = SetHook(_proc);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_hookID != IntPtr.Zero)
                {
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
                    // Безопасное копирование события для проверки null
                    Action handler = null;
                    
                    if (wParam == (IntPtr)NativeMethods.WM_KEYDOWN)
                    {
                        handler = OnLWinDown;
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
                                // Не прерываем цепочку хуков при ошибке обработчика
                                System.Diagnostics.Debug.WriteLine("Hook handler error: " + ex.Message);
                            }
                        }, null);
                    }
                    
                    // Подавляем стандартное поведение Win клавиши только если активен Ghost Mode
                    // При коротком нажатии позволяем Windows обработать событие (открыть меню Пуск)
                    if (_ghostLogic != null && _ghostLogic.ShouldSuppressWinKey)
                    {
                        return (IntPtr)1;
                    }
                }
            }
            return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
    }
}
