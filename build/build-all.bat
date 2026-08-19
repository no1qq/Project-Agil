@echo off
setlocal

rem Builds both distribution artifacts: the installer and the portable exe.
rem One build number covers both, a release always ships the pair.
rem
rem Just run this. Nothing to type, nothing to edit. The build number counts
rem itself up every run and build\version.txt records the number that was
rem used, so the file reads as a log of what has been built rather than
rem something anyone has to maintain.
rem
rem If the count ever needs correcting, either edit build\version.txt to the
rem last build that happened, or run build-all.bat 121 to jump straight to
rem that number. Neither is needed in normal use.

set ROOT=%~dp0..
set GIVEN=%~1

set LAST=0
if exist "%ROOT%\build\version.txt" (
  for /f "usebackq tokens=* delims= " %%v in ("%ROOT%\build\version.txt") do set LAST=%%v
)

echo %LAST%| findstr /r "^[0-9][0-9]*$" >nul
if errorlevel 1 set LAST=0

if not "%GIVEN%"=="" set GIVEN=%GIVEN:b=%

if "%GIVEN%"=="" (
  set /a BUILD=LAST+1
) else (
  echo %GIVEN%| findstr /r "^[0-9][0-9]*$" >nul
  if errorlevel 1 (
    echo The build number must be digits only, for example 121 or b121.
    exit /b 1
  )
  set BUILD=%GIVEN%
)

>"%ROOT%\build\version.txt" echo %BUILD%

echo.
echo ############################
echo   Project-Agil build
echo ############################

call "%~dp0build-installer.bat"
set INSTALLER=%errorlevel%

call "%~dp0build-portable.bat"
set PORTABLE=%errorlevel%

echo.
echo === Summary ===
if "%INSTALLER%"=="0" (echo installer  ok   dist\Project-Agil-Setup.exe) else (echo installer  failed or skipped, code %INSTALLER%)
if "%PORTABLE%"=="0"  (echo portable   ok   dist\Project-Agil-Portable.exe) else (echo portable   failed, code %PORTABLE%)
echo.

if not "%INSTALLER%"=="0" exit /b %INSTALLER%
if not "%PORTABLE%"=="0" exit /b %PORTABLE%

echo Done. Both files are in dist. Tag the release b%BUILD%.
echo.

endlocal
