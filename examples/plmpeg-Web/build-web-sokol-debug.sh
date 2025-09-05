cd ../../bindgen
python3 gen.py
cd ../examples/plmpeg-Web
cd ../../
emcmake cmake -B build-emscripten -S ext/
cmake --build build-emscripten
rm -f examples/plmpeg-Web/libs/sokol.a
mkdir -p examples/plmpeg-Web/libs
cp -f build-emscripten/libsokol.a examples/plmpeg-Web/libs/sokol.a
cd examples/plmpeg-Web
dotnet clean
dotnet msbuild -t:CompileShaders -p:DefineConstants="WEB"
dotnet build -f net8.0 -c Debug -p:DefineConstants="WEB" -o ./build
