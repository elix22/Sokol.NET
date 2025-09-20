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

# Configure and build
cmake ../ext -G "Visual Studio 17 2022" -A $Platform
cmake --build . --config Release

# Create directory
New-Item -ItemType Directory -Force "../libs/windows/$Platform" | Out-Null
Copy-Item -Force Release/sokol.dll "../libs/windows/$Platform/sokol.dll"
Copy-Item -Force Release/sokol.lib "../libs/windows/$Platform/sokol.lib"

# Clean up
Set-Location ..
Remove-Item -Recurse -Force build-vs2022-windows