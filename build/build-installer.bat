@echo off
setlocal enabledelayedexpansion

rem Builds the installer version of Project-Agil.
rem This is the framework-dependent build, so the installer stays small and
rem fetches the .NET 8 Desktop Runtime from Microsoft when the machine needs it.
rem Requires Inno Setup 6 (https://jrsoftware.org/isdl.php).

set ROOT=%~dp0..
set PROJECT=%ROOT%\src\ProjectAgil\ProjectAgil.csproj
set APPOUT=%ROOT%\dist\app

echo.
echo === Project-Agil installer build ===
echo.

if exist "%APPOUT%" rmdir /s /q "%APPOUT%"

dotnet publish "%PROJECT%" ^
  -c Release ^
  -r win-x64 ^
  --self-contained false ^
  -p:PublishSingleFile=false ^
  -p:DebugType=none ^
  -o "%APPOUT%"

if errorlevel 1 (
  echo.
  echo Build failed.
  exit /b 1
)

rem Locate the Inno Setup compiler.
set ISCC=
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe

if "%ISCC%"=="" (
  echo.
  echo The application was published to %APPOUT%
  echo but Inno Setup 6 was not found, so no installer was produced.
  echo.
  echo Install it from https://jrsoftware.org/isdl.php and run this script again.
  exit /b 2
)

"%ISCC%" "%ROOT%\build\ProjectAgil.iss"

if errorlevel 1 (
  echo.
  echo Installer compilation failed.
  exit /b 1
)

echo.
echo Installer written to %ROOT%\dist
echo.

endlocal