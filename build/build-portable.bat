@echo off
setlocal

rem Builds the portable single-file build of Project-Agil.
rem Everything is embedded: the .NET runtime, WPF and every dependency.
rem The result is one large exe that runs on a machine with nothing installed.

set ROOT=%~dp0..
set PROJECT=%ROOT%\src\ProjectAgil\ProjectAgil.csproj
set OUT=%ROOT%\dist\portable

echo.
echo === Project-Agil portable build ===
echo.

if exist "%OUT%" rmdir /s /q "%OUT%"

dotnet publish "%PROJECT%" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeAllContentForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=false ^
  -p:DebugType=none ^
  -o "%OUT%"

if errorlevel 1 (
  echo.
  echo Build failed.
  exit /b 1
)

echo.
echo Portable build written to:
echo   %OUT%\Project-Agil.exe
echo.
for %%F in ("%OUT%\Project-Agil.exe") do echo   size: %%~zF bytes
echo.

endlocal