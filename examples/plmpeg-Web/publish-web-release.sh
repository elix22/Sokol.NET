dotnet msbuild -t:CompileShaders -p:DefineConstants="WEB"
dotnet publish -f net8.0 -c Release -p:DefineConstants="WEB" -o ./build