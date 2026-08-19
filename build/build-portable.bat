@echo off
setlocal

rem Builds the portable single-file build of Project-Agil.
rem Everything is embedded: the .NET runtime, WPF and every dependency.
rem The result is one large exe that runs on a machine with nothing installed.
rem The build number comes from build\version.txt, which build-all.bat writes.
rem Running this script on its own reuses that number rather than counting up,
rem because a build is the installer and the portable exe together.

set ROOT=%~dp0..
set PROJECT=%ROOT%\src\ProjectAgil\ProjectAgil.csproj
set OUT=%ROOT%\dist\portable
set ARTIFACT=%ROOT%\dist\Project-Agil-Portable.exe

set BUILD=
for /f "usebackq tokens=* delims= " %%v in ("%ROOT%\build\version.txt") do set BUILD=%%v

if "%BUILD%"=="" (
  echo build\version.txt is missing or empty.
  exit /b 1
)

echo.
echo === Project-Agil portable build b%BUILD% ===
echo.

if exist "%OUT%" rmdir /s /q "%OUT%"
if exist "%ARTIFACT%" del /q "%ARTIFACT%"

dotnet publish "%PROJECT%" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeAllContentForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=false ^
  -p:DebugType=none ^
  -p:BuildNumber=%BUILD% ^
  -o "%OUT%"

if errorlevel 1 (
  echo.
  echo Build failed.
  exit /b 1
)

copy /y "%OUT%\Project-Agil.exe" "%ARTIFACT%" >nul

if errorlevel 1 (
  echo.
  echo The portable exe could not be copied into dist.
  exit /b 1
)

echo.
echo Portable build written to:
echo   %ARTIFACT%
for %%F in ("%ARTIFACT%") do echo   size: %%~zF bytes
echo.

endlocal
