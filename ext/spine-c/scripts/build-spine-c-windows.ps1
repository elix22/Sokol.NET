# Build script for spine-c library on Windows
# Usage: .\build-spine-c-windows.ps1 [-Architecture <arch>] [-BuildType <type>]
# Example: .\build-spine-c-windows.ps1 -Architecture x64 -BuildType Release
# Example: .\build-spine-c-windows.ps1 -Architecture Win32 -BuildType Debug
# Architectures: x64, Win32, ARM64

param(
    [string]$Architecture = "x64",
    [string]$BuildType = "Release"
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SpineCDir = Join-Path $ScriptDir "..\ext\spine-c"
$BuildDir = Join-Path $SpineCDir "build-windows-$Architecture"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Building spine-c for Windows" -ForegroundColor Cyan
Write-Host "Architecture: $Architecture" -ForegroundColor Cyan
Write-Host "Build Type: $BuildType" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# Create build directory
New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null
Set-Location $BuildDir

# Configure with CMake
Write-Host "Configuring CMake..." -ForegroundColor Yellow
cmake .. `
    -G "Visual Studio 17 2022" `
    -A $Architecture `
    -DCMAKE_BUILD_TYPE="$BuildType"

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ CMake configuration failed" -ForegroundColor Red
    exit 1
}

# Build
Write-Host "Building..." -ForegroundColor Yellow
cmake --build . --config $BuildType

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed" -ForegroundColor Red
    exit 1
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Build complete!" -ForegroundColor Green
Write-Host "Output: $BuildDir\$BuildType\spine-c.dll" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan

# Verify the library was created
$DllPath = Join-Path $BuildDir "$BuildType\spine-c.dll"
if (Test-Path $DllPath) {
    Write-Host "✓ Successfully built spine-c.dll" -ForegroundColor Green
    Get-Item $DllPath | Format-Table Name, Length, LastWriteTime
} else {
    Write-Host "✗ Failed to build spine-c.dll" -ForegroundColor Red
    exit 1
}
