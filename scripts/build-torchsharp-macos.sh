#!/bin/bash
# Builds TorchSharp (ext/TorchSharp) from source on macOS arm64 and collects the managed
# assembly, LibTorchSharp and the prebuilt libtorch dylibs (CPU + MPS backend) into
#   ext/TorchSharp/libs/macos/arm64/release/
# TorchSharp's own build downloads libtorch-macos-arm64-<ver>.zip (~200 MB, cached in
# ext/TorchSharp/bin/downloads) and compiles LibTorchSharp with CMake.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TS="$ROOT/ext/TorchSharp"
OUT="$TS/libs/macos/arm64/release"

if [ "$(uname -s)" != "Darwin" ] || [ "$(uname -m)" != "arm64" ]; then
    echo "error: this script supports macOS arm64 only" >&2
    exit 1
fi
if [ ! -f "$TS/src/TorchSharp/TorchSharp.csproj" ]; then
    echo "error: ext/TorchSharp missing - run: git submodule update --init ext/TorchSharp" >&2
    exit 1
fi

# Keep the libtorch download inside the submodule's ignored bin/ (the default is ext/,
# which would leave an untracked folder in the Sokol.NET tree).
dotnet build "$TS/src/TorchSharp/TorchSharp.csproj" -c Release "-p:MassiveDownloadRoot=$TS/bin/downloads/"

LIBTORCH="$(ls -d "$TS"/bin/obj/AnyCPU.Release/libtorch-cpu/libtorch-macos-arm64-*/libtorch/lib)"
rm -rf "$OUT"
mkdir -p "$OUT"
cp "$TS/bin/AnyCPU.Release/TorchSharp/net8.0/TorchSharp.dll" "$OUT/"
cp "$TS/bin/arm64.Release/Native/libLibTorchSharp.dylib" "$OUT/"
for lib in libtorch libtorch_cpu libtorch_global_deps libc10 libshm libomp; do
    cp "$LIBTORCH/$lib.dylib" "$OUT/"
done
chmod u+w "$OUT"/*.dylib

# Upstream libtorch links libomp by an absolute Homebrew path and carries a CI rpath.
# Point it at the bundled copy so the folder is self-contained, then re-sign (ad hoc).
install_name_tool -id @rpath/libomp.dylib "$OUT/libomp.dylib"
install_name_tool -change /opt/homebrew/opt/libomp/lib/libomp.dylib @rpath/libomp.dylib "$OUT/libtorch_cpu.dylib"
for lib in libtorch libtorch_cpu libtorch_global_deps libc10 libshm; do
    if ! otool -l "$OUT/$lib.dylib" | grep -q "path @loader_path "; then
        install_name_tool -add_rpath @loader_path "$OUT/$lib.dylib"
    fi
done
for f in "$OUT"/*.dylib; do
    codesign --force --sign - "$f" 2>/dev/null
done

# Fail if any non-system dependency would not resolve from this folder.
for f in "$OUT"/*.dylib; do
    otool -L "$f" | tail -n +2 | awk '{print $1}' | while read -r dep; do
        case "$dep" in
            /usr/lib/*|/System/*) ;;
            @rpath/*) [ -f "$OUT/${dep#@rpath/}" ] || { echo "error: $(basename "$f") needs $dep" >&2; exit 1; } ;;
            *) echo "error: $(basename "$f") links absolute path $dep" >&2; exit 1 ;;
        esac
    done
done

echo "========================================="
echo "TorchSharp build completed: $OUT"
ls -1 "$OUT"
echo "========================================="
