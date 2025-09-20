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

# Remove and create build directory
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue build-emscripten
New-Item -ItemType Directory -Force build-emscripten | Out-Null

# Configure with emcmake
Write-Host "Configuring with emcmake..."
emcmake cmake -B build-emscripten -S ext/
if ($LASTEXITCODE -ne 0) {
    Write-Error "CMake configuration failed."
    exit 1
}

# Build
Write-Host "Building..."
cmake --build build-emscripten
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed."
    exit 1
}

Write-Host "Build completed successfully!"