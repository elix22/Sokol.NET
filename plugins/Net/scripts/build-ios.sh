#!/bin/bash
# Build sokol_net.framework for iOS (deployment target 15.0, the app's floor).
# Output: plugins/Net/libs/ios/<arm64|simulator-arm64|simulator-x64>/release/sokol_net.framework
# Usage:  ./plugins/Net/scripts/build-ios.sh [device|simulator-arm64|simulator-x64|all]   (default: all)
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
apply_patches

build_target() {
    local TARGET=$1 ARCH=$2 SDK=$3
    echo "===== sokol_net — iOS $TARGET ($ARCH) ====="
    local B="$BUILD_ROOT/ios-$TARGET"
    local ARGS=( -DCMAKE_SYSTEM_NAME=iOS -DCMAKE_OSX_ARCHITECTURES="$ARCH"
                 -DCMAKE_OSX_SYSROOT="$SDK" -DCMAKE_OSX_DEPLOYMENT_TARGET=15.0 )
    build_deps "$B" "${ARGS[@]}"
    build_plugin "$B" "${ARGS[@]}"
}

case "${1:-all}" in
    device)          build_target device          arm64  iphoneos ;;
    simulator-arm64) build_target simulator-arm64 arm64  iphonesimulator ;;
    simulator-x64)   build_target simulator-x64   x86_64 iphonesimulator ;;
    all)             build_target device arm64 iphoneos
                     build_target simulator-arm64 arm64 iphonesimulator
                     build_target simulator-x64 x86_64 iphonesimulator ;;
    *) echo "Usage: $0 [device|simulator-arm64|simulator-x64|all]"; exit 1 ;;
esac
