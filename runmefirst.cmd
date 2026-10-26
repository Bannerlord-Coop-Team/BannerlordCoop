@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Unrestricted -File "%~dp0prepare-links.ps1"
if errorlevel 1 exit /b %errorlevel%
rem prepare-links.ps1 only saved BANNERLORD_DIR to the user environment, so reload it for setup.ps1.
for /f "tokens=2,*" %%A in ('reg query HKCU\Environment /v BANNERLORD_DIR 2^>nul ^| findstr /i "REG_"') do set "BANNERLORD_DIR=%%B"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\mcp\setup.ps1"
exit /b %errorlevel%
