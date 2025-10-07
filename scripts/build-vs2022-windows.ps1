# Get architecture and map to CMake platform
$ARCH = $env:PROCESSOR_ARCHITECTURE
if ($ARCH -eq "AMD64") {
    $Platform = "x64"
} elseif ($ARCH -eq "x86") {
    $Platform = "Win32"
} elseif ($ARCH -eq "ARM64") {
    $Platform = "ARM64"
} else {
    $Platform = "x64"  # default
}

# Remove and create build directory
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue build-vs2022-windows
New-Item -ItemType Directory -Force build-vs2022-windows | Out-Null
Set-Location build-vs2022-windows

# Configure
cmake ../ext -G "Visual Studio 17 2022" -A $Platform

# Build Debug
Write-Host "Building Debug configuration..." -ForegroundColor Cyan
cmake --build . --config Debug

# Build Release
Write-Host "Building Release configuration..." -ForegroundColor Cyan
cmake --build . --config Release

# Create directories
New-Item -ItemType Directory -Force "../libs/windows/$Platform/debug" | Out-Null
New-Item -ItemType Directory -Force "../libs/windows/$Platform/release" | Out-Null

# Copy Debug builds
Copy-Item -Force Debug/sokol.dll "../libs/windows/$Platform/debug/sokol.dll"
Copy-Item -Force Debug/sokol.lib "../libs/windows/$Platform/debug/sokol.lib"

# Copy Release builds
Copy-Item -Force Release/sokol.dll "../libs/windows/$Platform/release/sokol.dll"
Copy-Item -Force Release/sokol.lib "../libs/windows/$Platform/release/sokol.lib"

Write-Host "Debug build copied to: ../libs/windows/$Platform/debug/sokol.dll" -ForegroundColor Green
Write-Host "Release build copied to: ../libs/windows/$Platform/release/sokol.dll" -ForegroundColor Green

# Clean up
Set-Location ..
Remove-Item -Recurse -Force build-vs2022-windows