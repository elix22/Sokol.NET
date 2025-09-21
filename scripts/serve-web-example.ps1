# Simple HTTP Server for WebAssembly Examples (PowerShell)
# Serves the wwwroot directory of built WebAssembly projects

param(
    [Parameter(Mandatory=$true)]
    [string]$ExamplePath
)

$wwwrootPath = Join-Path $ExamplePath "bin\Debug\net8.0\wwwroot"

if (-not (Test-Path $wwwrootPath)) {
    Write-Host "❌ Error: wwwroot not found at $wwwrootPath" -ForegroundColor Red
    Write-Host "Please build the web project first using the appropriate build task." -ForegroundColor Yellow
    exit 1
}

$PORT = 8000

try {
    Write-Host "🚀 Serving $ExamplePath at http://localhost:$PORT" -ForegroundColor Green
    Write-Host "📁 Serving directory: $wwwrootPath" -ForegroundColor Cyan
    Write-Host "Press Ctrl+C to stop the server" -ForegroundColor Yellow
    
    # Open browser automatically
    Start-Process "http://localhost:$PORT"
    
    # Start Python HTTP server
    Set-Location $wwwrootPath
    & python -m http.server $PORT
}
catch {
    if ($_.Exception.Message -like "*Address already in use*") {
        Write-Host "❌ Error: Port $PORT is already in use" -ForegroundColor Red
        Write-Host "Please stop any other web servers or use a different port" -ForegroundColor Yellow
    }
    else {
        Write-Host "❌ Error starting server: $($_.Exception.Message)" -ForegroundColor Red
    }
    exit 1
}