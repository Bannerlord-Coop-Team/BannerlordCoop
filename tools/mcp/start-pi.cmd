@echo off
setlocal
pushd "%~dp0..\.." || exit /b 1
call pi %*
set "result=%errorlevel%"
popd
exit /b %result%
