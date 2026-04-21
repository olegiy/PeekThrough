# GhostThrough

GhostThrough — это лёгкая Windows-утилита в системном трее, которая после осознанного удержания клавиши или кнопки делает окно под курсором полупрозрачным и click-through. Она предназначена для быстрого "подглядывания" за окном без сворачивания и перестройки рабочего пространства.

Languages: [English](README.md) | [Русский](README.ru.md)

Примечание по синхронизации: `README.md` и `README.ru.md` нужно обновлять вместе.

Примечание о происхождении идеи: исходная идея вдохновлена проектом Peek Through от Luke Payne:
http://www.lukepaynesoftware.com/projects/peek-through/

[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-0078d7.svg)](https://www.microsoft.com/windows)
[![.NET Framework: 4.0](https://img.shields.io/badge/.NET_Framework-4.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework/net40)

## Обзор

GhostThrough работает из области уведомлений и ждёт жест активации.

Когда включается Ghost Mode, корневому окну под курсором назначаются:

- `WS_EX_LAYERED` для альфа-прозрачности
- `WS_EX_TRANSPARENT`, чтобы ввод мыши проходил сквозь окно

Приложение игнорирует системные окна оболочки, такие как рабочий стол и панель задач, восстанавливает изменённые стили окон при выходе и хранит настройки в `%APPDATA%\PeekThrough\settings.json`.

## Текущее поведение

Задержка активации всегда равна `1 секунде`, но после активации клавиатурный и мышиный режимы ведут себя по-разному.

### Режим клавиатуры

- Удерживайте клавишу активации 1 секунду, чтобы включить Ghost Mode.
- После активации можно отпустить клавишу, и Ghost Mode останется активным.
- Коротко нажмите клавишу активации ещё раз, чтобы отключить Ghost Mode.
- Нажмите `Esc` в любой момент во время Ghost Mode для немедленного выхода.

### Режим мыши

- Удерживайте выбранную кнопку мыши 1 секунду, чтобы включить Ghost Mode.
- Отпускание выбранной кнопки мыши отключает Ghost Mode.
- Если другая кнопка мыши нажата раньше выбранной, активация блокируется.
- Если другая кнопка мыши нажата в момент удержания выбранной, эта попытка активации тоже блокируется.

### Когда Ghost Mode активен

- Взаимодействие мышью происходит с окнами, которые находятся под ghost-окном.
- `Ctrl+Shift+Up` и `Ctrl+Shift+Down` переключают профили прозрачности.
- Если профиль меняется во время Ghost Mode, прозрачность переустанавливается сразу.
- Небольшая подсказка возле курсора показывает текущее состояние и активный профиль.
- При активации и деактивации звучит короткий звуковой сигнал.

## Варианты активации

GhostThrough поддерживает два режима активации:

- Активация клавиатурой
- Активация мышью

### Активация клавиатурой

Выбираемые клавиши активации ограничены клавишами, не являющимися модификаторами, а также клавишами Windows, чтобы приложение не ломало привычные сочетания клавиш в других программах.

Текущие варианты в меню трея:

- `Left Win`, `Right Win`
- `Caps Lock`, `Tab`, `Space`, `Escape`, `Tilde (\`~)`
- `Insert`, `Delete`, `Home`, `End`, `Page Up`, `Page Down`
- `0`-`9`
- `F1`-`F12`

Клавиши-модификаторы намеренно отклоняются и нормализуются обратно к `Left Win`:

- `Ctrl`
- `Shift`
- `Alt`

### Активация мышью

Поддерживаемые кнопки активации:

- Средняя кнопка
- Правая кнопка
- X1 button
- X2 button

## Профили прозрачности

Сейчас GhostThrough поставляется с девятью встроенными пресетами прозрачности и сохраняет активный пресет в настройках:

- `10%` (`26/255`)
- `20%` (`51/255`)
- `30%` (`76/255`)
- `40%` (`102/255`)
- `50%` (`128/255`)
- `60%` (`153/255`)
- `70%` (`178/255`)
- `80%` (`204/255`)
- `90%` (`230/255`)

Профиль по умолчанию — `10%`.

Старые значения по умолчанию с тремя профилями (`min` / `med` / `max`) автоматически нормализуются в новый набор из девяти профилей с сохранением ближайшей прозрачности.

## Меню в трее

Главного окна нет. Основной интерфейс приложения — иконка в трее, и сейчас в ней доступны:

- `Activation Key`
- `Activation Method`
- `Exit`

Пункт `Activation Method` открывает подменю:

- `Keyboard`
- `Mouse (Middle Button)`
- `Mouse (Right Button)`
- `Mouse (X1 Button)`
- `Mouse (X2 Button)`

## Архитектура

Приложение — это небольшая WinForms/Win32-утилита, разбитая на узкоспециализированные классы:

- `Program.cs` - точка входа, защита от второго экземпляра, запуск и завершение приложения
- `AppContext.cs` - собирает runtime-сервисы, hooks, controller и сохранённые настройки
- `TrayMenuController.cs` - владеет `NotifyIcon` и действиями меню трея
- `GhostController.cs` - координирует активацию, деактивацию, tooltip, звуки и смену профилей
- `ActivationStateManager.cs` - отслеживает таймеры удержания, окна подавления и состояние активации
- `WindowTransparencyManager.cs` - применяет и восстанавливает layered/transparent-стили окон
- `KeyboardHook.cs` - low-level keyboard hook, обработка activation key, `Esc` и hotkeys профилей
- `MouseHook.cs` - low-level mouse hook с отслеживанием конфликтующих нажатий кнопок
- `ProfileManager.cs` - управляет активным профилем прозрачности и циклическим переключением
- `OpacityProfilePresets.cs` - общие пресеты по умолчанию и вспомогательная логика нормализации старых профилей
- `HotkeyManager.cs` - захардкоженное переключение профилей через `Ctrl+Shift+Up/Down`
- `SettingsManager.cs` - загрузка/сохранение JSON и миграция старых строковых настроек
- `ActivationKeyCatalog.cs` - список доступных клавиш активации и их display name
- `ActivationTypeExtensions.cs` - вспомогательные методы преобразования сохранённых значений режима активации
- `IActivationHost.cs` - небольшой контракт, который используют глобальные input hooks
- `TooltipService.cs` - маленькая плавающая форма tooltip возле курсора
- `NativeMethods.cs` - объявления Win32 API и константы
- `DebugLogger.cs` - асинхронный файловый логгер для `peekthrough_debug.log`
- `KeyboardHookRegressionTest.cs` - небольшой standalone regression test executable

## Формат настроек

Настройки хранятся как JSON v2 в `%APPDATA%\PeekThrough\settings.json`:

```json
{
  "Version": 2,
  "Activation": {
    "Type": "keyboard",
    "KeyCode": 91,
    "MouseButton": 4
  },
  "Profiles": {
    "List": [
      { "Id": "p10", "Name": "10%", "Opacity": 26 },
      { "Id": "p20", "Name": "20%", "Opacity": 51 }
    ],
    "ActiveId": "p10"
  },
  "Hotkeys": {
    "NextProfile": { "Ctrl": true, "Shift": true, "Alt": false, "Key": "Up" },
    "PrevProfile": { "Ctrl": true, "Shift": true, "Alt": false, "Key": "Down" }
  }
}
```

Примечания:

- Старые построчные настройки автоматически мигрируют, а исходный файл сохраняется как `.bak`.
- Старый набор из трёх профилей по умолчанию автоматически обновляется до нового списка из девяти пресетов.
- Секция `Hotkeys` сохраняется для совместимости, но на практике горячие клавиши профилей сейчас захардкожены в `HotkeyManager.cs`.

## Сборка

В репозитории нет `.csproj` или `.sln`. Поддерживаемый путь сборки — `compile.bat`.

### Быстрая сборка

```bat
compile.bat
```

Если `PeekThrough.exe` уже запущен, перед пересборкой закройте его через трей, иначе компилятор не сможет перезаписать файл.

### Ручная сборка

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /out:PeekThrough.exe /win32icon:resources\icons\icon.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Program.cs NativeMethods.cs KeyboardHook.cs MouseHook.cs GhostController.cs ActivationStateManager.cs WindowTransparencyManager.cs TooltipService.cs SettingsManager.cs ProfileManager.cs OpacityProfilePresets.cs HotkeyManager.cs DebugLogger.cs IActivationHost.cs ActivationKeyCatalog.cs ActivationTypeExtensions.cs AppContext.cs TrayMenuController.cs Models\Settings.cs Models\Profile.cs Models\GhostWindowState.cs
```

Примечание: кодовая база и артефакты сборки пока ещё используют прежнее имя исполняемого файла `PeekThrough.exe`.

## Регрессионный тест

В репозитории есть автономный регрессионный тест для keyboard hook и связанного activation-кода:

- `KeyboardHookRegressionTest.cs`

Сейчас он проверяет:

- преобразование строковых значений типа активации
- доступность каталога клавиш активации
- доступ к клавише активации контроллера через `IActivationHost`
- порядок обработки activation key внутри keyboard hook
- отклонение клавиш-модификаторов как activation key
- сброс очереди debug-лога на диск

Сборка и запуск:

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:KeyboardHookRegressionTest.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll KeyboardHookRegressionTest.cs NativeMethods.cs KeyboardHook.cs MouseHook.cs GhostController.cs ActivationStateManager.cs WindowTransparencyManager.cs TooltipService.cs SettingsManager.cs ProfileManager.cs OpacityProfilePresets.cs HotkeyManager.cs DebugLogger.cs IActivationHost.cs ActivationKeyCatalog.cs ActivationTypeExtensions.cs AppContext.cs TrayMenuController.cs Models\Settings.cs Models\Profile.cs Models\GhostWindowState.cs && KeyboardHookRegressionTest.exe
```

Ожидаемый результат:

```text
PASS
```

## Логирование

- Отладочные логи пишутся в `peekthrough_debug.log` рядом с исполняемым файлом.
- Установите переменную окружения `PEEKTHROUGH_LOG_LEVEL=INFO`, чтобы уменьшить объём подробного debug-логирования.

## Установка

### Для пользователей

1. Скачайте `PeekThrough.exe` из Releases или соберите его локально.
2. Запустите исполняемый файл.
3. При желании добавьте ярлык в Windows Startup.

### Для разработчиков

1. Клонируйте репозиторий.
2. Соберите проект через `compile.bat`.
3. Запустите `PeekThrough.exe`.

## Известные ограничения

- Приложение работает только на Windows и зависит от low-level global hooks и манипуляции Win32-стилями окон.
- В репозитории сейчас отслеживаются сгенерированные файлы вроде `PeekThrough.exe`, `PeekThrough.pdb` и `peekthrough_debug.log`.
- По-прежнему нет installer, updater, `.csproj` и полноценного автоматизированного набора тестов.
- В корне репозитория пока нет файла `LICENSE`.

Автор: [olegiy](https://github.com/olegiy)
