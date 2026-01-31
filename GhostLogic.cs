using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace PeekThrough
{
    internal class GhostLogic : IDisposable
    {
        // Публичное свойство для проверки, нужно ли подавлять клавишу Win
        public bool ShouldSuppressWinKey { get; private set; }
        
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

        // Константы
        private const int GHOST_MODE_ACTIVATION_DELAY_MS = 500;
        private const int BEEP_FREQUENCY_ACTIVATE = 1000;
        private const int BEEP_FREQUENCY_DEACTIVATE = 500;
        private const int BEEP_DURATION_MS = 50;
        private const byte GHOST_OPACITY = 38; // ~15% opacity
        private const byte FULL_OPACITY = 255;
        private const int TOOLTIP_WIDTH = 120;
        private const int TOOLTIP_HEIGHT = 30;
        private const int TOOLTIP_OFFSET_X = 20;
        private const int TOOLTIP_OFFSET_Y = 20;

        private readonly object _lockObject = new object();
        private Timer _timer;
        private bool _isLWinDown;
        private bool _ghostModeActive;
        private IntPtr _targetHwnd = IntPtr.Zero;
        private int _originalExStyle;
        private bool _hasOriginalExStyle = false;

        private Form _tooltipForm;
        private Label _tooltipLabel;
        
        // Флаг для отслеживания состояния Dispose
        private bool _disposed = false;
        
        // Оптимизация: HashSet для проверки игнорируемых классов окон
        private static readonly HashSet<string> IgnoredWindowClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Progman", "WorkerW", "Shell_TrayWnd"
        };

        public GhostLogic()
        {
            _timer = new Timer();
            _timer.Interval = GHOST_MODE_ACTIVATION_DELAY_MS;
            _timer.Tick += OnTimerTick;

            // Initialize Tooltip Form с улучшенными настройками
            _tooltipForm = new Form();
            _tooltipForm.FormBorderStyle = FormBorderStyle.None;
            _tooltipForm.ShowInTaskbar = false;
            _tooltipForm.TopMost = true;
            _tooltipForm.BackColor = Color.FromArgb(255, 255, 225); // LightYellow
            _tooltipForm.Size = new Size(TOOLTIP_WIDTH, TOOLTIP_HEIGHT);
            _tooltipForm.StartPosition = FormStartPosition.Manual;
            _tooltipForm.Opacity = 0.95;
            
            // Ключевые улучшения: запрет фокуса и кликов
            _tooltipForm.Enabled = false;
            _tooltipForm.ShowIcon = false;
            _tooltipForm.ControlBox = false;
            
            _tooltipLabel = new Label();
            _tooltipLabel.Text = "👻 Ghost Mode";
            _tooltipLabel.AutoSize = true;
            _tooltipLabel.Location = new Point(5, 5);
            _tooltipLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _tooltipForm.Controls.Add(_tooltipLabel);
            _tooltipForm.AutoSize = true;
            _tooltipLabel.AutoSize = true;
            
            // Установка стиля окна для полной прозрачности для событий мыши
            // Делаем это после создания handle формы
            _tooltipForm.Load += (s, e) =>
            {
                int exStyle = NativeMethods.GetWindowLongPtr(_tooltipForm.Handle, NativeMethods.GWL_EXSTYLE).ToInt32();
                NativeMethods.SetWindowLongPtr(_tooltipForm.Handle, NativeMethods.GWL_EXSTYLE,
                    new IntPtr(exStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE));
            };
        }

        public void OnKeyDown()
        {
            lock (_lockObject)
            {
                if (_isLWinDown) return;
                _isLWinDown = true;
                _timer.Start();
            }
        }

        public void OnKeyUp()
        {
            lock (_lockObject)
            {
                _isLWinDown = false;
                _timer.Stop();

                if (_ghostModeActive)
                {
                    // Deactivate Ghost Mode
                    RestoreWindow();
                    HideTooltip();
                    NativeMethods.Beep(BEEP_FREQUENCY_DEACTIVATE, BEEP_DURATION_MS);
                    _ghostModeActive = false;
                    ShouldSuppressWinKey = false;
                }
                else
                {
                    // It was a short press - Windows will handle the key event normally
                    // No need to simulate the key press since we're not suppressing it
                }
            }
        }

        // Публичный метод для деактивации Ghost Mode извне (например, при нажатии другой клавиши)
        public void DeactivateGhostMode()
        {
            lock (_lockObject)
            {
                if (!_ghostModeActive) return;
                
                _isLWinDown = false;
                _timer.Stop();
                
                // Deactivate Ghost Mode
                RestoreWindow();
                HideTooltip();
                NativeMethods.Beep(BEEP_FREQUENCY_DEACTIVATE, BEEP_DURATION_MS);
                _ghostModeActive = false;
                ShouldSuppressWinKey = false;
            }
        }
        
        // Публичный метод для блокировки активации Ghost Mode (когда другая клавиша нажата до Win)
        public void BlockGhostMode()
        {
            lock (_lockObject)
            {
                // Если уже нажата другая клавиша, отменяем возможность активации Ghost Mode
                _isLWinDown = false;
                _timer.Stop();
            }
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            lock (_lockObject)
            {
                _timer.Stop(); // One-shot trigger check
                if (_isLWinDown)
                {
                    ActivateGhostMode();
                }
            }
        }

        private void ActivateGhostMode()
        {
            Point cursorPos;
            if (!NativeMethods.GetCursorPos(out cursorPos))
                return;
                
            IntPtr hwnd = NativeMethods.WindowFromPoint(cursorPos);
            if (hwnd == IntPtr.Zero)
                return;
                
            // Get the root window (ancestor) because we might be hovering a child control
            hwnd = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);

            // Проверка класса окна с минимальными аллокациями
            var className = new StringBuilder(256);
            if (NativeMethods.GetClassName(hwnd, className, className.Capacity) > 0)
            {
                string cls = className.ToString();
                if (IgnoredWindowClasses.Contains(cls))
                {
                    // Игнорируем системные окна
                    lock (_lockObject)
                    {
                        _ghostModeActive = true;
                    }
                    return;
                }
            }

            lock (_lockObject)
            {
                _targetHwnd = hwnd;
                _ghostModeActive = true;
                ShouldSuppressWinKey = true;
            }

            try
            {
                // Apply Transparency
                _originalExStyle = NativeMethods.GetWindowLongPtr(_targetHwnd, NativeMethods.GWL_EXSTYLE).ToInt32();
                _hasOriginalExStyle = true;

                int newStyle = _originalExStyle | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT;
                NativeMethods.SetWindowLongPtr(_targetHwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(newStyle));
                NativeMethods.SetLayeredWindowAttributes(_targetHwnd, 0, GHOST_OPACITY, NativeMethods.LWA_ALPHA);

                // Show Tooltip
                ShowTooltip(cursorPos);
                NativeMethods.Beep(BEEP_FREQUENCY_ACTIVATE, BEEP_DURATION_MS);
            }
            catch
            {
                // Fail silently or log
                RestoreWindow();
            }
        }

        private void RestoreWindow()
        {
            if (_targetHwnd != IntPtr.Zero && _hasOriginalExStyle)
            {
                // Проверка валидности окна перед манипуляциями
                if (!NativeMethods.IsWindow(_targetHwnd))
                {
                    // Окно уже закрыто, просто сбрасываем состояние
                    _targetHwnd = IntPtr.Zero;
                    _hasOriginalExStyle = false;
                    return;
                }

                try
                {
                    NativeMethods.SetWindowLongPtr(_targetHwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(_originalExStyle));
                    
                    // Восстановление прозрачности
                    if ((_originalExStyle & NativeMethods.WS_EX_LAYERED) != 0)
                    {
                        NativeMethods.SetLayeredWindowAttributes(_targetHwnd, 0, FULL_OPACITY, NativeMethods.LWA_ALPHA);
                    }
                }
                catch (Exception ex)
                {
                    // Логирование ошибки в Debug output
                    System.Diagnostics.Debug.WriteLine("RestoreWindow error: " + ex.Message);
                }
                finally
                {
                    _targetHwnd = IntPtr.Zero;
                    _hasOriginalExStyle = false;
                }
            }
        }

        private void ShowTooltip(Point location)
        {
            _tooltipForm.Location = new Point(location.X + TOOLTIP_OFFSET_X, location.Y + TOOLTIP_OFFSET_Y);
            _tooltipForm.Show();
        }

        private void HideTooltip()
        {
             _tooltipForm.Hide();
        }

        private void SendLWinClick()
        {
            // Simulate LWin Down and Up
            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[2];

            inputs[0].type = NativeMethods.INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = NativeMethods.VK_LWIN;
            inputs[0].U.ki.dwFlags = 0; // KeyDown

            inputs[1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = NativeMethods.VK_LWIN;
            inputs[1].U.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

            NativeMethods.SendInput(2, inputs, NativeMethods.INPUT.Size);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Освобождаем управляемые ресурсы
                lock (_lockObject)
                {
                    if (_timer != null)
                    {
                        _timer.Stop();
                        _timer.Dispose();
                        _timer = null;
                    }
                    
                    if (_tooltipForm != null)
                    {
                        _tooltipForm.Dispose();
                        _tooltipForm = null;
                    }
                }
            }

            // Восстанавливаем окно (управляемое и неуправляемое)
            RestoreWindow();
            
            _disposed = true;
        }

        ~GhostLogic()
        {
            Dispose(false);
        }
    }
}
