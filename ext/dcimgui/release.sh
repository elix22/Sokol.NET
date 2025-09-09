set -e
echo "Version: $1"

# main branch
git clone --depth 1 --branch $1 https://github.com/ocornut/imgui
git clone --depth 1 --branch $1-docking https://github.com/ocornut/imgui imgui-docking
git clone --depth 1 https://github.com/dearimgui/dear_bindings

python3 -m venv venv
source venv/bin/activate
python3 -m pip install -r dear_bindings/requirements.txt

mkdir -p src
python3 dear_bindings/dear_bindings.py -o src/cimgui --custom-namespace-prefix ig imgui/imgui.h
python3 dear_bindings/dear_bindings.py -o src/cimgui_internal --custom-namespace-prefix ig --include imgui/imgui.h imgui/imgui_internal.h
cp imgui/*.h imgui/*.cpp imgui/LICENSE.txt src/

# docking branch
mkdir -p src-docking
python3 dear_bindings/dear_bindings.py -o src-docking/cimgui --custom-namespace-prefix ig imgui-docking/imgui.h
python3 dear_bindings/dear_bindings.py -o src-docking/cimgui_internal --custom-namespace-prefix ig --include imgui-docking/imgui.h imgui-docking/imgui_internal.h
cp imgui-docking/*.h imgui-docking/*.cpp imgui-docking/LICENSE.txt src-docking/
