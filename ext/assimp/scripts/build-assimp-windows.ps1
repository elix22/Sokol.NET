# Build script for assimp library on Windows
# Usage: .\build-assimp-windows.ps1 [-Architecture <arch>] [-BuildType <type>]
# Example: .\build-assimp-windows.ps1 -Architecture x64 -BuildType Release
# Example: .\build-assimp-windows.ps1 -Architecture Win32 -BuildType Debug
# Architectures: x64, Win32, ARM64

param(
    [string]$Architecture = "x64",
    [string]$BuildType = "Release"
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$AssimpDir = Join-Path $ScriptDir ".."
$BuildDir = Join-Path $AssimpDir "build-windows-$Architecture"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Building assimp for Windows" -ForegroundColor Cyan
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
    -DCMAKE_BUILD_TYPE="$BuildType" `
    -DBUILD_SHARED_LIBS=ON `
    -DASSIMP_BUILD_TESTS=OFF `
    -DASSIMP_BUILD_ASSIMP_TOOLS=OFF `
    -DASSIMP_BUILD_SAMPLES=OFF `
    -DASSIMP_BUILD_ZLIB=OFF `
    -DASSIMP_NO_EXPORT=ON `
    -DASSIMP_BUILD_ALL_IMPORTERS_BY_DEFAULT=OFF `
    -DASSIMP_BUILD_OBJ_IMPORTER=ON `
    -DASSIMP_BUILD_FBX_IMPORTER=ON `
    -DASSIMP_BUILD_GLTF_IMPORTER=ON `
    -DASSIMP_BUILD_COLLADA_IMPORTER=ON

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

# Create destination directory
$BuildTypeLower = $BuildType.ToLower()
$DestDir = Join-Path $AssimpDir "libs\windows\$Architecture\$BuildTypeLower"
New-Item -ItemType Directory -Force -Path $DestDir | Out-Null

# Copy library to destination
Write-Host "Copying library to $DestDir..." -ForegroundColor Yellow
$DllPath = Join-Path $BuildDir "bin\$BuildType\assimp.dll"

if (Test-Path $DllPath) {
    Copy-Item "$DllPath" "$DestDir\"
    # Also copy lib file if it exists
    $LibPath = Join-Path $BuildDir "lib\$BuildType\assimp.lib"
    if (Test-Path $LibPath) {
        Copy-Item "$LibPath" "$DestDir\"
    }
} else {
    # Try alternative location
    $DllPath = Join-Path $BuildDir "$BuildType\bin\assimp.dll"
    if (Test-Path $DllPath) {
        Copy-Item "$DllPath" "$DestDir\"
        $LibPath = Join-Path $BuildDir "$BuildType\lib\assimp.lib"
        if (Test-Path $LibPath) {
            Copy-Item "$LibPath" "$DestDir\"
        }
    } else {
        Write-Host "✗ assimp.dll not found" -ForegroundColor Red
        Write-Host "Searched paths:" -ForegroundColor Yellow
        Write-Host "  - $BuildDir\bin\$BuildType\assimp.dll"
        Write-Host "  - $BuildDir\$BuildType\bin\assimp.dll"
        exit 1
    }
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Build complete!" -ForegroundColor Green
Write-Host "Output: $DestDir" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan

# Verify the library was created
$FinalDllPath = Join-Path $DestDir "assimp.dll"
if (Test-Path $FinalDllPath) {
    Write-Host "✓ Successfully built assimp.dll" -ForegroundColor Green
    Get-ChildItem $DestDir | Format-Table Name, Length, LastWriteTime
} else {
    Write-Host "✗ Failed to build assimp.dll" -ForegroundColor Red
    exit 1
}
