@echo off
echo Building and running Magnetizing Algorithm E2E Tests...
echo.

cd /d "%~dp0"

echo 🔨 Building test project...
dotnet build tests\Magnetizing_FPG.Tests.csproj -c Release

if %ERRORLEVEL% neq 0 (
    echo ❌ Build failed!
    pause
    exit /b 1
)

echo.
echo 🚀 Running tests...
echo.

tests\bin\Release\net48\Magnetizing_FPG.Tests.exe

if %ERRORLEVEL% neq 0 (
    echo ❌ Tests failed!
    pause
    exit /b 1
)

echo.
echo ✅ Tests completed!
pause