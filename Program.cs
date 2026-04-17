using System;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Linq;
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

        // Paths
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PeekThrough",
            "settings.json");

        // Key display names (unchanged)
        private static readonly Dictionary<int, string> KeyDisplayNames = new Dictionary<int, string>
        {
            { NativeMethods.VK_LWIN, "Left Win" },
            { NativeMethods.VK_RWIN, "Right Win" },
            { NativeMethods.VK_LCONTROL, "Left Ctrl" },
            { NativeMethods.VK_RCONTROL, "Right Ctrl" },
            { NativeMethods.VK_LMENU, "Left Alt" },
            { NativeMethods.VK_RMENU, "Right Alt" },
            { NativeMethods.VK_LSHIFT, "Left Shift" },
            { NativeMethods.VK_RSHIFT, "Right Shift" },
            { NativeMethods.VK_CAPITAL, "Caps Lock" },
            { NativeMethods.VK_TAB, "Tab" },
            { NativeMethods.VK_SPACE, "Space" },
            { NativeMethods.VK_ESCAPE, "Escape" },
            { NativeMethods.VK_OEM_3, "Tilde (`~)" },
            { NativeMethods.VK_INSERT, "Insert" },
            { NativeMethods.VK_DELETE, "Delete" },
            { NativeMethods.VK_HOME, "Home" },
            { NativeMethods.VK_END, "End" },
            { NativeMethods.VK_PRIOR, "Page Up" },
            { NativeMethods.VK_NEXT, "Page Down" },
            { 0x30, "0" }, { 0x31, "1" }, { 0x32, "2" }, { 0x33, "3" }, { 0x34, "4" },
            { 0x35, "5" }, { 0x36, "6" }, { 0x37, "7" }, { 0x38, "8" }, { 0x39, "9" },
            { 0x70, "F1" }, { 0x71, "F2" }, { 0x72, "F3" }, { 0x73, "F4" },
            { 0x74, "F5" }, { 0x75, "F6" }, { 0x76, "F7" }, { 0x77, "F8" },
            { 0x78, "F9" }, { 0x79, "F10" }, { 0x7A, "F11" }, { 0x7B, "F12" },
        };

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

                // Initialize settings
                _settingsManager = new SettingsManager(SettingsPath);
                _settings = _settingsManager.LoadSettings();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Create profile manager from settings
                var profiles = new List<Profile>();
                foreach (var p in _settings.Profiles.List)
                {
                    profiles.Add(new Profile(p.Id, p.Name, p.Opacity));
                }
                var profileManager = new ProfileManager(_settings.Profiles.ActiveId, profiles);

                // Create GhostController
                var activationType = _settings.Activation.Type == "mouse"
                    ? ActivationInputType.Mouse
                    : ActivationInputType.Keyboard;

                _controller = new GhostController(activationType, profileManager);
                _controller.ActivationKeyCode = _settings.Activation.KeyCode;

                // Create hooks
                _keyboardHook = new KeyboardHook(_controller);
                _mouseHook = new MouseHook(_controller, _settings.Activation.MouseButton);

                // Subscribe to events
                SubscribeHookEvents();

                // Setup Tray Icon
                using (var trayIcon = new NotifyIcon())
                {
                    trayIcon.Text = "PeekThrough Ghost Mode";

                    string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "icons", "icon.ico");
                    if (File.Exists(iconPath))
                    {
                        trayIcon.Icon = new System.Drawing.Icon(iconPath);
                    }
                    else
                    {
                        trayIcon.Icon = System.Drawing.SystemIcons.Application;
                    }

                    var contextMenu = new ContextMenu();
                    contextMenu.MenuItems.Add("Activation Key", (s, e) => ShowKeySelectionMenu());
                    contextMenu.MenuItems.Add("Activation Method", (s, e) => ShowActivationSettings());
                    contextMenu.MenuItems.Add("-");
                    contextMenu.MenuItems.Add("Exit", (s, e) => Application.Exit());
                    trayIcon.ContextMenu = contextMenu;
                    trayIcon.Visible = true;

                    Application.Run();

                    trayIcon.Visible = false;
                }

                // Cleanup
                if (_keyboardHook != null)
                    _keyboardHook.Dispose();
                if (_mouseHook != null)
                    _mouseHook.Dispose();
                if (_controller != null)
                    _controller.Dispose();

                // Save settings (preserve active profile)
                _settings.Profiles.ActiveId = profileManager.ActiveProfile.Id;
                _settingsManager.SaveSettings(_settings);
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

        private static void UnsubscribeHookEvents()
        {
            _keyboardHook.OnActivationKeyDown -= _controller.OnKeyDown;
            _keyboardHook.OnActivationKeyUp -= _controller.OnKeyUp;
            _keyboardHook.OnOtherKeyPressedBeforeActivation -= _controller.BlockGhostMode;

            _mouseHook.OnSelectedMouseDown -= _controller.OnMouseButtonDown;
            _mouseHook.OnSelectedMouseUp -= _controller.OnMouseButtonUp;
            _mouseHook.OnOtherMouseButtonPressedBeforeSelected -= _controller.BlockGhostMode;
        }

        private static void SwitchActivation(ActivationInputType type, int mouseButton)
        {
            if (_controller.IsGhostModeActive)
                _controller.DeactivateGhostMode();

            UnsubscribeHookEvents();
            _controller.Dispose();

            // Create new controller with new activation type
            var profileManager = CreateProfileManagerFromSettings();
            _controller = new GhostController(type, profileManager);
            _controller.ActivationKeyCode = _settings.Activation.KeyCode;

            // Recreate hooks
            if (_keyboardHook != null)
                _keyboardHook.Dispose();
            if (_mouseHook != null)
                _mouseHook.Dispose();
            _keyboardHook = new KeyboardHook(_controller);
            _mouseHook = new MouseHook(_controller, mouseButton);

            SubscribeHookEvents();

            // Update settings
            _settings.Activation.Type = (type == ActivationInputType.Mouse) ? "mouse" : "keyboard";
            _settings.Activation.MouseButton = mouseButton;
            _settingsManager.SaveSettings(_settings);
        }

        private static void SetActivationKey(int vkCode)
        {
            DebugLogger.Log(string.Format("SetActivationKey: {0} (0x{1:X2})", vkCode, vkCode));

            if (_controller.IsGhostModeActive)
                _controller.DeactivateGhostMode();

            UnsubscribeHookEvents();
            _controller.Dispose();

            var profileManager = CreateProfileManagerFromSettings();
            var activationType = _settings.Activation.Type == "mouse"
                ? ActivationInputType.Mouse
                : ActivationInputType.Keyboard;

            _controller = new GhostController(activationType, profileManager);
            _controller.ActivationKeyCode = vkCode;

            if (_keyboardHook != null)
                _keyboardHook.Dispose();
            _keyboardHook = new KeyboardHook(_controller);

            SubscribeHookEvents();

            _settings.Activation.KeyCode = vkCode;
            _settingsManager.SaveSettings(_settings);
        }

        private static void ShowActivationSettings()
        {
            var menu = new ContextMenuStrip();

            var keyboardItem = new ToolStripMenuItem("Keyboard");
            var mouseMiddleItem = new ToolStripMenuItem("Mouse (Middle Button)");
            var mouseRightItem = new ToolStripMenuItem("Mouse (Right Button)");
            var mouseX1Item = new ToolStripMenuItem("Mouse (X1 Button)");
            var mouseX2Item = new ToolStripMenuItem("Mouse (X2 Button)");

            // Set checked state
            if (_settings.Activation.Type == "keyboard")
            {
                keyboardItem.Checked = true;
            }
            else
            {
                switch (_settings.Activation.MouseButton)
                {
                    case NativeMethods.VK_MBUTTON: mouseMiddleItem.Checked = true; break;
                    case NativeMethods.VK_RBUTTON: mouseRightItem.Checked = true; break;
                    case NativeMethods.VK_XBUTTON1: mouseX1Item.Checked = true; break;
                    case NativeMethods.VK_XBUTTON2: mouseX2Item.Checked = true; break;
                }
            }

            keyboardItem.Click += (s, e) => SwitchActivation(ActivationInputType.Keyboard, NativeMethods.VK_MBUTTON);
            mouseMiddleItem.Click += (s, e) => SwitchActivation(ActivationInputType.Mouse, NativeMethods.VK_MBUTTON);
            mouseRightItem.Click += (s, e) => SwitchActivation(ActivationInputType.Mouse, NativeMethods.VK_RBUTTON);
            mouseX1Item.Click += (s, e) => SwitchActivation(ActivationInputType.Mouse, NativeMethods.VK_XBUTTON1);
            mouseX2Item.Click += (s, e) => SwitchActivation(ActivationInputType.Mouse, NativeMethods.VK_XBUTTON2);

            menu.Items.Add(keyboardItem);
            menu.Items.Add(mouseMiddleItem);
            menu.Items.Add(mouseRightItem);
            menu.Items.Add(mouseX1Item);
            menu.Items.Add(mouseX2Item);

            menu.ItemClicked += (s, e) => menu.Close();
            menu.Show(Cursor.Position);
        }

        private static void ShowKeySelectionMenu()
        {
            var menu = new ContextMenuStrip();

            int[] availableKeys = new int[]
            {
                NativeMethods.VK_LWIN, NativeMethods.VK_RWIN,
                NativeMethods.VK_LCONTROL, NativeMethods.VK_RCONTROL,
                NativeMethods.VK_LMENU, NativeMethods.VK_RMENU,
                NativeMethods.VK_LSHIFT, NativeMethods.VK_RSHIFT,
                NativeMethods.VK_CAPITAL, NativeMethods.VK_TAB,
                NativeMethods.VK_SPACE, NativeMethods.VK_ESCAPE,
                NativeMethods.VK_OEM_3,
                NativeMethods.VK_INSERT, NativeMethods.VK_DELETE,
                NativeMethods.VK_HOME, NativeMethods.VK_END,
                NativeMethods.VK_PRIOR, NativeMethods.VK_NEXT,
                0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39,
                0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B
            };

            foreach (int vkCode in availableKeys)
            {
                var item = new ToolStripMenuItem(GetKeyDisplayName(vkCode));
                if (vkCode == _settings.Activation.KeyCode)
                    item.Checked = true;

                int key = vkCode;
                item.Click += (s, e) => SetActivationKey(key);
                menu.Items.Add(item);
            }

            menu.ItemClicked += (s, e) => menu.Close();
            menu.Show(Cursor.Position);
        }

        private static string GetKeyDisplayName(int vkCode)
        {
            string name;
            if (KeyDisplayNames.TryGetValue(vkCode, out name))
                return name;
            return string.Format("Key 0x{0:X2}", vkCode);
        }

        private static ProfileManager CreateProfileManagerFromSettings()
        {
            var profiles = _settings.Profiles.List
                .Select(p => new Profile(p.Id, p.Name, p.Opacity))
                .ToList();

            return new ProfileManager(_settings.Profiles.ActiveId, profiles);
        }
    }
}
