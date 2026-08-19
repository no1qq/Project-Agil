@echo off
setlocal

rem Builds both distribution artifacts: the installer and the portable exe.

call "%~dp0build-installer.bat"
set INSTALLER=%errorlevel%

call "%~dp0build-portable.bat"
set PORTABLE=%errorlevel%

echo.
echo === Summary ===
if "%INSTALLER%"=="0" (echo installer  ok) else (echo installer  failed or skipped, code %INSTALLER%)
if "%PORTABLE%"=="0"  (echo portable   ok) else (echo portable   failed, code %PORTABLE%)
echo.

endlocal