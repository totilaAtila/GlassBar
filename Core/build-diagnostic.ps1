# Build WindowDiagnostic tool
Write-Host "Building Window Diagnostic Tool..." -ForegroundColor Cyan

$sourceFile = "WindowDiagnostic.cpp"
$outputExe = "WindowDiagnostic.exe"

# Compile with cl.exe
cl.exe /EHsc /std:c++17 /O2 /Fe:$outputExe $sourceFile user32.lib psapi.lib

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Build successful: $outputExe" -ForegroundColor Green
    Write-Host ""
    Write-Host "To run:" -ForegroundColor Yellow
    Write-Host "  .\$outputExe" -ForegroundColor Yellow
} else {
    Write-Host "✗ Build failed" -ForegroundColor Red
}
