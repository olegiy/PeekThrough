using System;
using System.IO;
using System.Windows.Forms;

namespace PeekThrough
{
    internal sealed class TrayMenuController : IDisposable
    {
        private readonly AppContext _appContext;
        private readonly NotifyIcon _trayIcon;

        public TrayMenuController(AppContext appContext)
        {
            _appContext = appContext;
            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "PeekThrough Ghost Mode";

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "icons", "icon.ico");
            if (File.Exists(iconPath))
                _trayIcon.Icon = new System.Drawing.Icon(iconPath);
            else
                _trayIcon.Icon = System.Drawing.SystemIcons.Application;

            _trayIcon.ContextMenu = BuildMenu();
            _trayIcon.Visible = true;
        }

        private ContextMenu BuildMenu()
        {
            var menu = new ContextMenu();
            menu.MenuItems.Add("Activation Key", (s, e) => ShowKeySelectionMenu());
            menu.MenuItems.Add("Activation Method", (s, e) => ShowActivationSettings());
            menu.MenuItems.Add("-");
            menu.MenuItems.Add("Exit", (s, e) => Application.Exit());
            return menu;
        }

        private void ShowActivationSettings()
        {
            var menu = new ContextMenuStrip();
            var activationType = _appContext.Settings.Activation.Type.ToActivationInputType();

            var keyboardItem = new ToolStripMenuItem("Keyboard");
            var mouseMiddleItem = new ToolStripMenuItem("Mouse (Middle Button)");
            var mouseRightItem = new ToolStripMenuItem("Mouse (Right Button)");
            var mouseX1Item = new ToolStripMenuItem("Mouse (X1 Button)");
            var mouseX2Item = new ToolStripMenuItem("Mouse (X2 Button)");

            if (activationType == ActivationInputType.Keyboard)
            {
                keyboardItem.Checked = true;
            }
            else
            {
                switch (_appContext.Settings.Activation.MouseButton)
                {
                    case NativeMethods.VK_MBUTTON: mouseMiddleItem.Checked = true; break;
                    case NativeMethods.VK_RBUTTON: mouseRightItem.Checked = true; break;
                    case NativeMethods.VK_XBUTTON1: mouseX1Item.Checked = true; break;
                    case NativeMethods.VK_XBUTTON2: mouseX2Item.Checked = true; break;
                }
            }

            keyboardItem.Click += (s, e) => _appContext.Reconfigure(ActivationInputType.Keyboard, _appContext.Settings.Activation.KeyCode, NativeMethods.VK_MBUTTON);
            mouseMiddleItem.Click += (s, e) => _appContext.Reconfigure(ActivationInputType.Mouse, _appContext.Settings.Activation.KeyCode, NativeMethods.VK_MBUTTON);
            mouseRightItem.Click += (s, e) => _appContext.Reconfigure(ActivationInputType.Mouse, _appContext.Settings.Activation.KeyCode, NativeMethods.VK_RBUTTON);
            mouseX1Item.Click += (s, e) => _appContext.Reconfigure(ActivationInputType.Mouse, _appContext.Settings.Activation.KeyCode, NativeMethods.VK_XBUTTON1);
            mouseX2Item.Click += (s, e) => _appContext.Reconfigure(ActivationInputType.Mouse, _appContext.Settings.Activation.KeyCode, NativeMethods.VK_XBUTTON2);

            menu.Items.Add(keyboardItem);
            menu.Items.Add(mouseMiddleItem);
            menu.Items.Add(mouseRightItem);
            menu.Items.Add(mouseX1Item);
            menu.Items.Add(mouseX2Item);

            menu.ItemClicked += (s, e) => menu.Close();
            menu.Show(Cursor.Position);
        }

        private void ShowKeySelectionMenu()
        {
            var menu = new ContextMenuStrip();

            foreach (int vkCode in ActivationKeyCatalog.AvailableKeys)
            {
                int key = vkCode;
                var item = new ToolStripMenuItem(ActivationKeyCatalog.GetDisplayName(vkCode));
                item.Checked = key == _appContext.Settings.Activation.KeyCode;
                item.Click += (s, e) => _appContext.Reconfigure(
                    _appContext.Settings.Activation.Type.ToActivationInputType(),
                    key,
                    _appContext.Settings.Activation.MouseButton);
                menu.Items.Add(item);
            }

            menu.ItemClicked += (s, e) => menu.Close();
            menu.Show(Cursor.Position);
        }

        public void Dispose()
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
    }
}
