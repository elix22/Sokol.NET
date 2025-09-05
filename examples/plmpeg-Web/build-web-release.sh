dotnet msbuild -t:CompileShaders -p:DefineConstants="WEB"
dotnet build -f net8.0 -c Release -p:DefineConstants="WEB" -o ./build