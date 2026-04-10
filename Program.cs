using System;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;

namespace PeekThrough
{
    internal static class Program
    {
        private static KeyboardHook _keyboardHook;
        private static MouseHook _mouseHook;
        private static GhostLogic _logic;
        
        // Путь к файлу настроек
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PeekThrough",
            "settings.json");
        
        // Настройки
        private static int _activationKeyCode = NativeMethods.VK_LWIN; // По умолчанию Win
        private static GhostLogic.ActivationType _activationType = GhostLogic.ActivationType.Keyboard;
        private static int _mouseButton = NativeMethods.VK_MBUTTON;
        
        // Карта клавиш для отображения
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
                    // Already running
                    return;
                }

                // Загружаем настройки
                LoadSettings();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Создаем GhostLogic с типом активации из настроек
                _logic = new GhostLogic(_activationType);
                _logic.ActivationKeyCode = _activationKeyCode;
                
                // Инициализируем хуки
                _keyboardHook = new KeyboardHook(_logic);
                _mouseHook = new MouseHook(_logic, _mouseButton);

                // Подписываемся на события
                SubscribeHookEvents();

                // Setup Tray Icon
                using (var trayIcon = new NotifyIcon())
                {
                    trayIcon.Text = "PeekThrough Ghost Mode";
                    
                    // Try to load icon from resources folder
                    string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "icons", "icon.ico");
                    if (File.Exists(iconPath))
                    {
                        trayIcon.Icon = new System.Drawing.Icon(iconPath);
                    }
                    else
                    {
                        // Fallback to generic icon if file not found
                        trayIcon.Icon = System.Drawing.SystemIcons.Application;
                    }

                    var contextMenu = new ContextMenu();
                    // Добавляем пункты для настройки активации
                    contextMenu.MenuItems.Add("Activation Key", (s, e) => ShowKeySelectionMenu());
                    contextMenu.MenuItems.Add("Activation Method", (s, e) => ShowActivationSettings());
                    contextMenu.MenuItems.Add("-");
                    contextMenu.MenuItems.Add("Exit", (s, e) => Application.Exit());
                    trayIcon.ContextMenu = contextMenu;
                    trayIcon.Visible = true;

                    // Create a dummy ApplicationContext to run the loop without a main form visible at start
                    Application.Run();

                    trayIcon.Visible = false;
                }

                _keyboardHook.Dispose();
                _mouseHook.Dispose();
                _logic.Dispose();
            }
        }
        
        // Загрузка настроек из файла
        private static void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string[] lines = File.ReadAllLines(SettingsPath);
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split(':');
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string value = parts[1].Trim();
                            
                            switch (key)
                            {
                                case "ActivationKeyCode":
                                    _activationKeyCode = int.Parse(value);
                                    break;
                                case "ActivationType":
                                    _activationType = (GhostLogic.ActivationType)int.Parse(value);
                                    break;
                                case "MouseButton":
                                    _mouseButton = int.Parse(value);
                                    break;
                            }
                        }
                    }
                    DebugLogger.Log(string.Format("Settings loaded: Key={0}, Type={1}, Mouse={2}", 
                        _activationKeyCode, _activationType, _mouseButton));
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log(string.Format("Error loading settings: {0}", ex.Message));
            }
        }
        
        // Сохранение настроек в файл
        private static void SaveSettings()
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                
                string[] lines = new string[]
                {
                    string.Format("ActivationKeyCode: {0}", _activationKeyCode),
                    string.Format("ActivationType: {0}", (int)_activationType),
                    string.Format("MouseButton: {0}", _mouseButton)
                };
                File.WriteAllLines(SettingsPath, lines);
                DebugLogger.Log("Settings saved");
            }
            catch (Exception ex)
            {
                DebugLogger.Log(string.Format("Error saving settings: {0}", ex.Message));
            }
        }
        
        // Получение отображаемого имени клавиши
        private static string GetKeyDisplayName(int vkCode)
        {
            string name;
            if (KeyDisplayNames.TryGetValue(vkCode, out name))
                return name;
            return string.Format("Key 0x{0:X2}", vkCode);
        }
        
        // Подписка событий хуков на текущий _logic
        private static void SubscribeHookEvents()
        {
            // Подписываемся на события клавиатуры
            _keyboardHook.OnActivationKeyDown += _logic.OnKeyDown;
            _keyboardHook.OnActivationKeyUp += _logic.OnKeyUp;
            _keyboardHook.OnOtherKeyPressedBeforeActivation += _logic.BlockGhostMode;

            // Подписываемся на события мыши
            _mouseHook.OnSelectedMouseDown += _logic.OnMouseButtonDown;
            _mouseHook.OnSelectedMouseUp += _logic.OnMouseButtonUp;
            _mouseHook.OnOtherMouseButtonPressedBeforeSelected += _logic.BlockGhostMode;
        }
        
        // Отписка событий хуков от текущего _logic
        private static void UnsubscribeHookEvents()
        {
            _keyboardHook.OnActivationKeyDown -= _logic.OnKeyDown;
            _keyboardHook.OnActivationKeyUp -= _logic.OnKeyUp;
            _keyboardHook.OnOtherKeyPressedBeforeActivation -= _logic.BlockGhostMode;

            _mouseHook.OnSelectedMouseDown -= _logic.OnMouseButtonDown;
            _mouseHook.OnSelectedMouseUp -= _logic.OnMouseButtonUp;
            _mouseHook.OnOtherMouseButtonPressedBeforeSelected -= _logic.BlockGhostMode;
        }
        
        // Безопасное переключение метода активации
        private static void SwitchActivation(GhostLogic.ActivationType type, int mouseButton = NativeMethods.VK_MBUTTON)
        {
            // Деактивируем Ghost Mode если активен
            if (_logic.IsGhostModeActive)
                _logic.DeactivateGhostMode();
            
            // Отписываемся от старого logic
            UnsubscribeHookEvents();
            
            // Dispose старого logic
            _logic.Dispose();
            
            // Создаём новый logic с нужным типом активации
            _logic = new GhostLogic(type);
            _logic.ActivationKeyCode = _activationKeyCode;
            
            // Переподписываем оба хука на новый logic
            SubscribeHookEvents();
            
            // Устанавливаем кнопку мыши если нужно
            if (type == GhostLogic.ActivationType.Mouse)
                _mouseHook.SetSelectedMouseButton(mouseButton);
            
            // Сохраняем настройки
            _activationType = type;
            _mouseButton = mouseButton;
            SaveSettings();
        }
        
        // Метод для показа настроек активации
        private static void ShowActivationSettings()
        {
            var menu = new ContextMenuStrip();
            
            // Добавляем пункты для выбора типа активации
            var keyboardItem = new ToolStripMenuItem("Keyboard");
            var mouseMiddleItem = new ToolStripMenuItem("Mouse (Middle Button)");
            var mouseRightItem = new ToolStripMenuItem("Mouse (Right Button)");
            var mouseX1Item = new ToolStripMenuItem("Mouse (X1 Button)");
            var mouseX2Item = new ToolStripMenuItem("Mouse (X2 Button)");
            
            // Устанавливаем галочки для текущего типа активации
            switch (_logic.CurrentActivationType)
            {
                case GhostLogic.ActivationType.Keyboard:
                    keyboardItem.Checked = true;
                    break;
                case GhostLogic.ActivationType.Mouse:
                    // Проверяем, какая кнопка мыши используется
                    int selectedMouseButton = _mouseHook.SelectedMouseButton;
                    if (selectedMouseButton == NativeMethods.VK_MBUTTON)
                        mouseMiddleItem.Checked = true;
                    else if (selectedMouseButton == NativeMethods.VK_RBUTTON)
                        mouseRightItem.Checked = true;
                    else if (selectedMouseButton == NativeMethods.VK_XBUTTON1)
                        mouseX1Item.Checked = true;
                    else if (selectedMouseButton == NativeMethods.VK_XBUTTON2)
                        mouseX2Item.Checked = true;
                    break;
            }
            
            // Обработчики для изменения типа активации
            keyboardItem.Click += (s, e) => SwitchActivation(GhostLogic.ActivationType.Keyboard);
            mouseMiddleItem.Click += (s, e) => SwitchActivation(GhostLogic.ActivationType.Mouse, NativeMethods.VK_MBUTTON);
            mouseRightItem.Click += (s, e) => SwitchActivation(GhostLogic.ActivationType.Mouse, NativeMethods.VK_RBUTTON);
            mouseX1Item.Click += (s, e) => SwitchActivation(GhostLogic.ActivationType.Mouse, NativeMethods.VK_XBUTTON1);
            mouseX2Item.Click += (s, e) => SwitchActivation(GhostLogic.ActivationType.Mouse, NativeMethods.VK_XBUTTON2);
            
            menu.Items.Add(keyboardItem);
            menu.Items.Add(mouseMiddleItem);
            menu.Items.Add(mouseRightItem);
            menu.Items.Add(mouseX1Item);
            menu.Items.Add(mouseX2Item);
            
            // Закрываем меню после выбора пункта
            menu.ItemClicked += (s, e) => menu.Close();
            
            // Показываем меню в позиции курсора
            menu.Show(System.Windows.Forms.Cursor.Position);
        }
        
        // Меню для выбора клавиши активации
        private static void ShowKeySelectionMenu()
        {
            var menu = new ContextMenuStrip();
            
            // Доступные клавиши для выбора
            int[] availableKeys = new int[]
            {
                NativeMethods.VK_LWIN, NativeMethods.VK_RWIN,
                NativeMethods.VK_LCONTROL, NativeMethods.VK_RCONTROL,
                NativeMethods.VK_LMENU, NativeMethods.VK_RMENU,
                NativeMethods.VK_LSHIFT, NativeMethods.VK_RSHIFT,
                NativeMethods.VK_CAPITAL, NativeMethods.VK_TAB,
                NativeMethods.VK_SPACE, NativeMethods.VK_ESCAPE,
                NativeMethods.VK_OEM_3, // Tilde (`~)
                NativeMethods.VK_INSERT, NativeMethods.VK_DELETE,
                NativeMethods.VK_HOME, NativeMethods.VK_END,
                NativeMethods.VK_PRIOR, NativeMethods.VK_NEXT,
                0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39,
                0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B
            };
            
            foreach (int vkCode in availableKeys)
            {
                var item = new ToolStripMenuItem(GetKeyDisplayName(vkCode));
                if (vkCode == _activationKeyCode)
                    item.Checked = true;
                
                int key = vkCode; // замыкание
                item.Click += (s, e) => SetActivationKey(key);
                menu.Items.Add(item);
            }
            
            // Закрываем меню после выбора пункта
            menu.ItemClicked += (s, e) => menu.Close();
            
            // Показываем меню в позиции курсора
            menu.Show(System.Windows.Forms.Cursor.Position);
        }
        
        // Установка новой клавиши активации
        private static void SetActivationKey(int vkCode)
        {
            DebugLogger.Log(string.Format("SetActivationKey: {0} (0x{1:X2})", vkCode, vkCode));
            
            // Деактивируем Ghost Mode если активен
            if (_logic.IsGhostModeActive)
                _logic.DeactivateGhostMode();
            
            // Отписываемся от старого logic
            UnsubscribeHookEvents();
            
            // Dispose старого logic
            _logic.Dispose();
            
            // Создаём новый logic
            _logic = new GhostLogic(_activationType);
            _logic.ActivationKeyCode = vkCode;
            
            // Обновляем хук
            _keyboardHook.Dispose();
            _keyboardHook = new KeyboardHook(_logic);
            
            // Переподписываем
            SubscribeHookEvents();
            
            // Сохраняем настройку
            _activationKeyCode = vkCode;
            SaveSettings();
        }
    }
}
