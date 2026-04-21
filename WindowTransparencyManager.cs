using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GhostThrough.Models;

namespace GhostThrough
{
    /// <summary>
    /// Applies and restores window transparency via Win32 API
    /// </summary>
    internal class WindowTransparencyManager : IDisposable
    {
        private readonly List<GhostWindowState> _ghostWindows = new List<GhostWindowState>();
        private readonly object _lockObject = new object();
        private const byte FULL_OPACITY = 255;

        // Ignored system window classes
        private static readonly HashSet<string> IgnoredWindowClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Progman", "WorkerW", "Shell_TrayWnd"
        };

        public IReadOnlyList<GhostWindowState> GhostWindows
        {
            get
            {
                lock (_lockObject)
                    return _ghostWindows.ToList().AsReadOnly();
            }
        }

        public bool IsWindowValid(IntPtr hwnd)
        {
            return hwnd != IntPtr.Zero && NativeMethods.IsWindow(hwnd);
        }

        public bool IsIgnoredWindowClass(IntPtr hwnd)
        {
            var className = new StringBuilder(256);
            if (NativeMethods.GetClassName(hwnd, className, className.Capacity) > 0)
            {
                return IgnoredWindowClasses.Contains(className.ToString());
            }
            return false;
        }

        public GhostWindowState ApplyTransparency(IntPtr hwnd, byte opacity)
        {
            if (!IsWindowValid(hwnd))
                throw new ArgumentException("Invalid window handle", "hwnd");

            if (IsIgnoredWindowClass(hwnd))
                throw new InvalidOperationException("Cannot apply transparency to system window");

            try
            {
                // Store original state
                int originalExStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt32();
                bool wasAlreadyLayered = (originalExStyle & NativeMethods.WS_EX_LAYERED) != 0;

                // Apply transparency
                int newStyle = originalExStyle | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT;
                NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(newStyle));
                NativeMethods.SetLayeredWindowAttributes(hwnd, 0, opacity, NativeMethods.LWA_ALPHA);

                var state = new GhostWindowState
                {
                    Hwnd = hwnd,
                    OriginalExStyle = originalExStyle,
                    WasAlreadyLayered = wasAlreadyLayered
                };

                lock (_lockObject)
                {
                    _ghostWindows.Add(state);
                }

                DebugLogger.Log(string.Format("WindowTransparencyManager: Applied {0} opacity to window {1}", opacity, hwnd));
                return state;
            }
            catch (Exception ex)
            {
                DebugLogger.Log(string.Format("WindowTransparencyManager.Apply ERROR: {0}", ex.Message));
                throw;
            }
        }

        public void RestoreWindow(IntPtr hwnd)
        {
            lock (_lockObject)
            {
                var windowState = _ghostWindows.FirstOrDefault(w => w.Hwnd == hwnd);
                if (windowState != null)
                {
                    RestoreSingleWindow(windowState);
                    _ghostWindows.Remove(windowState);
                }
            }
        }

        public void RestoreAllWindows()
        {
            lock (_lockObject)
            {
                DebugLogger.Log(string.Format("WindowTransparencyManager: Restoring {0} windows", _ghostWindows.Count));
                foreach (var windowState in _ghostWindows.ToList())
                {
                    RestoreSingleWindow(windowState);
                }
                _ghostWindows.Clear();
            }
        }

        private void RestoreSingleWindow(GhostWindowState windowState)
        {
            if (windowState.Hwnd == IntPtr.Zero)
                return;

            if (!NativeMethods.IsWindow(windowState.Hwnd))
            {
                DebugLogger.Log(string.Format("WindowTransparencyManager: Window {0} is no longer valid", windowState.Hwnd));
                return;
            }

            try
            {
                NativeMethods.SetWindowLongPtr(windowState.Hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(windowState.OriginalExStyle));

                if (windowState.WasAlreadyLayered)
                {
                    NativeMethods.SetLayeredWindowAttributes(windowState.Hwnd, 0, FULL_OPACITY, NativeMethods.LWA_ALPHA);
                }

                DebugLogger.Log(string.Format("WindowTransparencyManager: Restored window {0}", windowState.Hwnd));
            }
            catch (Exception ex)
            {
                DebugLogger.Log(string.Format("WindowTransparencyManager.Restore ERROR: {0}", ex.Message));
            }
        }

        public void Dispose()
        {
            RestoreAllWindows();
        }
    }
}
