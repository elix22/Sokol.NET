# Build script for Emscripten on Windows using PowerShell
# Uses local emsdk submodule

# Set Emscripten version
$EMSCRIPTEN_VERSION = "3.1.34"

# Path to local emsdk
$EMSDK_PATH = ".\tools\emsdk\emsdk"

# Check if local emsdk exists
if (-not (Test-Path $EMSDK_PATH)) {
    Write-Error "Local emsdk not found at $EMSDK_PATH. Ensure the submodule is initialized: git submodule update --init --recursive"
    exit 1
}

# Activate Emscripten SDK with the specified version
Write-Host "Installing Emscripten SDK version $EMSCRIPTEN_VERSION..."
& $EMSDK_PATH install $EMSCRIPTEN_VERSION
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to install Emscripten SDK version $EMSCRIPTEN_VERSION."
    exit 1
}

Write-Host "Activating Emscripten SDK version $EMSCRIPTEN_VERSION..."
& $EMSDK_PATH activate $EMSCRIPTEN_VERSION
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to activate Emscripten SDK version $EMSCRIPTEN_VERSION."
    exit 1
}

# Set up environment variables for Emscripten
& ".\tools\emsdk\emsdk_env.bat"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to set up Emscripten environment."
    exit 1
}

# Clean up any existing build directories
Write-Host "Cleaning up build directories..." -ForegroundColor Cyan
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue build-emscripten-debug
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue build-emscripten-release

# Build Debug configuration
Write-Host "=========================================" -ForegroundColor Yellow
Write-Host "Building Debug configuration..." -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Yellow
New-Item -ItemType Directory -Force build-emscripten-debug | Out-Null
emcmake cmake -B build-emscripten-debug -S ext/ -DCMAKE_BUILD_TYPE=Debug
if ($LASTEXITCODE -ne 0) {
    Write-Error "CMake configuration failed for Debug."
    exit 1
}
cmake --build build-emscripten-debug
if ($LASTEXITCODE -ne 0) {
    Write-Error "Debug build failed."
    exit 1
}

# Build Release configuration
Write-Host "=========================================" -ForegroundColor Yellow
Write-Host "Building Release configuration..." -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Yellow
New-Item -ItemType Directory -Force build-emscripten-release | Out-Null
emcmake cmake -B build-emscripten-release -S ext/ -DCMAKE_BUILD_TYPE=Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "CMake configuration failed for Release."
    exit 1
}
cmake --build build-emscripten-release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Release build failed."
    exit 1
}

# Clean up build directories
Write-Host "Cleaning up build directories..." -ForegroundColor Cyan
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue build-emscripten-debug
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue build-emscripten-release

Write-Host "=========================================" -ForegroundColor Yellow
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Debug build: libs/emscripten/wasm32/debug/sokol.a" -ForegroundColor Green
Write-Host "Release build: libs/emscripten/wasm32/release/sokol.a" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Yellow