cd ../../bindgen
python3 gen.py
cd ../examples/loadpng-web
cd ../../
emcmake cmake -B build-emscripten -S ext/
cmake --build build-emscripten
rm -f examples/loadpng-web/libs/sokol.a
mkdir -p examples/loadpng-web/libs
cp -f build-emscripten/sokol.a examples/loadpng-web/libs/sokol.a
cd examples/loadpng-web
dotnet clean
dotnet msbuild -t:CompileShaders -p:DefineConstants="WEB"
dotnet publish -f net8.0 -c Debug -p:DefineConstants="WEB" -o ./build
