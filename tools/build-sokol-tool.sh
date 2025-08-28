cd sokol-tools
git submodule init  
git submodule update
./fips set config osx-xcode-release
./fips clean
./fips build
cd ..
mkdir -p bin
cp ./fips-deploy/sokol-tools/osx-xcode-release/sokol-shdc ./bin/sokol-shdc