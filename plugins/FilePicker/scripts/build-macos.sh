#!/bin/bash
# Build libsokol_filepicker.dylib for macOS — standalone FilePicker plugin library.
# Output: plugins/FilePicker/libs/macos/{arm64,X64}/release/libsokol_filepicker.dylib
# Run from any directory; the script resolves the repo root automatically.
#
# Usage: ./plugins/FilePicker/scripts/build-macos.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
PLUGIN_NATIVE="$REPO_ROOT/plugins/FilePicker/native"

echo "========================================="
echo "sokol_filepicker — macOS build"
echo "========================================="

for ARCH in arm64 x86_64; do
    ARCH_DIR="arm64"
    [ "$ARCH" = "x86_64" ] && ARCH_DIR="X64"

    echo ""
    echo "----- arch: $ARCH ($ARCH_DIR) -----"

    BUILD_DIR="$REPO_ROOT/build-sokol-filepicker-macos-$ARCH"
    rm -rf "$BUILD_DIR"

    cmake -S "$PLUGIN_NATIVE" -B "$BUILD_DIR" \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_OSX_ARCHITECTURES="$ARCH" \
        -DCMAKE_OSX_DEPLOYMENT_TARGET=11.0

    cmake --build "$BUILD_DIR" --config Release

    rm -rf "$BUILD_DIR"

    echo "  -> plugins/FilePicker/libs/macos/$ARCH_DIR/release/libsokol_filepicker.dylib"
done

echo ""
echo "========================================="
echo "sokol_filepicker — macOS done!"
echo "========================================="
