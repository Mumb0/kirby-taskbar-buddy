@echo off
echo Stopping running KirbyTaskbarBuddy...
echo stop > "%~dp0.stop"
timeout /t 1 /nobreak >nul
taskkill /f /im KirbyTaskbarBuddy.exe >nul 2>&1
del /f /q "%~dp0.stop" >nul 2>&1
del /f /q "%~dp0.KirbyTaskbarBuddy.prev.exe" >nul 2>&1

echo Compiling KirbyTaskbarBuddy.cs...
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /win32icon:"%~dp0sprites\kirby.ico" /out:"%~dp0KirbyTaskbarBuddy.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "%~dp0KirbyTaskbarBuddy.cs"

if %ERRORLEVEL% EQU 0 (
    echo Starting KirbyTaskbarBuddy.exe...
    start "" "%~dp0KirbyTaskbarBuddy.exe"
    echo Done!
) else (
    echo Build failed!
    pause
)
