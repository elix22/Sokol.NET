# Universal Android Build Script for sokol-charp (PowerShell)
# Works with Visual Studio Code, Rider, Visual Studio 2022, and command line

param(
    [Parameter(Position=0)]
    [ValidateSet("build-release", "build-debug", "install-release", "install-debug", "help")]
    [string]$Command = "help"
)

# Get script directory
$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ProjectDir

# Function to print colored output
function Write-Step {
    param([string]$Message)
    Write-Host "[STEP] $Message" -ForegroundColor Blue
}

function Write-Success {
    param([string]$Message)
    Write-Host "[SUCCESS] $Message" -ForegroundColor Green
}

function Write-Error {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Write-Warning {
    param([string]$Message)
    Write-Host "[WARNING] $Message" -ForegroundColor Yellow
}

# Show usage
function Show-Usage {
    Write-Host "Usage: .\android-build.ps1 [build-release|build-debug|install-release|install-debug|help]"
    Write-Host ""
    Write-Host "Commands:"
    Write-Host "  build-release    - Build Android APK (Release)"
    Write-Host "  build-debug      - Build Android APK (Debug)"
    Write-Host "  install-release  - Build and Install Android APK (Release)"
    Write-Host "  install-debug    - Build and Install Android APK (Debug)"
    Write-Host "  help            - Show this help message"
    Write-Host ""
    Write-Host "Environment Variables:"
    Write-Host "  ANDROID_DEVICE_ID    - Specific Android device ID for installation"
    Write-Host "  ANDROID_ORIENTATION  - App orientation (landscape/portrait, default: landscape)"
    Write-Host ""
}

# Check if dotnet is available
function Test-DotNet {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Error "dotnet CLI is not installed or not in PATH"
        exit 1
    }
}

# Execute MSBuild target
function Invoke-Target {
    param(
        [string]$Target,
        [string]$Description
    )
    
    Write-Step $Description
    
    $result = & dotnet msbuild cimgui.csproj -t:$Target
    if ($LASTEXITCODE -eq 0) {
        Write-Success "$Description completed successfully"
    } else {
        Write-Error "$Description failed"
        exit 1
    }
}

# Main execution
Test-DotNet

switch ($Command) {
    "build-release" {
        Invoke-Target "BuildAndroid" "Building Android APK (Release)"
    }
    "build-debug" {
        Invoke-Target "BuildAndroidDebug" "Building Android APK (Debug)"
    }
    "install-release" {
        Invoke-Target "BuildAndroidInstall" "Building and Installing Android APK (Release)"
    }
    "install-debug" {
        Invoke-Target "BuildAndroidDebugInstall" "Building and Installing Android APK (Debug)"
    }
    "help" {
        Show-Usage
    }
    default {
        Write-Error "Unknown command: $Command"
        Write-Host ""
        Show-Usage
        exit 1
    }
}