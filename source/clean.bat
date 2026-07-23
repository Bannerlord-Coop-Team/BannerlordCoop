@echo off
setlocal enabledelayedexpansion

set "targetDir=obj"

for /d /r %%d in (!targetDir!) do (
    rd /s /q "%%d"
)

set "targetDir=bin"

for /d /r %%d in (!targetDir!) do (
    rd /s /q "%%d"
)


echo Removal of "!targetDir!" directories complete.
pause