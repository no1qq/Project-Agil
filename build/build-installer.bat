@echo off
setlocal

rem Builds the installer version of Project-Agil.
rem This is the framework-dependent build, so the installer stays small and
rem fetches the .NET 8 Desktop Runtime from Microsoft when the machine needs it.
rem Requires Inno Setup 6 (https://jrsoftware.org/isdl.php).
rem The build number comes from build\version.txt, which build-all.bat writes.
rem Running this script on its own reuses that number rather than counting up,
rem because a build is the installer and the portable exe together.

set ROOT=%~dp0..
set PROJECT=%ROOT%\src\ProjectAgil\ProjectAgil.csproj
set APPOUT=%ROOT%\dist\app
set ARTIFACT=%ROOT%\dist\Project-Agil-Setup.exe

set BUILD=
for /f "usebackq tokens=* delims= " %%v in ("%ROOT%\build\version.txt") do set BUILD=%%v

if "%BUILD%"=="" (
  echo build\version.txt is missing or empty.
  exit /b 1
)

echo.
echo === Project-Agil installer build b%BUILD% ===
echo.

if exist "%APPOUT%" rmdir /s /q "%APPOUT%"
if exist "%ARTIFACT%" del /q "%ARTIFACT%"

dotnet publish "%PROJECT%" ^
  -c Release ^
  -r win-x64 ^
  --self-contained false ^
  -p:PublishSingleFile=false ^
  -p:DebugType=none ^
  -p:BuildNumber=%BUILD% ^
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
if exist "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" set ISCC=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe

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
echo Installer written to:
echo   %ARTIFACT%
for %%F in ("%ARTIFACT%") do echo   size: %%~zF bytes
echo.

endlocal
