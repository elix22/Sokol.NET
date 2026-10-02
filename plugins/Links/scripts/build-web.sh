#!/bin/bash
# Build sokol_links.a for Web (Emscripten) — standalone Links plugin library.
# Output: plugins/Links/libs/emscripten/x86/release/sokol_links.a
#
# Uses the repo-root emsdk submodule pinned to the Emscripten version .NET's WASM
# toolchain requires, or an emcc already on PATH (CI: setup-emsdk with the same pin).
#
# Usage: ./plugins/Links/scripts/build-web.sh [Release|Debug]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
PLUGIN_NATIVE="$REPO_ROOT/plugins/Links/native"

BUILD_TYPE="${1:-Release}"
EMSCRIPTEN_VERSION="3.1.56"

EMSDK_PATH="$REPO_ROOT/tools/emsdk/emsdk"
if command -v emcc >/dev/null 2>&1; then
    echo "Using emcc from PATH: $(emcc --version | head -1)"
elif [ -f "$EMSDK_PATH" ]; then
    chmod +x "$EMSDK_PATH" 2>/dev/null || true
    echo "Activating Emscripten $EMSCRIPTEN_VERSION via local emsdk..."
    "$EMSDK_PATH" install "$EMSCRIPTEN_VERSION"
    "$EMSDK_PATH" activate "$EMSCRIPTEN_VERSION"
    # shellcheck disable=SC1091
    source "$REPO_ROOT/tools/emsdk/emsdk_env.sh"
else
    echo "Error: no Emscripten found. Provide tools/emsdk or put emcc $EMSCRIPTEN_VERSION on PATH."
    exit 1
fi

echo "========================================="
echo "sokol_links — Web ($BUILD_TYPE)"
echo "========================================="

BUILD_DIR="$REPO_ROOT/build-sokol-links-web"
rm -rf "$BUILD_DIR"
emcmake cmake -S "$PLUGIN_NATIVE" -B "$BUILD_DIR" -DCMAKE_BUILD_TYPE="$BUILD_TYPE"
cmake --build "$BUILD_DIR"
rm -rf "$BUILD_DIR"

echo "  -> plugins/Links/libs/emscripten/x86/$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')/sokol_links.a"
