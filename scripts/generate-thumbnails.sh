#!/bin/bash

# Generate thumbnails for WebAssembly examples showcase
# Usage: ./generate-thumbnails.sh [example_name]
# If example_name is provided, only that thumbnail will be generated
# Otherwise, all thumbnails will be generated from existing screenshots

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
THUMBNAILS_DIR="$REPO_ROOT/docs/thumbnails"
SCREENSHOTS_DIR="$REPO_ROOT/screenshots"
BUILDER_TOOL="$REPO_ROOT/tools/SokolApplicationBuilder"

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║        Sokol.NET Thumbnail Generation Script              ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Target thumbnail size
THUMB_WIDTH=800
THUMB_HEIGHT=600

# Create thumbnails directory
mkdir -p "$THUMBNAILS_DIR"

echo -e "${GREEN}✓ Using SokolApplicationBuilder for image processing${NC}"
echo ""

# Function to resize and crop image to fill target dimensions using SokolApplicationBuilder
resize_and_crop() {
    local source="$1"
    local dest="$2"
    local width="$3"
    local height="$4"
    
    dotnet run --project "$BUILDER_TOOL" -- \
        --task imageprocess \
        --source "$source" \
        --dest "$dest" \
        --width "$width" \
        --height "$height" > /dev/null 2>&1
}

# Function to process a single example
process_example() {
    local example="$1"
    local source_image=""
    
    # Look for source image in various locations
    # 1. Check screenshots directory
    for ext in png jpg jpeg PNG JPG JPEG; do
        if [ -f "$SCREENSHOTS_DIR/$example.$ext" ]; then
            source_image="$SCREENSHOTS_DIR/$example.$ext"
            break
        fi
    done
    
    # 2. Check example Assets directory
    if [ -z "$source_image" ]; then
        local assets_dir="$REPO_ROOT/examples/$example/Assets"
        if [ -d "$assets_dir" ]; then
            for ext in png jpg jpeg PNG JPG JPEG; do
                local found=$(find "$assets_dir" -maxdepth 1 -iname "*screenshot*.$ext" -o -iname "*thumbnail*.$ext" 2>/dev/null | head -1)
                if [ -n "$found" ] && [ -f "$found" ]; then
                    source_image="$found"
                    break
                fi
            done
        fi
    fi
    
    # 3. Check docs/examples output (might have runtime screenshots)
    if [ -z "$source_image" ]; then
        local example_dir="$REPO_ROOT/docs/examples/$example"
        if [ -d "$example_dir" ]; then
            for ext in png jpg jpeg PNG JPG JPEG; do
                local found=$(find "$example_dir" -maxdepth 1 -iname "*screenshot*.$ext" -o -iname "*thumbnail*.$ext" 2>/dev/null | head -1)
                if [ -n "$found" ] && [ -f "$found" ]; then
                    source_image="$found"
                    break
                fi
            done
        fi
    fi
    
    if [ -z "$source_image" ]; then
        echo -e "${YELLOW}   ⚠ No source image found for $example${NC}"
        echo -e "${YELLOW}      Place a screenshot at: $SCREENSHOTS_DIR/$example.png${NC}"
        return 1
    fi
    
    local dest_image="$THUMBNAILS_DIR/$example.png"
    
    echo -e "${BLUE}   Processing: ${NC}$(basename "$source_image") → ${example}.png"
    
    resize_and_crop "$source_image" "$dest_image" "$THUMB_WIDTH" "$THUMB_HEIGHT"
    
    if [ -f "$dest_image" ]; then
        echo -e "${GREEN}   ✓ Generated thumbnail: $example.png (${THUMB_WIDTH}x${THUMB_HEIGHT})${NC}"
        return 0
    else
        echo -e "${RED}   ✗ Failed to generate thumbnail for $example${NC}"
        return 1
    fi
}

# List of all examples
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
    # Process only the requested example
    EXAMPLES=("$SPECIFIC_EXAMPLE")
    echo -e "${YELLOW}Generating thumbnail for: $SPECIFIC_EXAMPLE${NC}"
    echo ""
fi

# Process counters
TOTAL=${#EXAMPLES[@]}
SUCCESS=0
FAILED=0
SKIPPED=0

echo -e "${BLUE}Total examples to process: $TOTAL${NC}"
echo ""

# Create screenshots directory if it doesn't exist
mkdir -p "$SCREENSHOTS_DIR"

# Process each example
for i in "${!EXAMPLES[@]}"; do
    EXAMPLE="${EXAMPLES[$i]}"
    NUM=$((i + 1))
    
    echo -e "${YELLOW}[$NUM/$TOTAL]${NC} ${GREEN}$EXAMPLE${NC}"
    
    if process_example "$EXAMPLE"; then
        ((SUCCESS++))
    else
        ((SKIPPED++))
    fi
    
    echo ""
done

# Summary
echo -e "${BLUE}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║                   Thumbnail Summary                        ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════╝${NC}"
echo -e "Total:    $TOTAL"
echo -e "${GREEN}Success:  $SUCCESS${NC}"
echo -e "${YELLOW}Skipped:  $SKIPPED (no source image)${NC}"
echo ""

if [ $SUCCESS -gt 0 ]; then
    echo -e "${GREEN}✓ Generated $SUCCESS thumbnail(s)!${NC}"
    echo -e "${BLUE}Thumbnails saved to: $THUMBNAILS_DIR${NC}"
    echo ""
    echo -e "${YELLOW}Next steps:${NC}"
    echo -e "1. Review thumbnails in: ${BLUE}$THUMBNAILS_DIR${NC}"
    echo -e "2. Add missing screenshots to: ${BLUE}$SCREENSHOTS_DIR${NC}"
    echo -e "3. Re-run this script to generate remaining thumbnails"
fi

if [ $SKIPPED -gt 0 ]; then
    echo ""
    echo -e "${YELLOW}Note: $SKIPPED example(s) skipped - no source images found${NC}"
    echo -e "${YELLOW}To generate thumbnails for these:${NC}"
    echo -e "1. Take screenshots of the examples"
    echo -e "2. Save them to: ${BLUE}$SCREENSHOTS_DIR/example_name.png${NC}"
    echo -e "3. Run: ${GREEN}./scripts/generate-thumbnails.sh example_name${NC}"
fi

exit 0
