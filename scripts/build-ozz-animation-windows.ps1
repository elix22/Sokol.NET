# Build script for ozz-animation library on Windows
# Usage: .\build-ozz-animation-windows.ps1 [architecture] [build_type]
# Example: .\build-ozz-animation-windows.ps1 x64 Release
# Example: .\build-ozz-animation-windows.ps1 x86 Debug

param(
    [string]$Architecture = "x64",
    [string]$BuildType = "Release"
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SokolCharpRoot = Split-Path -Parent $ScriptDir
$OzzDir = Join-Path $SokolCharpRoot "ext\ozz-animation"
$BuildDir = Join-Path $OzzDir "build-vs2022-windows"

Write-Host "==========================================" -ForegroundColor Green
Write-Host "Building ozz-animation for Windows" -ForegroundColor Green
Write-Host "Architecture: $Architecture" -ForegroundColor Green
Write-Host "Build Type: $BuildType" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green

# Check if ozz-animation directory exists
if (-not (Test-Path $OzzDir)) {
    Write-Error "ozz-animation directory not found at $OzzDir. Please ensure the submodule is initialized: git submodule update --init --recursive"
    exit 1
}

# Clean up existing build directory for a fresh build
Write-Host "Cleaning build directory: $BuildDir"
if (Test-Path $BuildDir) {
    Remove-Item -Recurse -Force $BuildDir
}

# Create build directory
New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null
Set-Location $BuildDir

# Configure with CMake
$Generator = "Visual Studio 17 2022"
$Platform = if ($Architecture -eq "x86") { "Win32" } else { $Architecture }

cmake .. `
    -G $Generator `
    -A $Platform `
    -DCMAKE_BUILD_TYPE=$BuildType `
    -Dozz_build_samples=OFF `
    -Dozz_build_howtos=OFF `
    -Dozz_build_tests=OFF `
    -Dozz_build_tools=OFF `
    -DBUILD_SHARED_LIBS=OFF

if ($LASTEXITCODE -ne 0) {
    Write-Error "CMake configuration failed"
    exit 1
}

# Build
cmake --build . --config $BuildType

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

# Create destination directory
$DestDir = Join-Path $OzzDir "bin\windows\$Architecture\$($BuildType.ToLower())"
New-Item -ItemType Directory -Force -Path $DestDir | Out-Null

# Copy libraries to destination
Write-Host "Copying libraries to $DestDir..."
# ozz uses postfix naming: _r for release, _d for debug
$Postfix = ""
if ($BuildType -eq "Release") {
    $Postfix = "_r"
} elseif ($BuildType -eq "Debug") {
    $Postfix = "_d"
}

$AnimRuntimePath = Join-Path $BuildDir "src\animation\runtime\$BuildType"
$BasePath = Join-Path $BuildDir "src\base\$BuildType"  
$GeometryPath = Join-Path $BuildDir "src\geometry\runtime\$BuildType"
$AnimOfflinePath = Join-Path $BuildDir "src\animation\offline\$BuildType"
$OptionsPath = Join-Path $BuildDir "src\options\$BuildType"

Copy-Item "$AnimRuntimePath\ozz_animation$Postfix.lib" "$DestDir\ozz_animation.lib" -ErrorAction SilentlyContinue
Copy-Item "$BasePath\ozz_base$Postfix.lib" "$DestDir\ozz_base.lib" -ErrorAction SilentlyContinue
Copy-Item "$GeometryPath\ozz_geometry$Postfix.lib" "$DestDir\ozz_geometry.lib" -ErrorAction SilentlyContinue
Copy-Item "$AnimOfflinePath\ozz_animation_offline$Postfix.lib" "$DestDir\ozz_animation_offline.lib" -ErrorAction SilentlyContinue
Copy-Item "$OptionsPath\ozz_options$Postfix.lib" "$DestDir\ozz_options.lib" -ErrorAction SilentlyContinue

Write-Host "==========================================" -ForegroundColor Green
Write-Host "Build complete!" -ForegroundColor Green
Write-Host "Output: $DestDir" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green

# Verify the libraries were created
$AnimationLib = Join-Path $DestDir "ozz_animation.lib"
$BaseLib = Join-Path $DestDir "ozz_base.lib"

if ((Test-Path $AnimationLib) -and (Test-Path $BaseLib)) {
    Write-Host "✓ Successfully built ozz-animation libraries" -ForegroundColor Green
    Get-ChildItem "$DestDir\*.lib" | Format-Table Name, Length -AutoSize
} else {
    Write-Host "✗ Failed to build ozz-animation libraries" -ForegroundColor Red
    Write-Host "Available files in build directory:"
    Get-ChildItem $BuildDir -Recurse -Filter "*.lib" | ForEach-Object { Write-Host $_.FullName }
    exit 1
}