#!/bin/bash
# Shared steps for the sokol_net build scripts (sourced, not run).
#   1. Mbed TLS (ext/mbedtls)              → static libs, configured with native/mbedtls_user_config.h
#   2. libdatachannel (ext/libdatachannel) → static libs (+ libjuice, usrsctp), with patches/*.patch applied
#      for the duration of the build and reverted afterwards (the submodule stays clean)
#   3. native/CMakeLists.txt               → the one shipped library (see that file)
# Build trees live outside the repo: $SOKOLNET_BUILD_DIR (default: $TMPDIR/sokol-net-build).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLUGIN_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO_ROOT="$(cd "$PLUGIN_ROOT/../.." && pwd)"
LDC_SRC="$REPO_ROOT/ext/libdatachannel"
MBED_SRC="$REPO_ROOT/ext/mbedtls"
NATIVE="$PLUGIN_ROOT/native"
BUILD_ROOT="${SOKOLNET_BUILD_DIR:-${TMPDIR:-/tmp}/sokol-net-build}"
JOBS="$( (sysctl -n hw.ncpu || nproc) 2>/dev/null )"

for d in "$LDC_SRC/deps/libjuice/CMakeLists.txt" "$LDC_SRC/deps/usrsctp/CMakeLists.txt" "$MBED_SRC/framework/CMakeLists.txt"; do
    [ -f "$d" ] || { echo "Missing $d — run: git submodule update --init --recursive ext/libdatachannel ext/mbedtls"; exit 1; }
done

# ── patches: apply now, revert on exit ───────────────────────────────────────────────────────────────────
_SOKOLNET_PATCHED=()
apply_patches() {
    for p in "$PLUGIN_ROOT"/patches/*.patch; do
        [ -e "$p" ] || continue
        if git -C "$LDC_SRC" apply --reverse --check "$p" 2>/dev/null; then
            echo "patch already applied: $(basename "$p")"
        else
            git -C "$LDC_SRC" apply "$p"
            _SOKOLNET_PATCHED+=("$p")
            echo "patch applied: $(basename "$p")"
        fi
    done
}
revert_patches() {
    local i
    for (( i=${#_SOKOLNET_PATCHED[@]}-1; i>=0; i-- )); do
        git -C "$LDC_SRC" apply --reverse "${_SOKOLNET_PATCHED[$i]}" && echo "patch reverted: $(basename "${_SOKOLNET_PATCHED[$i]}")"
    done
    _SOKOLNET_PATCHED=()
}
trap revert_patches EXIT

# build_deps <build-dir> <extra cmake args…>   →  <build-dir>/deps/lib*.a
build_deps() {
    local B="$1"; shift
    local CFG="$NATIVE/mbedtls_user_config.h"
    rm -rf "$B"
    cmake -S "$MBED_SRC" -B "$B/mbedtls" -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_INSTALL_PREFIX="$B/mbedtls-install" \
        -DMBEDTLS_USER_CONFIG_FILE="$CFG" \
        -DENABLE_TESTING=OFF -DENABLE_PROGRAMS=OFF \
        -DUSE_STATIC_MBEDTLS_LIBRARY=ON -DUSE_SHARED_MBEDTLS_LIBRARY=OFF \
        -DMBEDTLS_FATAL_WARNINGS=OFF -DCMAKE_POSITION_INDEPENDENT_CODE=ON \
        -DCMAKE_C_FLAGS="-ffunction-sections -fdata-sections" "$@"
    cmake --build "$B/mbedtls" -j "$JOBS"
    cmake --install "$B/mbedtls" >/dev/null

    # libdatachannel's FindMbedTLS searches pkg-config and the system include dirs, and the NDK / iOS
    # toolchains restrict find_* to their sysroot → pass the locations explicitly.
    local MB="$B/mbedtls-install"
    cmake -S "$LDC_SRC" -B "$B/ldc" -DCMAKE_BUILD_TYPE=Release \
        -DUSE_MBEDTLS=ON -DNO_MEDIA=ON -DNO_WEBSOCKET=OFF -DNO_EXAMPLES=ON -DNO_TESTS=ON \
        -DCMAKE_DISABLE_FIND_PACKAGE_PkgConfig=ON \
        -DCMAKE_PROJECT_INCLUDE="$NATIVE/ldc_project_include.cmake" \
        -DSOKOLNET_MBEDTLS_USER_CONFIG="$CFG" \
        -DMbedTLS_INCLUDE_DIR="$MB/include" \
        -DMbedTLS_LIBRARY="$MB/lib/libmbedtls.a" \
        -DMbedCrypto_LIBRARY="$MB/lib/libmbedcrypto.a" \
        -DMbedX509_LIBRARY="$MB/lib/libmbedx509.a" \
        -DCMAKE_POSITION_INDEPENDENT_CODE=ON \
        -DCMAKE_C_FLAGS="-ffunction-sections -fdata-sections" \
        -DCMAKE_CXX_FLAGS="-ffunction-sections -fdata-sections" "$@"
    cmake --build "$B/ldc" --target datachannel-static -j "$JOBS"

    mkdir -p "$B/deps"
    cp "$B/ldc/libdatachannel-static.a" "$B/deps/"
    cp "$(find "$B/ldc/deps/libjuice" -name 'libjuice-static.a' | head -1)" "$B/deps/"
    cp "$(find "$B/ldc/deps/usrsctp" -name 'libusrsctp.a' | head -1)" "$B/deps/"
    cp "$MB"/lib/libmbedtls.a "$MB"/lib/libmbedx509.a "$MB"/lib/libmbedcrypto.a "$B/deps/"
}

# build_plugin <build-dir> <extra cmake args…>   →  plugins/Net/libs/…
build_plugin() {
    local B="$1"; shift
    cmake -S "$NATIVE" -B "$B/plugin" -DCMAKE_BUILD_TYPE=Release -DSOKOLNET_DEPS_DIR="$B/deps" "$@"
    cmake --build "$B/plugin" -j "$JOBS"
}
