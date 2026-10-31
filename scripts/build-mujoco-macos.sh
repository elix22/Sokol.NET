#!/bin/bash
# Builds MuJoCo (ext/mujoco) as a shared library on macOS arm64 and copies it to
#   ext/mujoco/libs/macos/arm64/release/libmujoco.dylib   (install name @rpath/libmujoco.dylib)
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
cmake --build "$BUILD" --target mujoco --config Release -j "$(sysctl -n hw.ncpu)"

LIB="$(ls "$BUILD"/lib/libmujoco.*.*.*.dylib)"
rm -rf "$OUT"
mkdir -p "$OUT"
cp "$LIB" "$OUT/libmujoco.dylib"
chmod u+w "$OUT/libmujoco.dylib"
install_name_tool -id @rpath/libmujoco.dylib "$OUT/libmujoco.dylib"
codesign --force --sign - "$OUT/libmujoco.dylib" 2>/dev/null

# Fail if the library links anything but system libraries (dependencies must be static).
otool -L "$OUT/libmujoco.dylib" | tail -n +2 | awk '{print $1}' | while read -r dep; do
    case "$dep" in
        /usr/lib/*|/System/*|@rpath/libmujoco.dylib) ;;
        *) echo "error: libmujoco.dylib links $dep" >&2; exit 1 ;;
    esac
done
nm -gU "$OUT/libmujoco.dylib" | grep " _mj_step$" >/dev/null ||{ echo "error: mj_step not exported" >&2; exit 1; }

echo "========================================="
echo "MuJoCo build completed: $OUT"
ls -l "$OUT"
echo "========================================="
