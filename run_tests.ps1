Write-Host "Building and running Magnetizing Algorithm E2E Tests..." -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Stop"

try {
    Write-Host "🔨 Building test project..." -ForegroundColor Yellow
    dotnet build tests\Magnetizing_FPG.Tests.csproj -c Release

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }

    Write-Host ""
    Write-Host "🚀 Running tests..." -ForegroundColor Yellow
    Write-Host ""

    & "tests\bin\Release\net48\Magnetizing_FPG.Tests.exe"

    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed with exit code $LASTEXITCODE"
    }

    Write-Host ""
    Write-Host "✅ Tests completed successfully!" -ForegroundColor Green
}
catch {
    Write-Host ""
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Press any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")