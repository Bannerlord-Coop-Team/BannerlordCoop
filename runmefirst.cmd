@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Unrestricted -File "%~dp0prepare-links.ps1"
if errorlevel 1 exit /b %errorlevel%
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\mcp\setup.ps1"
exit /b %errorlevel%
