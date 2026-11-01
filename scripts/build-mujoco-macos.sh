#!/bin/bash
# Builds MuJoCo (ext/mujoco) as a shared library on macOS arm64 and copies it to
#   ext/mujoco/libs/macos/arm64/release/libmujoco.dylib   (install name @rpath/libmujoco.dylib)
# plus MuJoCo's SDF plugin library (plugin/sdf: torus, gear, bowl, nut, bolt) beside it as
#   ext/mujoco/libs/macos/arm64/release/libsdf_plugin.dylib   (load it with mj_loadPluginLibrary)
# CMake fetches MuJoCo's own dependencies (lodepng, qhull, tinyxml2, ...) into the build folder
# on the first run, so that run needs network access.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MJ="$ROOT/ext/mujoco"
BUILD="$MJ/build/macos-arm64-release"
OUT="$MJ/libs/macos/arm64/release"

if [ "$(uname -s)" != "Darwin" ] || [ "$(uname -m)" != "arm64" ]; then
    echo "error: this script supports macOS arm64 only" >&2
    exit 1
fi
if [ ! -f "$MJ/CMakeLists.txt" ]; then
    echo "error: ext/mujoco missing - run: git submodule update --init ext/mujoco" >&2
    exit 1
fi

cmake -S "$MJ" -B "$BUILD" \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_OSX_ARCHITECTURES=arm64 \
    -DBUILD_SHARED_LIBS=ON \
    -DMUJOCO_BUILD_EXAMPLES=OFF \
    -DMUJOCO_BUILD_SIMULATE=OFF \
    -DMUJOCO_BUILD_TESTS=OFF \
    -DMUJOCO_TEST_PYTHON_UTIL=OFF
cmake --build "$BUILD" --target mujoco sdf_plugin --config Release -j "$(sysctl -n hw.ncpu)"

LIB="$(ls "$BUILD"/lib/libmujoco.*.*.*.dylib)"
rm -rf "$OUT"
mkdir -p "$OUT"
cp "$LIB" "$OUT/libmujoco.dylib"
chmod u+w "$OUT/libmujoco.dylib"
install_name_tool -id @rpath/libmujoco.dylib "$OUT/libmujoco.dylib"
codesign --force --sign - "$OUT/libmujoco.dylib" 2>/dev/null

# The plugin links libmujoco's versioned name through the build folder's rpath: link it to
# @rpath/libmujoco.dylib and point the rpath at its own folder only.
cp "$BUILD/lib/libsdf_plugin.dylib" "$OUT/libsdf_plugin.dylib"
chmod u+w "$OUT/libsdf_plugin.dylib"
install_name_tool -id @rpath/libsdf_plugin.dylib "$OUT/libsdf_plugin.dylib"
install_name_tool -change "@rpath/$(basename "$LIB")" @rpath/libmujoco.dylib "$OUT/libsdf_plugin.dylib"
otool -l "$OUT/libsdf_plugin.dylib" | awk '/cmd LC_RPATH/{getline; getline; print $2}' | while read -r rp; do
    install_name_tool -delete_rpath "$rp" "$OUT/libsdf_plugin.dylib"
done
install_name_tool -add_rpath @loader_path "$OUT/libsdf_plugin.dylib"
codesign --force --sign - "$OUT/libsdf_plugin.dylib" 2>/dev/null

# Fail if a library links anything but system libraries and libmujoco (dependencies must be static).
for lib in libmujoco.dylib libsdf_plugin.dylib; do
    otool -L "$OUT/$lib" | tail -n +2 | awk '{print $1}' | while read -r dep; do
        case "$dep" in
            /usr/lib/*|/System/*|@rpath/libmujoco.dylib|@rpath/$lib) ;;
            *) echo "error: $lib links $dep" >&2; exit 1 ;;
        esac
    done
done
nm -gU "$OUT/libmujoco.dylib" | grep " _mj_step$" >/dev/null ||{ echo "error: mj_step not exported" >&2; exit 1; }

echo "========================================="
echo "MuJoCo build completed: $OUT"
ls -l "$OUT"
echo "========================================="
