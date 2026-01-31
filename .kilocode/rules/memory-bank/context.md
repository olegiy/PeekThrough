# Ghost Window - Current Context

## Project Status
**Status**: ? Complete / Stable

Приложение полностью функционально и готово к использованию. Все основные функции реализованы:
- Глобальный перехват клавиши Win
- Прозрачность и click-through для окон
- Всплывающая подсказка и звуковая обратная связь
- Корректная обработка коротких/длинных нажатий
- **NEW**: Автоматическое отключение Ghost Mode при нажатии любой другой клавиши

## Current Focus
Нет активной разработки. Проект находится в стабильном состоянии.

## Recent Changes
- Сгенерированы концепты иконок (SVG) в `resources/icons/icon_previews.html`
- Добавлена логика отключения Ghost Mode при нажатии дополнительной клавиши
- Добавлен публичный метод `DeactivateGhostMode()` в `GhostLogic.cs`
- Добавлено публичное свойство `IsGhostModeActive` для проверки состояния
- Модифицирован `KeyboardHook.cs` для обработки нажатий других клавиш

## Next Steps / Potential Improvements
Приоритет низкий — проект выполняет свою функцию:
- [ ] Выбрать иконку и интегрировать в приложение (.ico)
- [ ] Добавить конфигурационный файл для настройки прозрачности
- [ ] Добавить поддержку других клавиш (например, configurable hotkey)
- [ ] Иконка в системном трее с меню настроек
- [ ] Автозагрузка с Windows
- [ ] Использование современного .NET (Core/5/6+) вместо .NET Framework 4.0
- [ ] Установщик (Installer/MSI)

## Build Instructions
```batch
# Простая сборка
compile.bat

# Ручная сборка
csc.exe /target:winexe /out:PeekThrough.exe ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Drawing.dll ^
    Program.cs NativeMethods.cs KeyboardHook.cs GhostLogic.cs
```

## Usage
1. Запустить `PeekThrough.exe`
2. Удерживать клавишу `Win` более 0.5 секунд над любым окном
3. Окно станет прозрачным и click-through
4. Отпустить `Win` для возврата в нормальное состояние
