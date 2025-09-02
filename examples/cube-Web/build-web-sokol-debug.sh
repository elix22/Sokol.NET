cd ../../
# rm -rf build-emscripten
emcmake cmake -B build-emscripten -S ext/
cmake --build build-emscripten
rm -f examples/cube-Web/libs/sokol.a
mkdir -p examples/cube-Web/libs
cp -f build-emscripten/libsokol.a examples/cube-Web/libs/sokol.a
cd bindgen
python3 gen.py
cd ../examples/cube-Web
dotnet clean
dotnet build -f net8.0 -c Debug -p:DefineConstants="WEB" -o ./build
