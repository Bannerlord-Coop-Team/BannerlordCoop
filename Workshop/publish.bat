@echo off
rem Publishes %BANNERLORD_DIR%\Modules\Coop with a workshop task file (default WorkshopUpdate.xml, or pass WorkshopCreate.xml).
setlocal
if not defined BANNERLORD_DIR (
    echo BANNERLORD_DIR is not set. Run runmefirst.cmd, then restart this terminal.
    exit /b 1
)
set "TASK_TEMPLATE=%~dp0%~1"
if "%~1"=="" set "TASK_TEMPLATE=%~dp0WorkshopUpdate.xml"
set "TASK_FILE=%TEMP%\CoopWorkshop-%RANDOM%%RANDOM%.xml"
rem The workshop tool can't expand variables, so write a copy with the module path filled in.
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$text = [IO.File]::ReadAllText($env:TASK_TEMPLATE); $module = [Security.SecurityElement]::Escape((Join-Path $env:BANNERLORD_DIR 'Modules\Coop')); [IO.File]::WriteAllText($env:TASK_FILE, $text.Replace('%%BANNERLORD_DIR%%\Modules\Coop', $module))"
if errorlevel 1 exit /b %errorlevel%
"%BANNERLORD_DIR%\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.SteamWorkshop.exe" "%TASK_FILE%"
set "PUBLISH_EXIT=%errorlevel%"
del "%TASK_FILE%"
exit /b %PUBLISH_EXIT%
