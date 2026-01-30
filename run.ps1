param(
    [switch]$Test
)

if ($Test) {
    Write-Host "Running SymatoIME Engine Tests..." -ForegroundColor Cyan
    dotnet run -c Test -- --test
} else {
    $process = Get-Process -Name "SymatoIME" -ErrorAction SilentlyContinue
    if ($process) {
        Write-Host "SymatoIME is already running. Restarting..." -ForegroundColor Yellow
        Stop-Process -Name "SymatoIME" -Force
        Start-Sleep -Milliseconds 500
    }
    
    Write-Host "Starting SymatoIME..." -ForegroundColor Green
    dotnet run
}
