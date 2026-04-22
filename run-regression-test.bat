@echo off
echo Building KeyboardHookRegressionTest...

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo ERROR: C# compiler not found at "%CSC%"
    echo Please install .NET Framework 4.x Developer Pack.
    exit /b 1
)

if not exist "bin" mkdir "bin"

set "OUTPUT=bin\KeyboardHookRegressionTest.exe"
set "SOURCES=KeyboardHookRegressionTest.cs JsonFileSerializer.cs NativeMethods.cs KeyboardHook.cs MouseHook.cs GhostController.cs ActivationStateManager.cs WindowTransparencyManager.cs TooltipService.cs SettingsManager.cs ProfileManager.cs OpacityProfilePresets.cs HotkeyManager.cs DebugLogger.cs IActivationHost.cs ActivationKeyCatalog.cs ActivationTypeExtensions.cs AppContext.cs TrayMenuController.cs Models\Settings.cs Models\Profile.cs Models\GhostWindowState.cs"

echo Compiling...
"%CSC%" /nologo /target:exe /out:"%OUTPUT%" ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.Runtime.Serialization.dll ^
    %SOURCES% 2> compile_errors.txt

if errorlevel 1 (
    echo.
    echo COMPILATION FAILED!
    echo.
    type compile_errors.txt
    exit /b 1
)

echo.
echo Running regression test...
"%OUTPUT%"
set "TEST_EXIT_CODE=%ERRORLEVEL%"

if exist compile_errors.txt del compile_errors.txt
exit /b %TEST_EXIT_CODE%
