@echo off
setlocal

rem Builds both distribution artifacts: the installer and the portable exe.
rem One build number covers both, a release always ships the pair.
rem
rem   build-all.bat        uses the number in build\version.txt
rem   build-all.bat 121    writes 121 into build\version.txt first
rem
rem A leading b is accepted and dropped, so b121 and 121 mean the same thing.

set ROOT=%~dp0..
set GIVEN=%~1

if not "%GIVEN%"=="" (
  set GIVEN=%GIVEN:b=%
)

if not "%GIVEN%"=="" (
  echo %GIVEN%| findstr /r "^[0-9][0-9]*$" >nul
  if errorlevel 1 (
    echo The build number must be digits only, for example 121 or b121.
    exit /b 1
  )
  >"%ROOT%\build\version.txt" echo %GIVEN%
)

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
