using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace PeekThrough
{
    // Класс для хранения состояния окна в режиме Ghost Mode
    internal class GhostWindowState
    {
        public IntPtr Hwnd { get; set; }
        public int OriginalExStyle { get; set; }
        public bool WasAlreadyLayered { get; set; }
    }

    internal class GhostLogic : IDisposable
    {
        // Тип активации Ghost Mode
        public enum ActivationType
        {
            Keyboard, // Активация с помощью клавиатуры (Win)
            Mouse     // Активация с помощью мыши
        }
        
        // Публичное свойство для проверки, нужно ли подавлять стандартное поведение Win
        public bool ShouldSuppressWinKey
        {
            get
            {
                lock (_lockObject)
                {
                    // Подавляем Win когда Ghost Mode активен ИЛИ в течение задержки после деактивации
                    return _ghostModeActive || _suppressWinKey;
                }
            }
        }
        
        // Публичное свойство для проверки, активен ли Ghost Mode
        public bool IsGhostModeActive
        {
            get
            {
                lock (_lockObject)
                {
                    return _ghostModeActive;
                }
            }
        }
        
        // Публичное свойство для проверки, активна ли клавиша мыши
        public bool IsMouseActivationActive
        {
            get
            {
                lock (_lockObject)
                {
                    return _isMouseButtonDown;
                }
            }
        }

        // Публичное свойство для проверки, нажата ли клавиша Win
        public bool IsLWinPressed
        {
            get
            {
                lock (_lockObject)
                {
                    return _isLWinDown;
                }
            }
        }
        
        // Публичное свойство для получения типа активации
        public ActivationType CurrentActivationType
        {
            get
            {
                lock (_lockObject)
                {
                    return _activationType;
                }
            }
        }

        // Константы
        private const int GHOST_MODE_ACTIVATION_DELAY_MS = 1000;
        private const int SUPPRESS_WIN_AFTER_DEACTIVATE_MS = 100; // Задержка подавления Win после деактивации
        private const int BEEP_FREQUENCY_ACTIVATE = 1000;
        private const int BEEP_FREQUENCY_DEACTIVATE = 500;
        private const int BEEP_FREQUENCY_ADD = 1500; // Более высокий тон для добавления окна
        private const int BEEP_DURATION_MS = 50;
        private const byte GHOST_OPACITY = 38; // ~15% opacity
        private const byte FULL_OPACITY = 255;
        private const int TOOLTIP_WIDTH = 140;
        private const int TOOLTIP_HEIGHT = 40;
        private const int TOOLTIP_OFFSET_X = 20;
        private const int TOOLTIP_OFFSET_Y = 20;

        private readonly object _lockObject = new object();
        private Timer _timer;
        private Timer _suppressWinTimer; // Таймер для подавления Win после деактивации
        private bool _isLWinDown;
        private bool _isMouseButtonDown; // Флаг: нажата ли выбранная кнопка мыши
        private bool _ghostModeActive;
        private bool _suppressWinKey; // Флаг: подавлять Win после деактивации
        private bool _timerFired; // Флаг: сработал ли таймер (длинное нажатие)
        private ActivationType _activationType; // Тип активации (клавиатура или мышь)
        
        // Список окон в Ghost Mode
        private List<GhostWindowState> _ghostWindows = new List<GhostWindowState>();

        // Текущее целевое окно (для добавления)
        private IntPtr _currentTargetHwnd = IntPtr.Zero;

        // Handle окна, которое сейчас под курсором (для отслеживания изменений)
        private IntPtr _lastActivatedHwnd = IntPtr.Zero;

        private Form _tooltipForm;
        private Label _tooltipLabel;
        
        // Флаг для предотвращения повторного вызова Dispose
        private bool _disposed = false;
        
        // Константы: HashSet для игнорируемых системных окон
        private static readonly HashSet<string> IgnoredWindowClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Progman", "WorkerW", "Shell_TrayWnd"
        };

        public GhostLogic(ActivationType activationType = ActivationType.Keyboard)
        {
            _activationType = activationType;
            _timer = new Timer();
            _timer.Interval = GHOST_MODE_ACTIVATION_DELAY_MS;
            _timer.Tick += OnTimerTick;

            _suppressWinTimer = new Timer();
            _suppressWinTimer.Interval = SUPPRESS_WIN_AFTER_DEACTIVATE_MS;
            _suppressWinTimer.Tick += OnSuppressWinTimerTick;

            // Initialize Tooltip Form с прозрачностью настройками
            _tooltipForm = new Form();
            _tooltipForm.FormBorderStyle = FormBorderStyle.None;
            _tooltipForm.ShowInTaskbar = false;
            _tooltipForm.TopMost = true;
            _tooltipForm.BackColor = Color.FromArgb(255, 255, 225); // LightYellow
            _tooltipForm.Size = new Size(TOOLTIP_WIDTH, TOOLTIP_HEIGHT);
            _tooltipForm.StartPosition = FormStartPosition.Manual;
            _tooltipForm.Opacity = 0.95;
            
            // Отключаем взаимодействие: окно без фокуса и ввода
            _tooltipForm.Enabled = false;
            _tooltipForm.ShowIcon = false;
            _tooltipForm.ControlBox = false;
            
            _tooltipLabel = new Label();
            _tooltipLabel.Text = "Ghost Mode";
            _tooltipLabel.AutoSize = true;
            _tooltipLabel.Location = new Point(5, 5);
            _tooltipLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _tooltipForm.Controls.Add(_tooltipLabel);
            _tooltipForm.AutoSize = true;
            _tooltipLabel.AutoSize = true;
            
            // Устанавливаем стиль окна после создания handle окна
            // чтобы окно было прозрачным для кликов
            _tooltipForm.Load += (s, e) =>
            {
                int exStyle = NativeMethods.GetWindowLongPtr(_tooltipForm.Handle, NativeMethods.GWL_EXSTYLE).ToInt32();
                NativeMethods.SetWindowLongPtr(_tooltipForm.Handle, NativeMethods.GWL_EXSTYLE,
                    new IntPtr(exStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE));
            };
        }

        public void OnKeyDown()
        {
            if (_activationType == ActivationType.Mouse)
            {
                // Если используется активация мышью, игнорируем нажатие клавиши
                return;
            }
            
            DebugLogger.Log("=== OnKeyDown START ===");
            lock (_lockObject)
            {
                if (_isLWinDown)
                {
                    DebugLogger.Log("OnKeyDown: _isLWinDown already true, returning");
                    return;
                }
                _isLWinDown = true;
                _timerFired = false; // Сбрасываем флаг таймера
                DebugLogger.LogState("OnKeyDown", _isLWinDown, _ghostModeActive, ShouldSuppressWinKey, _timerFired);

                // Перезапускаем таймер (для активации ИЛИ деактивации)
                _timer.Stop();
                _timer.Start();
                
                DebugLogger.Log("OnKeyDown: Started ghost mode timer");
            }
        }
        
        public void OnMouseButtonDown()
        {
            if (_activationType == ActivationType.Keyboard)
            {
                // Если используется активация клавиатурой, игнорируем нажатие мыши
                return;
            }
            
            DebugLogger.Log("=== OnMouseButtonDown START ===");
            lock (_lockObject)
            {
                if (_isMouseButtonDown)
                {
                    DebugLogger.Log("OnMouseButtonDown: _isMouseButtonDown already true, returning");
                    return;
                }
                _isMouseButtonDown = true;
                _timerFired = false; // Сбрасываем флаг таймера
                DebugLogger.LogState("OnMouseButtonDown", _isMouseButtonDown, _ghostModeActive, ShouldSuppressWinKey, _timerFired);

                // Перезапускаем таймер (для активации ИЛИ деактивации)
                _timer.Stop();
                _timer.Start();
                
                DebugLogger.Log("OnMouseButtonDown: Started ghost mode timer");
            }
        }

        public void OnKeyUp()
        {
            DebugLogger.Log("=== OnKeyUp START ===");
            lock (_lockObject)
            {
                DebugLogger.LogState("OnKeyUp ENTER", _isLWinDown, _ghostModeActive, ShouldSuppressWinKey, _timerFired);

                _isLWinDown = false;
                _timer.Stop();

                if (_ghostModeActive)
                {
                    // Отпускание Win при активном Ghost Mode - деактивируем
                    DebugLogger.Log("OnKeyUp: Ghost Mode active on release - deactivating Ghost Mode");
                    RestoreAllWindows();
                    HideTooltip();
                    NativeMethods.Beep(BEEP_FREQUENCY_DEACTIVATE, BEEP_DURATION_MS);
                    _ghostModeActive = false;
                    _ghostWindows.Clear();
                    _currentTargetHwnd = IntPtr.Zero;
                    _lastActivatedHwnd = IntPtr.Zero;

                    // Включаем подавление Win на короткое время после деактивации
                    _suppressWinKey = true;
                    _suppressWinTimer.Start();
                    DebugLogger.Log("OnKeyUp: Started Win suppression timer");
                }
                else if (!_timerFired)
                {
                    DebugLogger.Log("OnKeyUp: Ghost mode not active and timer didn't fire - short press");
                }

                DebugLogger.LogState("OnKeyUp EXIT", _isLWinDown, _ghostModeActive, ShouldSuppressWinKey, _timerFired);
            }
        }

        public void OnMouseButtonUp()
        {
            if (_activationType == ActivationType.Keyboard)
            {
                // Если используется активация клавиатурой, игнорируем отпускание мыши
                return;
            }
            
            DebugLogger.Log("=== OnMouseButtonUp START ===");
            lock (_lockObject)
            {
                DebugLogger.LogState("OnMouseButtonUp ENTER", _isMouseButtonDown, _ghostModeActive, ShouldSuppressWinKey, _timerFired);

                _isMouseButtonDown = false;
                _timer.Stop();

                if (_ghostModeActive)
                {
                    // Отпускание кнопки мыши при активном Ghost Mode - деактивируем
                    DebugLogger.Log("OnMouseButtonUp: Ghost Mode active on release - deactivating Ghost Mode");
                    RestoreAllWindows();
                    HideTooltip();
                    NativeMethods.Beep(BEEP_FREQUENCY_DEACTIVATE, BEEP_DURATION_MS);
                    _ghostModeActive = false;
                    _ghostWindows.Clear();
                    _currentTargetHwnd = IntPtr.Zero;
                    _lastActivatedHwnd = IntPtr.Zero;
                    // Не подавляем Win-клавишу при активации мышью —
                    // SendWinReleaseWithCtrl не нужен, т.к. Win не использовалась
                }
                else if (!_timerFired)
                {
                    DebugLogger.Log("OnMouseButtonUp: Ghost mode not active and timer didn't fire - short press");
                }

                DebugLogger.LogState("OnMouseButtonUp EXIT", _isMouseButtonDown, _ghostModeActive, ShouldSuppressWinKey, _timerFired);
            }
        }
        
        // Публичный метод для деактивации Ghost Mode извне (например, при нажатии другой клавиши)
        public void DeactivateGhostMode()
        {
            DebugLogger.Log("=== DeactivateGhostMode START ===");
            lock (_lockObject)
            {
                if (!_ghostModeActive)
                {
                    DebugLogger.Log("DeactivateGhostMode: Ghost mode not active, returning");
                    return;
                }

                DebugLogger.LogState("DeactivateGhostMode", _isLWinDown, _ghostModeActive, ShouldSuppressWinKey, _timerFired);

                _isLWinDown = false;
                _isMouseButtonDown = false; // Также сбрасываем состояние кнопки мыши
                _timerFired = false;
                _timer.Stop();

                // Deactivate Ghost Mode - восстанавливаем все окна
                RestoreAllWindows();
                HideTooltip();
                NativeMethods.Beep(BEEP_FREQUENCY_DEACTIVATE, BEEP_DURATION_MS);
                _ghostModeActive = false;
                _ghostWindows.Clear();
                _currentTargetHwnd = IntPtr.Zero;
            }
        }
        
        // Публичный метод для блокировки активации Ghost Mode (когда нажата другая клавиша до ИЛИ после Win)
        public void BlockGhostMode()
        {
            DebugLogger.Log("=== BlockGhostMode ===");
            lock (_lockObject)
            {
                // Если еще не было нажатия кнопки Win, отменяем возможную активацию Ghost Mode
                // ИЛИ если Win отпущен, предотвращаем активацию
                _isLWinDown = false;
                _timerFired = false;
                _timer.Stop();
                
                DebugLogger.Log("BlockGhostMode: Ghost mode activation blocked");
            }
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            DebugLogger.Log("=== OnTimerTick ===");
            lock (_lockObject)
            {
                _timer.Stop(); // One-shot trigger check
                
                bool shouldActivate = false;
                
                if (_activationType == ActivationType.Keyboard)
                {
                    shouldActivate = _isLWinDown;
                }
                else if (_activationType == ActivationType.Mouse)
                {
                    shouldActivate = _isMouseButtonDown;
                }
                
                if (shouldActivate)
                {
                    _timerFired = true; // Отмечаем что было длинное нажатие (таймер сработал)
                    DebugLogger.LogState(string.Format("OnTimerTick - activating ({0})", _activationType), _isLWinDown, _ghostModeActive, ShouldSuppressWinKey, _timerFired);
                    ActivateGhostMode();
                }
                else
                {
                    DebugLogger.Log(string.Format("OnTimerTick: {0} button is false, not activating", _activationType));
                }
            }
        }

        private void OnSuppressWinTimerTick(object sender, EventArgs e)
        {
            DebugLogger.Log("=== OnSuppressWinTimerTick ===");
            lock (_lockObject)
            {
                _suppressWinTimer.Stop();
                _suppressWinKey = false;
            }

            // Эмулируем Win+Ctrl комбинацию для предотвращения меню Пуск
            SendWinReleaseWithCtrl();
        }

        private void SendWinReleaseWithCtrl()
        {
            DebugLogger.Log("SendWinReleaseWithCtrl: Pressing Ctrl, releasing Win, then releasing Ctrl");

            // Последовательность: Ctrl down -> Win up -> Ctrl up
            // Важно: Win отпускается пока Ctrl ещё нажат, чтобы предотвратить открытие меню Пуск
            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[3];
            
            // 1. Нажимаем Ctrl
            inputs[0].type = NativeMethods.INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = NativeMethods.VK_CONTROL;
            inputs[0].U.ki.dwFlags = 0;
            inputs[0].U.ki.time = 0;
            inputs[0].U.ki.dwExtraInfo = IntPtr.Zero;

            // 2. Отпускаем Win (пока Ctrl ещё нажат!)
            inputs[1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = NativeMethods.VK_LWIN;
            inputs[1].U.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;
            inputs[1].U.ki.time = 0;
            inputs[1].U.ki.dwExtraInfo = IntPtr.Zero;

            // 3. Отпускаем Ctrl
            inputs[2].type = NativeMethods.INPUT_KEYBOARD;
            inputs[2].U.ki.wVk = NativeMethods.VK_CONTROL;
            inputs[2].U.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;
            inputs[2].U.ki.time = 0;
            inputs[2].U.ki.dwExtraInfo = IntPtr.Zero;

            NativeMethods.SendInput(3, inputs, NativeMethods.INPUT.Size);
        }

        private void ActivateGhostMode()
        {
            DebugLogger.Log("=== ActivateGhostMode START ===");
            Point cursorPos;
            if (!NativeMethods.GetCursorPos(out cursorPos))
            {
                DebugLogger.Log("ActivateGhostMode: Failed to get cursor position");
                return;
            }

            IntPtr hwnd = NativeMethods.WindowFromPoint(cursorPos);
            if (hwnd == IntPtr.Zero)
            {
                DebugLogger.Log("ActivateGhostMode: WindowFromPoint returned zero");
                return;
            }

            // Get the root window (ancestor) because we might be hovering a child control
            hwnd = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
            DebugLogger.Log(string.Format("ActivateGhostMode: Root window handle: {0}", hwnd));

            // Получаем класс окна и фильтруем системные окна
            var className = new StringBuilder(256);
            if (NativeMethods.GetClassName(hwnd, className, className.Capacity) > 0)
            {
                string cls = className.ToString();
                DebugLogger.Log(string.Format("ActivateGhostMode: Window class: {0}", cls));
                if (IgnoredWindowClasses.Contains(cls))
                {
                    DebugLogger.Log("ActivateGhostMode: Ignored system window");
                    return;
                }
            }

            // Проверяем, не то ли это же окно, что уже активировано
            if (hwnd == _lastActivatedHwnd && _ghostModeActive)
            {
                DebugLogger.Log("ActivateGhostMode: Same window, just showing tooltip");
                ShowTooltip(cursorPos);
                return;
            }

            // Ghost Mode уже активен на другом окне - игнорируем
            if (_ghostModeActive)
            {
                DebugLogger.Log("ActivateGhostMode: Ghost Mode already active on different window, ignoring");
                ShowTooltip(cursorPos);
                return;
            }

            lock (_lockObject)
            {
                _currentTargetHwnd = hwnd;
                _lastActivatedHwnd = hwnd;
                _ghostModeActive = true;
                DebugLogger.LogState("ActivateGhostMode - set active", _isLWinDown, _ghostModeActive, ShouldSuppressWinKey, _timerFired);
            }

            try
            {
                // Сохраняем оригинальный стиль окна
                int originalExStyle = NativeMethods.GetWindowLongPtr(_currentTargetHwnd, NativeMethods.GWL_EXSTYLE).ToInt32();
                bool wasAlreadyLayered = (originalExStyle & NativeMethods.WS_EX_LAYERED) != 0;

                // Apply Transparency
                int newStyle = originalExStyle | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT;
                NativeMethods.SetWindowLongPtr(_currentTargetHwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(newStyle));
                NativeMethods.SetLayeredWindowAttributes(_currentTargetHwnd, 0, GHOST_OPACITY, NativeMethods.LWA_ALPHA);

                // Сохраняем состояние окна в список
                var windowState = new GhostWindowState
                {
                    Hwnd = _currentTargetHwnd,
                    OriginalExStyle = originalExStyle,
                    WasAlreadyLayered = wasAlreadyLayered
                };

                lock (_lockObject)
                {
                    _ghostWindows.Add(windowState);
                }

                // Show Tooltip в позиции курсора
                ShowTooltip(cursorPos);

                // Звук активации
                NativeMethods.Beep(BEEP_FREQUENCY_ACTIVATE, BEEP_DURATION_MS);
                DebugLogger.Log("ActivateGhostMode: Window activated as single ghost window");
            }
            catch (Exception ex)
            {
                DebugLogger.Log(string.Format("ActivateGhostMode ERROR: {0}", ex.Message));
                lock (_lockObject)
                {
                    _ghostWindows.RemoveAll(w => w.Hwnd == _currentTargetHwnd);
                }
            }
        }

        private void RestoreAllWindows()
        {
            DebugLogger.Log("=== RestoreAllWindows ===");
            lock (_lockObject)
            {
                DebugLogger.Log(string.Format("Restoring {0} windows", _ghostWindows.Count));
                foreach (var windowState in _ghostWindows)
                {
                    RestoreSingleWindow(windowState);
                }
                _ghostWindows.Clear();
                _currentTargetHwnd = IntPtr.Zero;
            }
        }

        private void RestoreSingleWindow(GhostWindowState windowState)
        {
            if (windowState.Hwnd == IntPtr.Zero)
                return;
                
            // Проверяем валидность окна перед восстановлением
            if (!NativeMethods.IsWindow(windowState.Hwnd))
            {
                // Окно уже закрыто, пропускаем
                DebugLogger.Log(string.Format("RestoreSingleWindow: Window {0} is no longer valid", windowState.Hwnd));
                return;
            }

            try
            {
                NativeMethods.SetWindowLongPtr(windowState.Hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(windowState.OriginalExStyle));
                
                // Восстанавливаем прозрачность
                if (windowState.WasAlreadyLayered)
                {
                    NativeMethods.SetLayeredWindowAttributes(windowState.Hwnd, 0, FULL_OPACITY, NativeMethods.LWA_ALPHA);
                }
                DebugLogger.Log(string.Format("RestoreSingleWindow: Restored window {0}", windowState.Hwnd));
            }
            catch (Exception ex)
            {
                // Логируем ошибку в Debug output
                DebugLogger.Log(string.Format("RestoreSingleWindow ERROR: {0}", ex.Message));
            }
        }

        private void ShowTooltip(Point location)
        {
            lock (_lockObject)
            {
                _tooltipLabel.Text = "Ghost Mode";
            }

            _tooltipForm.Location = new Point(location.X + TOOLTIP_OFFSET_X, location.Y + TOOLTIP_OFFSET_Y);
            if (!_tooltipForm.Visible)
                _tooltipForm.Show();
        }

        private void HideTooltip()
        {
             _tooltipForm.Hide();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            lock (_lockObject)
            {
                if (_disposed)
                    return;
                _disposed = true;
            }

            if (disposing)
            {
                Timer timerToDispose = null;
                Timer suppressTimerToDispose = null;
                Form formToDispose = null;

                lock (_lockObject)
                {
                    timerToDispose = _timer;
                    _timer = null;
                    suppressTimerToDispose = _suppressWinTimer;
                    _suppressWinTimer = null;
                    formToDispose = _tooltipForm;
                    _tooltipForm = null;
                }

                if (timerToDispose != null) timerToDispose.Stop();
                if (timerToDispose != null) timerToDispose.Dispose();
                if (suppressTimerToDispose != null) suppressTimerToDispose.Stop();
                if (suppressTimerToDispose != null) suppressTimerToDispose.Dispose();
                if (formToDispose != null) formToDispose.Dispose();

                RestoreAllWindows();
            }
        }

        ~GhostLogic()
        {
            Dispose(false);
        }
    }
}
