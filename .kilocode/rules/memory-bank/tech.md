# Ghost Window - Technical Details

## Technology Stack

### Primary Language
- **C#** (.NET Framework 4.0)
- **Target**: Windows executable (winexe)

### Framework Dependencies
- **System.Windows.Forms** — для тултипа и ApplicationContext
- **System.Drawing** — для Point, Size, Color
- **System.Runtime.InteropServices** — для P/Invoke

### Build System
- **Compiler**: csc.exe (C# Compiler from .NET Framework 4.0)
- **Build Script**: compile.bat
- **Output**: PeekThrough.exe

## Development Environment

### Build Commands
```batch
# Build
csc.exe /target:winexe /out:PeekThrough.exe ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Drawing.dll ^
    Program.cs NativeMethods.cs KeyboardHook.cs GhostLogic.cs

# Or use compile.bat
compile.bat
```

### Compiler Path
```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

## File Structure

```
PeekThrough/
├── Program.cs          # Entry point, single instance, app loop
├── GhostLogic.cs       # Core ghost mode logic
├── KeyboardHook.cs     # Global keyboard hook
├── NativeMethods.cs    # Windows API P/Invoke declarations
├── compile.bat         # Build script
├── .gitignore          # Git ignore rules
└── PeekThrough.exe     # Compiled executable
```

## Windows API Usage

### Hooking APIs
| Function | Purpose |
|----------|---------|
| `SetWindowsHookEx` | Установка глобального хука клавиатуры (WH_KEYBOARD_LL) |
| `UnhookWindowsHookEx` | Удаление хука при выходе |
| `CallNextHookEx` | Передача события следующему хуку |

### Window Manipulation APIs
| Function | Purpose |
|----------|---------|
| `WindowFromPoint` | Получение HWND окна под курсором |
| `GetAncestor` | Получение корневого окна (GA_ROOT = 2) |
| `GetClassName` | Получение класса окна для фильтрации |
| `GetWindowLong` | Чтение расширенного стиля окна |
| `SetWindowLong` | Установка расширенного стиля (WS_EX_LAYERED, WS_EX_TRANSPARENT) |
| `SetLayeredWindowAttributes` | Установка прозрачности (alpha = 80) |

### Input APIs
| Function | Purpose |
|----------|---------|
| `SendInput` | Симуляция нажатия клавиши Win |
| `GetCursorPos` | Получение позиции курсора |
| `Beep` | Звуковой сигнал |

## Key Constants

```csharp
// Hook constants
WH_KEYBOARD_LL = 13
WM_KEYDOWN = 0x0100
WM_KEYUP = 0x0101
VK_LWIN = 0x5B

// Window style constants
GWL_EXSTYLE = -20
WS_EX_TRANSPARENT = 0x00000020
WS_EX_LAYERED = 0x00080000
LWA_ALPHA = 0x2

// Ancestor constant
GA_ROOT = 2

// Input constants
INPUT_KEYBOARD = 1
KEYEVENTF_KEYUP = 0x0002
```

## Technical Constraints

1. **Single Instance**: Используется Mutex с именем "PeekThroughGhostModeApp"
2. **Global Hook**: Требует права на установку low-level hook
3. **Timer Precision**: 500ms порог для определения длинного нажатия
4. **Transparency Level**: 80/255 (~31% непрозрачности)
5. **Ignored Window Classes**: Progman, WorkerW, Shell_TrayWnd

## Memory Management
- KeyboardHook и GhostLogic реализуют IDisposable
- Cleanup в Program.cs при выходе из Application.Run()
- Автоматическое восстановление стилей окон при выходе

## Known Limitations

1. **UWP/Modern Apps**: Некоторые современные приложения Windows могут некорректно обрабатывать WS_EX_TRANSPARENT
2. **Administrator Rights**: Для некоторых окон может потребоваться запуск от администратора
3. **Already Layered Windows**: Окна, изначально имеющие WS_EX_LAYERED, могут сохранить прозрачность после восстановления (обрабатывается в RestoreWindow)
4. **No Config**: Нет внешнего конфигурационного файла — все параметры hardcoded
