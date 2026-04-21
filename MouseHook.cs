using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace GhostThrough
{
    internal class MouseHook : IDisposable
    {
        private NativeMethods.LowLevelMouseProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private SynchronizationContext _syncContext;
        private bool _disposed = false;
        private IActivationHost _activationHost;
        private readonly object _lockObject = new object();
        
        // Отслеживание нажатых кнопок мыши
        private HashSet<int> _pressedMouseButtons = new HashSet<int>();
        
        // Флаг: была ли нажата другая кнопка мыши ПОСЛЕ выбранной
        private bool _mouseButtonPressedAfterSelected = false;
        
        // Выбранная кнопка мыши для активации Ghost Mode
        private volatile int _selectedMouseButton;
        
        // Состояние нажатия выбранной кнопки мыши
        private bool _isMouseButtonDown = false;

        public event Action OnSelectedMouseDown;
        public event Action OnSelectedMouseUp;
        
        // Событие для уведомления о нажатии другой кнопки мыши перед выбранной
        public event Action OnOtherMouseButtonPressedBeforeSelected;

        public MouseHook(IActivationHost activationHost, int selectedMouseButton = NativeMethods.VK_MBUTTON)
        {
            _activationHost = activationHost;
            _selectedMouseButton = selectedMouseButton;
            _proc = HookCallback;
            _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();
            _hookID = SetHook(_proc);
            DebugLogger.Log(string.Format("MouseHook initialized, hook ID: {0}, selected mouse button: {1}", _hookID, _selectedMouseButton));
        }

        public void Dispose()
        {
            IntPtr hookToDispose = IntPtr.Zero;

            lock (_lockObject)
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
                DebugLogger.Log("MouseHook.Dispose: Unhooking mouse hook");
                NativeMethods.UnhookWindowsHookEx(hookToDispose);
            }

            GC.SuppressFinalize(this);
        }

        private IntPtr SetHook(NativeMethods.LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, proc,
                    NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int mouseButton;
                bool isButtonDown;
                bool isButtonUp;
                ResolveMouseButton(wParam, lParam, out mouseButton, out isButtonDown, out isButtonUp);
                
                // Считываем текущую selected кнопку (volatile)
                int currentSelectedButton = _selectedMouseButton;
                
                if (mouseButton == currentSelectedButton)
                {
                    DebugLogger.Log(string.Format("HookCallback: Selected mouse button detected, wParam={0}, button={1}", wParam, mouseButton));

                    // Безопасное копирование события для проверки null
                    Action handler = null;

                    lock (_lockObject)
                    {
                        if (isButtonDown)
                        {
                            // Сбрасываем флаг при новом нажатии выбранной кнопки мыши
                            _mouseButtonPressedAfterSelected = false;

                            // Если нажата другая кнопка мыши до выбранной - не активируем Ghost Mode
                            if (_pressedMouseButtons.Count > 0)
                            {
                                DebugLogger.Log(string.Format("HookCallback: Other mouse buttons pressed before selected ({0}), blocking Ghost Mode", _pressedMouseButtons.Count));
                                var handlerBlocked = OnOtherMouseButtonPressedBeforeSelected;
                                PostHandler(handlerBlocked, "OtherMouseButton handler error");
                            }
                            else
                            {
                                _isMouseButtonDown = true;
                                handler = OnSelectedMouseDown;
                            }
                        }
                        else if (isButtonUp)
                        {
                            // При отпускании выбранной кнопки мыши проверяем, была ли нажата другая кнопка после
                            if (_mouseButtonPressedAfterSelected)
                            {
                                DebugLogger.Log("HookCallback: Selected mouse button released but other button was pressed after - blocking Ghost Mode");
                                // Блокируем активацию Ghost Mode
                                var handlerBlocked = OnOtherMouseButtonPressedBeforeSelected;
                                PostHandler(handlerBlocked, "OtherMouseButton handler error on selected button release");
                            }
                            else
                            {
                                _isMouseButtonDown = false;
                                handler = OnSelectedMouseUp;
                            }

                            // Сбрасываем флаг после обработки
                            _mouseButtonPressedAfterSelected = false;
                        }
                    }

                    // Вызов обработчика с обработкой исключений через syncContext
                    PostHandler(handler, "Mouse hook handler error");
                }
                else
                {
                    lock (_lockObject)
                    {
                        // Отслеживание нажатия/отпускания других кнопок мыши
                        if (isButtonDown)
                        {
                            _pressedMouseButtons.Add(mouseButton);
                            DebugLogger.Log(string.Format("HookCallback: Other mouse button DOWN, button={0}, total pressed: {1}", mouseButton, _pressedMouseButtons.Count));

                            // Если выбранная кнопка мыши сейчас нажата, отмечаем что другая кнопка нажата ПОСЛЕ неё
                            if (_activationHost != null && _isMouseButtonDown)
                            {
                                _mouseButtonPressedAfterSelected = true;
                                DebugLogger.Log("HookCallback: Other mouse button pressed AFTER selected - will block Ghost Mode");
                            }
                        }
                        else if (isButtonUp)
                        {
                            _pressedMouseButtons.Remove(mouseButton);
                            DebugLogger.Log(string.Format("HookCallback: Other mouse button UP, button={0}, remaining: {1}", mouseButton, _pressedMouseButtons.Count));
                        }
                    }

                    // Mouse clicks no longer deactivate Ghost Mode (toggle mode)
                    // User can interact with windows behind the ghost window normally
                }
            }
            return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
        
        // Метод для изменения выбранной кнопки мыши
        public void SetSelectedMouseButton(int mouseButton)
        {
            lock (_lockObject)
            {
                _selectedMouseButton = mouseButton;
                _pressedMouseButtons.Clear();
                _mouseButtonPressedAfterSelected = false;
                _isMouseButtonDown = false;
            }
            DebugLogger.Log(string.Format("MouseHook: Selected mouse button changed to {0}", mouseButton));
        }
        
        // Свойство для получения текущей выбранной кнопки мыши
        public int SelectedMouseButton
        {
            get { return _selectedMouseButton; }
        }

        private static void ResolveMouseButton(IntPtr wParam, IntPtr lParam, out int mouseButton, out bool isButtonDown, out bool isButtonUp)
        {
            int mouseMessage = (int)wParam;
            mouseButton = 0;
            isButtonDown = false;
            isButtonUp = false;

            switch (mouseMessage)
            {
                case NativeMethods.WM_LBUTTONDOWN:
                    mouseButton = NativeMethods.VK_LBUTTON;
                    isButtonDown = true;
                    break;
                case NativeMethods.WM_LBUTTONUP:
                    mouseButton = NativeMethods.VK_LBUTTON;
                    isButtonUp = true;
                    break;
                case NativeMethods.WM_RBUTTONDOWN:
                    mouseButton = NativeMethods.VK_RBUTTON;
                    isButtonDown = true;
                    break;
                case NativeMethods.WM_RBUTTONUP:
                    mouseButton = NativeMethods.VK_RBUTTON;
                    isButtonUp = true;
                    break;
                case NativeMethods.WM_MBUTTONDOWN:
                    mouseButton = NativeMethods.VK_MBUTTON;
                    isButtonDown = true;
                    break;
                case NativeMethods.WM_MBUTTONUP:
                    mouseButton = NativeMethods.VK_MBUTTON;
                    isButtonUp = true;
                    break;
                case NativeMethods.WM_XBUTTONDOWN:
                case NativeMethods.WM_XBUTTONUP:
                    var hookStruct = (NativeMethods.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT));
                    int xButton = (hookStruct.mouseData >> 16) & 0xFFFF;
                    mouseButton = xButton == NativeMethods.XBUTTON1 ? NativeMethods.VK_XBUTTON1 : NativeMethods.VK_XBUTTON2;
                    isButtonDown = mouseMessage == NativeMethods.WM_XBUTTONDOWN;
                    isButtonUp = mouseMessage == NativeMethods.WM_XBUTTONUP;
                    break;
            }
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
