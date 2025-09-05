dotnet msbuild -t:CompileShaders -p:DefineConstants="WEB"
dotnet build -f net8.0 -c Debug -p:DefineConstants="WEB" -o ./build