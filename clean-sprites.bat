@echo off
pushd "%~dp0"
echo Stripping opaque backgrounds and divider grids from sprites\*.png...
powershell -NoProfile -Command "Add-Type -Path 'src\SpriteLoader.cs','src\FormDefinitions.cs','src\NativeMethods.cs' -ReferencedAssemblies System.Drawing,System.Windows.Forms; $n = [KirbyTaskbarBuddy.SpriteLoader]::StripSpritesheetBackgrounds('sprites'); Write-Host \"Cleaned $n spritesheet(s).\""
popd
