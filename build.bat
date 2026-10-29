@echo off
setlocal EnableDelayedExpansion
pushd "%~dp0"

echo Stopping running KirbyTaskbarBuddy...
taskkill /f /im KirbyTaskbarBuddy.exe >nul 2>&1

set "RES_FLAGS="
for %%F in (sprites\kirby-*.png) do (
    set "RES_FLAGS=!RES_FLAGS! /resource:sprites\%%~nxF,%%~nxF"
)

echo Compiling single-file KirbyTaskbarBuddy.exe (with embedded sprites)...
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /optimize+ /platform:x86 /target:winexe /win32icon:sprites\kirby.ico /out:KirbyTaskbarBuddy.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll !RES_FLAGS! KirbyTaskbarBuddy.cs

if %ERRORLEVEL% EQU 0 (
    echo Starting KirbyTaskbarBuddy.exe...
    start "" "%~dp0KirbyTaskbarBuddy.exe"
    echo Done!
) else (
    echo Build failed!
    pause
)
popd
