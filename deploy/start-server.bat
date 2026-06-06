@echo off
setlocal

set "GAME_DIR=%~dp0..\..\bin\Win64_Shipping_Client"
set "EXE=%GAME_DIR%\Bannerlord.exe"
set "MODULES=_MODULES_*Native*SandBoxCore*SandBox*StoryMode*Coop*_MODULES_"

if not exist "%EXE%" (
    echo Could not find Bannerlord.exe at:
    echo "%EXE%"
    echo.
    echo Make sure this file is inside the Coop module folder.
    pause
    exit /b 1
)

cd /d "%GAME_DIR%"

start "Coop Bannerlord" "%EXE%" /singleplayer /server "%MODULES%"

exit /b 0