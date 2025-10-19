# Build script for ozzutil library on Windows
# Usage: ./build-ozzutil-windows.ps1 [build_type]
# Example: ./build-ozzutil-windows.ps1 Release
# Example: ./build-ozzutil-windows.ps1 Debug

param(
    [string]$BuildType = "Release"
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$SokolCharpRoot = Split-Path -Parent $ScriptDir
$OzzUtilDir = "$SokolCharpRoot\ext\ozzutil"
$BuildDir = "$OzzUtilDir\build-vs2022-windows"

Write-Host "==========================================" -ForegroundColor Green
Write-Host "Building ozzutil for Windows" -ForegroundColor Green
Write-Host "Build Type: $BuildType" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green

# Check if ozzutil directory exists
if (-not (Test-Path $OzzUtilDir)) {
    Write-Host "Error: ozzutil directory not found at $OzzUtilDir" -ForegroundColor Red
    exit 1
}

# Check if ozz-animation libraries exist
$OzzAnimationDir = "$SokolCharpRoot\ext\ozz-animation"
$OzzLibsDir = "$OzzAnimationDir\bin\windows\x64\$($BuildType.ToLower())"
if (-not (Test-Path $OzzLibsDir)) {
    Write-Host "Error: ozz-animation libraries not found. Please build ozz-animation first:" -ForegroundColor Red
    Write-Host "  .\build-ozz-animation-windows.ps1 $BuildType" -ForegroundColor Yellow
    exit 1
}

# Create build directory
if (Test-Path $BuildDir) {
    Remove-Item -Recurse -Force $BuildDir
}
New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null
Set-Location $BuildDir

# Configure with CMake for Visual Studio 2022
cmake .. `
    -G "Visual Studio 17 2022" `
    -A x64 `
    -DCMAKE_BUILD_TYPE="$BuildType"

if ($LASTEXITCODE -ne 0) {
    Write-Host "CMake configuration failed" -ForegroundColor Red
    exit 1
}

# Build
cmake --build . --config "$BuildType"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed" -ForegroundColor Red
    exit 1
}

# Create destination directory
$DestDir = "$OzzUtilDir\libs\windows\x64\$($BuildType.ToLower())"
New-Item -ItemType Directory -Force -Path $DestDir | Out-Null

# Copy library to destination
Write-Host "Copying library to $DestDir..."
$SourceLib = "$BuildDir\$BuildType\ozzutil.dll"
$DestLib = "$DestDir\ozzutil.dll"

if (Test-Path $SourceLib) {
    Copy-Item $SourceLib $DestLib
} else {
    Write-Host "Error: Built library not found at $SourceLib" -ForegroundColor Red
    Get-ChildItem -Recurse $BuildDir -Filter "*.dll" | ForEach-Object {
        Write-Host "Found: $($_.FullName)"
    }
    exit 1
}

Write-Host "==========================================" -ForegroundColor Green
Write-Host "Build complete!" -ForegroundColor Green
Write-Host "Output: $DestDir" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green

# Verify the library was created
if (Test-Path $DestLib) {
    Write-Host "✓ Successfully built ozzutil library" -ForegroundColor Green
    Get-Item $DestLib | Format-List Name, Length, LastWriteTime
} else {
    Write-Host "✗ Failed to build ozzutil library" -ForegroundColor Red
    exit 1
}