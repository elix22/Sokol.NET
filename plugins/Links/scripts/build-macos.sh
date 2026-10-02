#!/bin/bash
# Build libsokol_links.dylib for macOS — standalone Links plugin library.
# Output: plugins/Links/libs/macos/{arm64,X64}/release/libsokol_links.dylib
# Run from any directory; the script resolves the repo root automatically.
#
# Usage: ./plugins/Links/scripts/build-macos.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
PLUGIN_NATIVE="$REPO_ROOT/plugins/Links/native"

echo "========================================="
echo "sokol_links — macOS build"
echo "========================================="

for ARCH in arm64 x86_64; do
    ARCH_DIR="arm64"
    [ "$ARCH" = "x86_64" ] && ARCH_DIR="X64"

    echo ""
    echo "----- arch: $ARCH ($ARCH_DIR) -----"

    BUILD_DIR="$REPO_ROOT/build-sokol-links-macos-$ARCH"
    rm -rf "$BUILD_DIR"

    cmake -S "$PLUGIN_NATIVE" -B "$BUILD_DIR" \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_OSX_ARCHITECTURES="$ARCH"

    cmake --build "$BUILD_DIR" --config Release

    rm -rf "$BUILD_DIR"

    echo "  -> plugins/Links/libs/macos/$ARCH_DIR/release/libsokol_links.dylib"
done

echo ""
echo "========================================="
echo "sokol_links — macOS done!"
echo "========================================="
