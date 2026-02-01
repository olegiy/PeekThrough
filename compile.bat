@echo off
echo Building PeekThrough with Debug Logging...

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo ERROR: C# compiler not found at %CSC%
    echo Please check your .NET Framework installation.
    pause
    exit /b 1
)

set OUTPUT=PeekThrough.exe

if exist %OUTPUT% (
    echo Removing old executable...
    del %OUTPUT%
)

echo Compiling...
"%CSC%" /target:winexe /out:%OUTPUT% ^
    /win32icon:resources\icons\icon.ico ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Drawing.dll ^
    /debug ^
    Program.cs NativeMethods.cs KeyboardHook.cs GhostLogic.cs DebugLogger.cs ^
    2> compile_errors.txt

if errorlevel 1 (
    echo.
    echo COMPILATION FAILED!
    echo.
    type compile_errors.txt
    pause
    exit /b 1
) else (
    echo.
    echo Build successful: %OUTPUT%
    echo.
    if exist compile_errors.txt del compile_errors.txt
)
