#!/bin/bash

# Build all WebAssembly examples and prepare for GitHub Pages deployment
# Usage: ./build-all-web-examples.sh [example_name]
# If example_name is provided, only that example will be built
# Otherwise, all examples will be built

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DOCS_DIR="$REPO_ROOT/docs"
EXAMPLES_OUTPUT="$DOCS_DIR/examples"

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║        Sokol.NET WebAssembly Examples Build Script        ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""

# List of all examples to build
EXAMPLES=(
    "cube"
    "clear"
    "instancing"
    "offscreen"
    "mrt"
    "shadows"
    "sgl"
    "sgl_lines"
    "shapes_transform"
    "dyntex"
    "debugtext"
    "debugtext_context"
    "fontstash"
    "fontstash_layers"
    "assimp_simple"
    "assimp_animation"
    "assimp_scene"
    "cgltf"
    "GltfViewer"
    "ozz_shdfeatures"
    "spine_simple"
    "spine_skinsets"
    "spine_inspector"
    "loadpng"
    "basisu"
    "cubemap_jpeg"
    "cubemaprt"
    "miprender"
    "vertextexture"
    "texview"
    "sdf"
    "cimgui"
    "imgui_usercallback"
    "drawcallperf"
    "plmpeg"
)

# Check if a specific example was requested
SPECIFIC_EXAMPLE=""
if [ $# -gt 0 ]; then
    SPECIFIC_EXAMPLE="$1"
    # Check if the example exists in the list
    if [[ ! " ${EXAMPLES[@]} " =~ " ${SPECIFIC_EXAMPLE} " ]]; then
        echo -e "${RED}Error: Example '$SPECIFIC_EXAMPLE' not found in the examples list${NC}"
        echo -e "${YELLOW}Available examples:${NC}"
        for ex in "${EXAMPLES[@]}"; do
            echo "  - $ex"
        done
        exit 1
    fi
    # Build only the requested example
    EXAMPLES=("$SPECIFIC_EXAMPLE")
    echo -e "${YELLOW}Building only: $SPECIFIC_EXAMPLE${NC}"
    echo ""
fi

# Create docs/examples directory
mkdir -p "$EXAMPLES_OUTPUT"

# Build counter
TOTAL=${#EXAMPLES[@]}
SUCCESS=0
FAILED=0
SKIPPED=0

echo -e "${BLUE}Total examples to build: $TOTAL${NC}"
echo ""

# Build each example
for i in "${!EXAMPLES[@]}"; do
    EXAMPLE="${EXAMPLES[$i]}"
    NUM=$((i + 1))
    
    echo -e "${YELLOW}[$NUM/$TOTAL]${NC} Building ${GREEN}$EXAMPLE${NC}..."
    
    EXAMPLE_PATH="$REPO_ROOT/examples/$EXAMPLE"
    EXAMPLE_OUTPUT="$EXAMPLES_OUTPUT/$EXAMPLE"
    
    # Try different web project naming patterns
    if [ -f "$EXAMPLE_PATH/${EXAMPLE}web.csproj" ]; then
        WEB_CSPROJ="$EXAMPLE_PATH/${EXAMPLE}web.csproj"
    elif [ -f "$EXAMPLE_PATH/${EXAMPLE}Web.csproj" ]; then
        WEB_CSPROJ="$EXAMPLE_PATH/${EXAMPLE}Web.csproj"
    else
        WEB_CSPROJ=""
    fi
    
    # Check if example directory exists
    if [ ! -d "$EXAMPLE_PATH" ]; then
        echo -e "${RED}   ✗ Example directory not found: $EXAMPLE_PATH${NC}"
        ((SKIPPED++))
        continue
    fi
    
    # Check if web project file exists
    if [ -z "$WEB_CSPROJ" ] || [ ! -f "$WEB_CSPROJ" ]; then
        echo -e "${RED}   ✗ Web project file not found for: $EXAMPLE${NC}"
        ((SKIPPED++))
        continue
    fi
    
    # Compile shaders first
    if ! dotnet build "$WEB_CSPROJ" -t:CompileShaders > /dev/null 2>&1; then
        echo -e "${YELLOW}   ⚠ Shader compilation warning (continuing anyway)${NC}"
    fi
    
    # Build the project in Release configuration
    if dotnet build "$WEB_CSPROJ" -c Release > /dev/null 2>&1; then
        
        # The AppBundle contains the complete deployable web app
        APP_BUNDLE="$EXAMPLE_PATH/bin/Release/net8.0/browser-wasm/AppBundle"
        
        if [ -d "$APP_BUNDLE" ]; then
            # Copy AppBundle contents to docs/examples/EXAMPLE_NAME
            mkdir -p "$EXAMPLE_OUTPUT"
            cp -r "$APP_BUNDLE"/* "$EXAMPLE_OUTPUT/"
            
            # Ensure Assets directory is copied if it exists and wasn't already in AppBundle
            if [ -d "$EXAMPLE_PATH/Assets" ]; then
                # Copy assets, but don't overwrite if they're already there from AppBundle
                cp -rn "$EXAMPLE_PATH/Assets"/* "$EXAMPLE_OUTPUT/" 2>/dev/null || true
            fi
            
            echo -e "${GREEN}   ✓ Built and deployed to docs/examples/$EXAMPLE${NC}"
            ((SUCCESS++))
        else
            echo -e "${RED}   ✗ Build succeeded but AppBundle not found at $APP_BUNDLE${NC}"
            ((FAILED++))
        fi
    else
        echo -e "${RED}   ✗ Build failed${NC}"
        ((FAILED++))
    fi
    
    echo ""
done

# Summary
echo -e "${BLUE}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║                      Build Summary                         ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════╝${NC}"
echo -e "Total:    $TOTAL"
echo -e "${GREEN}Success:  $SUCCESS${NC}"
echo -e "${RED}Failed:   $FAILED${NC}"
echo -e "${YELLOW}Skipped:  $SKIPPED${NC}"
echo ""

if [ $FAILED -eq 0 ] && [ $SUCCESS -gt 0 ]; then
    echo -e "${GREEN}✓ All builds completed successfully!${NC}"
    echo -e "${BLUE}Examples are ready in: $EXAMPLES_OUTPUT${NC}"
    echo ""
    echo -e "${YELLOW}Next steps:${NC}"
    echo -e "1. Commit and push the docs/examples/ directory"
    echo -e "2. Enable GitHub Pages in repository settings (Settings → Pages → Source: main branch /docs folder)"
    echo -e "3. Visit https://elix22.github.io/Sokol.NET/ to view the showcase"
    exit 0
else
    echo -e "${RED}✗ Some builds failed or were skipped. Check the output above for details.${NC}"
    exit 1
fi
