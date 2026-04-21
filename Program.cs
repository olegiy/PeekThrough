using System;
using System.IO;
using System.Windows.Forms;
using PeekThrough.Models;

namespace PeekThrough
{
    internal static class Program
    {
        private static KeyboardHook _keyboardHook;
        private static MouseHook _mouseHook;
        private static GhostController _controller;
        private static SettingsManager _settingsManager;
        private static Settings _settings;
        private static AppContext _appContext;

        // Paths
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PeekThrough",
            "settings.json");

        [STAThread]
        static void Main()
        {
            // Ensure single instance
            using (var mutex = new System.Threading.Mutex(false, "PeekThroughGhostModeApp"))
            {
                if (!mutex.WaitOne(0, false))
                {
                    return; // Already running
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                _appContext = AppContext.Create(SettingsPath);
                _controller = _appContext.Controller;
                _keyboardHook = _appContext.KeyboardHook;
                _mouseHook = _appContext.MouseHook;
                _settingsManager = _appContext.SettingsManager;
                _settings = _appContext.Settings;

                // Subscribe to events
                SubscribeHookEvents();

                using (var trayMenu = new TrayMenuController(_appContext))
                {
                    Application.Run();
                }

                if (_appContext != null)
                    _appContext.Shutdown();
            }
        }

        private static void SubscribeHookEvents()
        {
            _keyboardHook.OnActivationKeyDown += _controller.OnKeyDown;
            _keyboardHook.OnActivationKeyUp += _controller.OnKeyUp;
            _keyboardHook.OnOtherKeyPressedBeforeActivation += _controller.BlockGhostMode;

            _mouseHook.OnSelectedMouseDown += _controller.OnMouseButtonDown;
            _mouseHook.OnSelectedMouseUp += _controller.OnMouseButtonUp;
            _mouseHook.OnOtherMouseButtonPressedBeforeSelected += _controller.BlockGhostMode;
        }

    }
}
