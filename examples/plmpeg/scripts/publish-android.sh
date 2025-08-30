

# compile shaders first
 dotnet msbuild -t:CompileShaders -p:DefineConstants="__ANDROID__"

dotnet publish -r linux-bionic-arm64 -p:BuildAsLibrary=true -p:DisableUnsupportedError=true -p:PublishAotUsingRuntimePack=true -p:RemoveSections=true -p:DefineConstants="__ANDROID__"
dotnet publish -r linux-bionic-arm -p:BuildAsLibrary=true -p:DisableUnsupportedError=true -p:PublishAotUsingRuntimePack=true -p:RemoveSections=true -p:DefineConstants="__ANDROID__"
dotnet publish -r linux-bionic-x64 -p:BuildAsLibrary=true -p:DisableUnsupportedError=true -p:PublishAotUsingRuntimePack=true -p:RemoveSections=true -p:DefineConstants="__ANDROID__"

cd Android/native-activity
./gradlew assembleDebug -PcmakeArgs="-DAPP_NAME=dyntex"