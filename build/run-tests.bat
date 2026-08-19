@echo off
setlocal

rem Runs the Project-Agil test suite.
rem Besides the normal unit tests this also lints the source tree:
rem   - no em dash or other fancy dash anywhere
rem   - no leftover comments in .cs files
rem   - every SymbolRegular name exists and stays inside the basic plane

set ROOT=%~dp0..
set TESTS=%ROOT%\tests\ProjectAgil.Tests\ProjectAgil.Tests.csproj

echo.
echo === Project-Agil tests ===
echo.

dotnet test "%TESTS%" --nologo

if errorlevel 1 (
  echo.
  echo Tests failed.
  exit /b 1
)

echo.
echo All tests passed.
echo.

endlocal
