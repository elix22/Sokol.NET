#!/bin/bash
# Build sokol_notifications.framework for iOS — standalone Notifications plugin library.
# Output: libs/ios/<target>/{debug,release}/sokol_notifications.framework
# Run from any directory; the script resolves the repo root automatically.
#
# Usage: ./plugins/Notifications/scripts/build-ios.sh [device|simulator-arm64|simulator-x64|all]
# Default: all
#
# The framework compiles an Objective-C bridge (UNUserNotificationCenter) plus the
# shared C event queue. Deployment target 15.0. No link against the main sokol
# framework is required.

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
PLUGIN_NATIVE="$REPO_ROOT/plugins/Notifications/native"

BUILD_TARGET="${1:-all}"

build_for_target() {
    local TARGET=$1
    local ARCH=$2
    local SDK=$3
    local OUTPUT_DIR=$4

    echo "========================================="
    echo "sokol_notifications — iOS $TARGET ($ARCH)"
    echo "========================================="

    local BUILD_DIR="$REPO_ROOT/build-sokol-notifications-ios-$TARGET"
    rm -rf "$BUILD_DIR"

    cmake -S "$PLUGIN_NATIVE" -B "$BUILD_DIR" -G Xcode \
        -DCMAKE_SYSTEM_NAME=iOS \
        -DCMAKE_OSX_DEPLOYMENT_TARGET=15.0 \
        -DCMAKE_OSX_ARCHITECTURES="$ARCH" \
        -DCMAKE_OSX_SYSROOT="$SDK"

    echo "Building Release..."
    cmake --build "$BUILD_DIR" --config Release

    echo "Building Debug..."
    cmake --build "$BUILD_DIR" --config Debug

    rm -rf "$BUILD_DIR"

    echo "Done — $TARGET"
    echo "  Release: plugins/Notifications/libs/ios/$OUTPUT_DIR/release/sokol_notifications.framework"
    echo "  Debug:   plugins/Notifications/libs/ios/$OUTPUT_DIR/debug/sokol_notifications.framework"
}

case "$BUILD_TARGET" in
    device)
        build_for_target "device" "arm64" "iphoneos" "arm64"
        ;;
    simulator-arm64)
        build_for_target "simulator-arm64" "arm64" "iphonesimulator" "simulator-arm64"
        ;;
    simulator-x64)
        build_for_target "simulator-x64" "x86_64" "iphonesimulator" "simulator-x64"
        ;;
    all)
        build_for_target "device"          "arm64"  "iphoneos"        "arm64"
        build_for_target "simulator-arm64" "arm64"  "iphonesimulator" "simulator-arm64"
        build_for_target "simulator-x64"   "x86_64" "iphonesimulator" "simulator-x64"
        echo "========================================="
        echo "sokol_notifications — iOS all targets done!"
        echo "  Device:              plugins/Notifications/libs/ios/arm64/{debug,release}/sokol_notifications.framework"
        echo "  Simulator (arm64):   plugins/Notifications/libs/ios/simulator-arm64/{debug,release}/sokol_notifications.framework"
        echo "  Simulator (x86_64):  plugins/Notifications/libs/ios/simulator-x64/{debug,release}/sokol_notifications.framework"
        echo "========================================="
        ;;
    *)
        echo "Error: unknown target '$BUILD_TARGET'"
        echo "Usage: $0 [device|simulator-arm64|simulator-x64|all]"
        exit 1
        ;;
esac

echo ""
echo "Next: embed sokol_notifications.framework in the app bundle alongside sokol.framework."
