using System;
using System.IO;
using System.Windows.Forms;
using GhostThrough.Models;

namespace GhostThrough
{
    internal static class Program
    {
        private static KeyboardHook _keyboardHook;
        private static MouseHook _mouseHook;
        private static GhostController _controller;
        private static SettingsManager _settingsManager;
        private static Settings _settings;
        private static AppContext _appContext;

        private const string AppDataFolderName = "GhostThrough";
        private const string LegacyAppDataFolderName = "PeekThrough";
        private const string SettingsFileName = "settings.json";
        private const string SingleInstanceMutexName = "GhostThroughApp";

        [STAThread]
        static void Main()
        {
            // Ensure single instance
            using (var mutex = new System.Threading.Mutex(false, SingleInstanceMutexName))
            {
                if (!mutex.WaitOne(0, false))
                {
                    return; // Already running
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                _appContext = AppContext.Create(ResolveSettingsPath());
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

        private static string ResolveSettingsPath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string currentSettingsPath = Path.Combine(appDataPath, AppDataFolderName, SettingsFileName);
            string legacySettingsPath = Path.Combine(appDataPath, LegacyAppDataFolderName, SettingsFileName);

            if (File.Exists(currentSettingsPath) || !File.Exists(legacySettingsPath))
                return currentSettingsPath;

            try
            {
                string currentSettingsDir = Path.GetDirectoryName(currentSettingsPath);
                if (!Directory.Exists(currentSettingsDir))
                    Directory.CreateDirectory(currentSettingsDir);

                File.Copy(legacySettingsPath, currentSettingsPath, false);
                DebugLogger.LogInfo(string.Format("Program: Migrated settings from {0} to {1}", legacySettingsPath, currentSettingsPath));
            }
            catch (Exception ex)
            {
                DebugLogger.LogInfo(string.Format("Program: Settings migration skipped - {0}", ex.Message));
            }

            return currentSettingsPath;
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
