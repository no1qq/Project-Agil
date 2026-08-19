@echo off
setlocal

rem Builds both distribution artifacts: the installer and the portable exe.
rem One build number covers both, a release always ships the pair.

set ROOT=%~dp0..

set BUILD=
for /f "usebackq tokens=* delims= " %%v in ("%ROOT%\build\version.txt") do set BUILD=%%v

if "%BUILD%"=="" (
  echo build\version.txt is missing or empty.
  exit /b 1
)

echo.
echo ############################
echo   Project-Agil build b%BUILD%
echo ############################

call "%~dp0build-installer.bat"
set INSTALLER=%errorlevel%

call "%~dp0build-portable.bat"
set PORTABLE=%errorlevel%

echo.
echo === Summary for b%BUILD% ===
if "%INSTALLER%"=="0" (echo installer  ok   dist\Project-Agil-Setup.exe) else (echo installer  failed or skipped, code %INSTALLER%)
if "%PORTABLE%"=="0"  (echo portable   ok   dist\Project-Agil-Portable.exe) else (echo portable   failed, code %PORTABLE%)
echo.

if not "%INSTALLER%"=="0" exit /b %INSTALLER%
if not "%PORTABLE%"=="0" exit /b %PORTABLE%

echo Build b%BUILD% is complete. Tag the release b%BUILD% and attach both files.
echo.

endlocal
