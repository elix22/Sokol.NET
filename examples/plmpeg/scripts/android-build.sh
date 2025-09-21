#!/bin/bash

# Universal Android Build Script for sokol-charp
# Works with Visual Studio Code, Rider, Visual Studio 2022, and command line

set -e

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$PROJECT_DIR"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_step() {
    echo -e "${BLUE}[STEP]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

# Show usage
show_usage() {
    echo "Usage: $0 [build-release|build-debug|install-release|install-debug|help]"
    echo ""
    echo "Commands:"
    echo "  build-release    - Build Android APK (Release)"
    echo "  build-debug      - Build Android APK (Debug)"
    echo "  install-release  - Build and Install Android APK (Release)"
    echo "  install-debug    - Build and Install Android APK (Debug)"
    echo "  help            - Show this help message"
    echo ""
    echo "Environment Variables:"
    echo "  ANDROID_DEVICE_ID    - Specific Android device ID for installation"
    echo "  ANDROID_ORIENTATION  - App orientation (landscape/portrait, default: landscape)"
    echo ""
}

# Check if dotnet is available
check_dotnet() {
    if ! command -v dotnet &> /dev/null; then
        print_error "dotnet CLI is not installed or not in PATH"
        exit 1
    fi
}

# Execute MSBuild target
execute_target() {
    local target=$1
    local description=$2
    
    print_step "$description"
    
    if dotnet msbuild plmpeg.csproj -t:"$target"; then
        print_success "$description completed successfully"
    else
        print_error "$description failed"
        exit 1
    fi
}

# Main execution
main() {
    check_dotnet
    
    case "${1:-help}" in
        "build-release")
            execute_target "BuildAndroid" "Building Android APK (Release)"
            ;;
        "build-debug")
            execute_target "BuildAndroidDebug" "Building Android APK (Debug)"
            ;;
        "install-release")
            execute_target "BuildAndroidInstall" "Building and Installing Android APK (Release)"
            ;;
        "install-debug")
            execute_target "BuildAndroidDebugInstall" "Building and Installing Android APK (Debug)"
            ;;
        "help"|"--help"|"-h")
            show_usage
            ;;
        *)
            print_error "Unknown command: $1"
            echo ""
            show_usage
            exit 1
            ;;
    esac
}

main "$@"