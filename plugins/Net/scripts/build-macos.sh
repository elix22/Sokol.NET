#!/bin/bash
# Build libsokol_net.dylib for macOS.
# Output: plugins/Net/libs/macos/<arm64|X64>/release/libsokol_net.dylib
# Usage:  ./plugins/Net/scripts/build-macos.sh [arm64|x86_64|all]   (default: all)
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
apply_patches

build_arch() {
    local ARCH=$1
    echo "===== sokol_net — macOS $ARCH ====="
    local B="$BUILD_ROOT/macos-$ARCH"
    local ARGS=( -DCMAKE_OSX_ARCHITECTURES="$ARCH" -DCMAKE_OSX_DEPLOYMENT_TARGET=11.0 )
    build_deps "$B" "${ARGS[@]}"
    build_plugin "$B" "${ARGS[@]}"
}

case "${1:-all}" in
    arm64|x86_64) build_arch "$1" ;;
    all)          build_arch arm64; build_arch x86_64 ;;
    *) echo "Usage: $0 [arm64|x86_64|all]"; exit 1 ;;
esac
