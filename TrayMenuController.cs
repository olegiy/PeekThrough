using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace GhostThrough
{
    internal sealed class TrayMenuController : IDisposable
    {
        private readonly AppContext _appContext;
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _activationKeyMenuItem;
        private readonly ToolStripMenuItem _activationMethodMenuItem;
        private readonly ToolStripMenuItem _activationDelayMenuItem;
        private readonly ToolStripMenuItem _activationModeMenuItem;
        private readonly ToolStripMenuItem _loggingLevelMenuItem;
        private bool _disposed;

        public TrayMenuController(AppContext appContext)
        {
            _appContext = appContext;
            _trayIcon = new NotifyIcon();
            _trayIcon.Text = BuildTrayTooltip(Assembly.GetExecutingAssembly());

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "icons", "icon.ico");
            System.Drawing.Icon trayIcon = null;
            if (File.Exists(iconPath))
                trayIcon = new System.Drawing.Icon(iconPath);

            if (trayIcon == null)
                trayIcon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            _trayIcon.Icon = trayIcon ?? System.Drawing.SystemIcons.Application;

            _activationKeyMenuItem = BuildActivationKeyMenuItem();
            _activationMethodMenuItem = BuildActivationMethodMenuItem();
            _activationDelayMenuItem = BuildActivationDelayMenuItem();
            _activationModeMenuItem = BuildActivationModeMenuItem();
            _loggingLevelMenuItem = BuildLoggingLevelMenuItem();
            _menu = BuildMenu();

            _trayIcon.ContextMenuStrip = _menu;
            _trayIcon.Visible = true;
        }

        internal static string BuildTrayTooltip(Assembly assembly)
        {
            const int notifyIconTextLimit = 63;
            const string fallbackName = "GhostThrough";

            try
            {
                AssemblyName assemblyName = assembly.GetName();
                string productName = string.IsNullOrWhiteSpace(assemblyName.Name)
                    ? fallbackName
                    : assemblyName.Name;
                string version = FormatProductVersion(assemblyName.Version);
                string timestamp = NormalizeInformationalVersion(
                    assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

                string tooltip;
                if (!string.IsNullOrWhiteSpace(version) && !string.IsNullOrWhiteSpace(timestamp))
                    tooltip = string.Format("{0} {1} ({2})", productName, version, timestamp);
                else if (!string.IsNullOrWhiteSpace(version))
                    tooltip = string.Format("{0} {1}", productName, version);
                else
                    tooltip = productName;

                return tooltip.Length <= notifyIconTextLimit
                    ? tooltip
                    : tooltip.Substring(0, notifyIconTextLimit);
            }
            catch
            {
                return fallbackName;
            }
        }

        private static string FormatProductVersion(Version version)
        {
            if (version == null)
                return null;

            return string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build < 0 ? 0 : version.Build);
        }

        private static string NormalizeInformationalVersion(string informationalVersion)
        {
            if (string.IsNullOrWhiteSpace(informationalVersion))
                return null;

            int metadataIndex = informationalVersion.IndexOf('+');
            if (metadataIndex >= 0)
                informationalVersion = informationalVersion.Substring(0, metadataIndex);

            return informationalVersion.Trim();
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Opening += OnMenuOpening;
            menu.Closed += OnMenuClosed;
            menu.Items.Add(_activationKeyMenuItem);
            menu.Items.Add(_activationMethodMenuItem);
            menu.Items.Add(_activationModeMenuItem);
            menu.Items.Add(_activationDelayMenuItem);
            menu.Items.Add(_loggingLevelMenuItem);
            menu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) =>
            {
                if (_disposed)
                    return;

                // Delay shutdown until the current menu message unwinds.
                _menu.BeginInvoke((Action)Application.Exit);
            };

            menu.Items.Add(exitItem);
            return menu;
        }

        private void OnMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_disposed)
                return;

            _appContext.Controller.OnOtherInputBeforeActivation();
            _appContext.MouseHook.SuppressSelectedMouseButtonFor(500);
        }

        private void OnMenuClosed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            if (_disposed)
                return;

            _appContext.Controller.OnOtherInputBeforeActivation();
            _appContext.MouseHook.SuppressSelectedMouseButtonFor(500);
        }

        private ToolStripMenuItem BuildActivationMethodMenuItem()
        {
            var item = new ToolStripMenuItem("Activation Method");
            item.DropDownOpening += (s, e) => RefreshActivationMethodMenu(item);

            item.DropDownItems.Add(new ToolStripMenuItem("Keyboard", null, OnActivationMethodKeyboardClick));
            item.DropDownItems.Add(new ToolStripMenuItem("Mouse (Middle Button)", null, OnActivationMethodMouseMiddleClick));
            item.DropDownItems.Add(new ToolStripMenuItem("Mouse (Right Button)", null, OnActivationMethodMouseRightClick));
            item.DropDownItems.Add(new ToolStripMenuItem("Mouse (X1 Button)", null, OnActivationMethodMouseX1Click));
            item.DropDownItems.Add(new ToolStripMenuItem("Mouse (X2 Button)", null, OnActivationMethodMouseX2Click));

            return item;
        }

        private ToolStripMenuItem BuildLoggingLevelMenuItem()
        {
            var item = new ToolStripMenuItem("Logging Level");
            item.DropDownOpening += (s, e) => RefreshLoggingLevelMenu(item);

            var infoItem = new ToolStripMenuItem("Info", null, OnLoggingLevelInfoClick);
            infoItem.Tag = DebugLogger.LEVEL_INFO;
            item.DropDownItems.Add(infoItem);

            var debugItem = new ToolStripMenuItem("Debug", null, OnLoggingLevelDebugClick);
            debugItem.Tag = DebugLogger.LEVEL_DEBUG;
            item.DropDownItems.Add(debugItem);

            return item;
        }

        private ToolStripMenuItem BuildActivationKeyMenuItem()
        {
            var item = new ToolStripMenuItem("Activation Key");
            item.DropDownOpening += (s, e) => RefreshActivationKeyMenu(item);

            foreach (int vkCode in ActivationKeyCatalog.AvailableKeys)
            {
                int key = vkCode;
                var keyItem = new ToolStripMenuItem(ActivationKeyCatalog.GetDisplayName(vkCode));
                keyItem.Tag = key;
                keyItem.Click += OnActivationKeyClick;
                item.DropDownItems.Add(keyItem);
            }

            return item;
        }

        private ToolStripMenuItem BuildActivationDelayMenuItem()
        {
            var item = new ToolStripMenuItem("Activation Hold Time");
            item.DropDownOpening += (s, e) => RefreshActivationDelayMenu(item);

            for (int delayMs = ActivationStateManager.MIN_ACTIVATION_DELAY_MS;
                 delayMs <= ActivationStateManager.MAX_ACTIVATION_DELAY_MS;
                 delayMs += ActivationStateManager.ACTIVATION_DELAY_STEP_MS)
            {
                int selectedDelay = delayMs;
                string text = string.Format("{0:0.0} s", selectedDelay / 1000.0);
                var delayItem = new ToolStripMenuItem(text);
                delayItem.Tag = selectedDelay;
                delayItem.Click += OnActivationDelayClick;
                item.DropDownItems.Add(delayItem);
            }

            return item;
        }

        private ToolStripMenuItem BuildActivationModeMenuItem()
        {
            var item = new ToolStripMenuItem("Activation Mode");
            item.DropDownOpening += (s, e) => RefreshActivationModeMenu(item);

            var holdItem = new ToolStripMenuItem("Hold", null, OnActivationModeHoldClick);
            holdItem.Tag = ActivationMode.Hold;
            item.DropDownItems.Add(holdItem);

            var clickItem = new ToolStripMenuItem("Click", null, OnActivationModeClickClick);
            clickItem.Tag = ActivationMode.Click;
            item.DropDownItems.Add(clickItem);

            return item;
        }

        private void RefreshActivationMethodMenu(ToolStripMenuItem item)
        {
            var activationType = _appContext.Settings.Activation.Type.ToActivationInputType();
            int selectedMouseButton = _appContext.Settings.Activation.MouseButton;

            for (int i = 0; i < item.DropDownItems.Count; i++)
            {
                var child = item.DropDownItems[i] as ToolStripMenuItem;
                if (child == null)
                    continue;

                child.Checked = false;
            }

            if (activationType == ActivationInputType.Keyboard)
            {
                ((ToolStripMenuItem)item.DropDownItems[0]).Checked = true;
                return;
            }

            switch (selectedMouseButton)
            {
                case NativeMethods.VK_MBUTTON:
                    ((ToolStripMenuItem)item.DropDownItems[1]).Checked = true;
                    break;
                case NativeMethods.VK_RBUTTON:
                    ((ToolStripMenuItem)item.DropDownItems[2]).Checked = true;
                    break;
                case NativeMethods.VK_XBUTTON1:
                    ((ToolStripMenuItem)item.DropDownItems[3]).Checked = true;
                    break;
                case NativeMethods.VK_XBUTTON2:
                    ((ToolStripMenuItem)item.DropDownItems[4]).Checked = true;
                    break;
            }
        }

        private void RefreshActivationKeyMenu(ToolStripMenuItem item)
        {
            int selectedKey = _appContext.Settings.Activation.KeyCode;

            foreach (ToolStripItem dropDownItem in item.DropDownItems)
            {
                var keyItem = dropDownItem as ToolStripMenuItem;
                if (keyItem == null)
                    continue;

                int key = (int)keyItem.Tag;
                keyItem.Checked = key == selectedKey;
            }
        }

        private void RefreshActivationDelayMenu(ToolStripMenuItem item)
        {
            int selectedDelayMs = _appContext.Settings.Activation.ActivationDelayMs;

            foreach (ToolStripItem dropDownItem in item.DropDownItems)
            {
                var delayItem = dropDownItem as ToolStripMenuItem;
                if (delayItem == null)
                    continue;

                int delayMs = (int)delayItem.Tag;
                delayItem.Checked = delayMs == selectedDelayMs;
            }
        }

        private void RefreshActivationModeMenu(ToolStripMenuItem item)
        {
            var selectedMode = _appContext.Settings.Activation.Mode.ToActivationMode();

            foreach (ToolStripItem dropDownItem in item.DropDownItems)
            {
                var modeItem = dropDownItem as ToolStripMenuItem;
                if (modeItem == null || !(modeItem.Tag is ActivationMode))
                    continue;

                modeItem.Checked = (ActivationMode)modeItem.Tag == selectedMode;
            }
        }

        private void RefreshLoggingLevelMenu(ToolStripMenuItem item)
        {
            string selectedLevel = DebugLogger.NormalizeLogLevel(_appContext.Settings.Logging.Level);

            foreach (ToolStripItem dropDownItem in item.DropDownItems)
            {
                var levelItem = dropDownItem as ToolStripMenuItem;
                if (levelItem == null || !(levelItem.Tag is string))
                    continue;

                string level = (string)levelItem.Tag;
                levelItem.Checked = string.Equals(level, selectedLevel, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void OnActivationKeyClick(object sender, EventArgs e)
        {
            if (_disposed)
                return;

            var item = sender as ToolStripMenuItem;
            if (item == null || !(item.Tag is int))
                return;

            int key = (int)item.Tag;
            _appContext.Reconfigure(
                _appContext.Settings.Activation.Type.ToActivationInputType(),
                key,
                _appContext.Settings.Activation.MouseButton);
        }

        private void OnActivationMethodKeyboardClick(object sender, EventArgs e)
        {
            ReconfigureActivationMethod(ActivationInputType.Keyboard, NativeMethods.VK_MBUTTON);
        }

        private void OnActivationMethodMouseMiddleClick(object sender, EventArgs e)
        {
            ReconfigureActivationMethod(ActivationInputType.Mouse, NativeMethods.VK_MBUTTON);
        }

        private void OnActivationMethodMouseRightClick(object sender, EventArgs e)
        {
            ReconfigureActivationMethod(ActivationInputType.Mouse, NativeMethods.VK_RBUTTON);
        }

        private void OnActivationMethodMouseX1Click(object sender, EventArgs e)
        {
            ReconfigureActivationMethod(ActivationInputType.Mouse, NativeMethods.VK_XBUTTON1);
        }

        private void OnActivationMethodMouseX2Click(object sender, EventArgs e)
        {
            ReconfigureActivationMethod(ActivationInputType.Mouse, NativeMethods.VK_XBUTTON2);
        }

        private void OnActivationDelayClick(object sender, EventArgs e)
        {
            if (_disposed)
                return;

            var item = sender as ToolStripMenuItem;
            if (item == null || !(item.Tag is int))
                return;

            int activationDelayMs = (int)item.Tag;
            _appContext.ReconfigureActivationDelay(activationDelayMs);
        }

        private void OnActivationModeHoldClick(object sender, EventArgs e)
        {
            ReconfigureActivationMode(ActivationMode.Hold);
        }

        private void OnActivationModeClickClick(object sender, EventArgs e)
        {
            ReconfigureActivationMode(ActivationMode.Click);
        }

        private void OnLoggingLevelInfoClick(object sender, EventArgs e)
        {
            ReconfigureLogLevel(DebugLogger.LEVEL_INFO);
        }

        private void OnLoggingLevelDebugClick(object sender, EventArgs e)
        {
            ReconfigureLogLevel(DebugLogger.LEVEL_DEBUG);
        }

        private void ReconfigureActivationMethod(ActivationInputType activationType, int mouseButton)
        {
            if (_disposed)
                return;

            _appContext.Reconfigure(activationType, _appContext.Settings.Activation.KeyCode, mouseButton);
        }

        private void ReconfigureActivationMode(ActivationMode activationMode)
        {
            if (_disposed)
                return;

            _appContext.ReconfigureActivationMode(activationMode);
        }

        private void ReconfigureLogLevel(string logLevel)
        {
            if (_disposed)
                return;

            _appContext.ReconfigureLogLevel(logLevel);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_menu.Visible)
                _menu.Close();

            _trayIcon.Visible = false;
            _trayIcon.ContextMenuStrip = null;
            _menu.Dispose();
            _trayIcon.Dispose();
        }
    }
}
