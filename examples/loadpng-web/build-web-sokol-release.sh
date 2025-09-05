cd ../../
# rm -rf build-emscripten-release
emcmake cmake -B build-emscripten-release -S ext/ -DCMAKE_BUILD_TYPE=Release
cmake --build build-emscripten-release  --config Release
rm -f examples/loadpng-web/libs/sokol.a
cp -f build-emscripten-release/libsokol.a examples/loadpng-web/libs/sokol.a
cd bindgen
python3 gen.py
cd ../examples/loadpng-web
dotnet clean
dotnet msbuild -t:CompileShaders -p:DefineConstants="WEB"
dotnet build -f net8.0 -c Release -p:DefineConstants="WEB" -o ./build
