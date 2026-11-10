@echo off
pushd "%~dp0"
echo Stripping opaque backgrounds and divider grids from sprites\*.png...
powershell -NoProfile -Command "Add-Type -Path 'src\Rendering\SpritesheetCleaner.cs' -ReferencedAssemblies System.Drawing; $n = [KirbyTaskbarBuddy.SpritesheetCleaner]::StripSpritesheetBackgrounds('sprites'); Write-Host \"Cleaned $n spritesheet(s).\""
popd
