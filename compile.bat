@echo off
echo Building PeekThrough...

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo ERROR: C# compiler not found at "%CSC%"
    echo Please install .NET Framework 4.x Developer Pack.
    exit /b 1
)

set "OUTPUT=PeekThrough.exe"
if exist "%OUTPUT%" (
    echo Removing old executable...
    del "%OUTPUT%"
)

set "SOURCES=Program.cs NativeMethods.cs KeyboardHook.cs MouseHook.cs GhostController.cs ActivationStateManager.cs WindowTransparencyManager.cs TooltipService.cs SettingsManager.cs ProfileManager.cs OpacityProfilePresets.cs HotkeyManager.cs DebugLogger.cs Models\Settings.cs Models\Profile.cs Models\GhostWindowState.cs"

echo Compiling...
"%CSC%" /nologo /target:winexe /out:"%OUTPUT%" ^
    /win32icon:resources\icons\icon.ico ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Drawing.dll ^
    %SOURCES% 2> compile_errors.txt

if errorlevel 1 (
    echo.
    echo COMPILATION FAILED!
    echo.
    type compile_errors.txt
    exit /b 1
)

echo.
echo Build successful: %OUTPUT%
if exist compile_errors.txt del compile_errors.txt
