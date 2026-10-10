@echo off
setlocal
pushd "%~dp0..\.." || exit /b 1
call codex %*
set "result=%errorlevel%"
popd
exit /b %result%
