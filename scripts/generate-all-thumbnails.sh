#!/bin/bash

# Generate thumbnails for all example screenshots
# Thumbnails are 300x225 (4:3 ratio) for optimal web performance

WORKSPACE_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SCREENSHOTS_DIR="$WORKSPACE_ROOT/screenshots"
THUMBNAILS_DIR="$WORKSPACE_ROOT/docs/thumbnails"
BUILDER_PROJECT="$WORKSPACE_ROOT/tools/SokolApplicationBuilder"

# Create thumbnails directory if it doesn't exist
mkdir -p "$THUMBNAILS_DIR"

# List of all examples with screenshots
EXAMPLES=(
    "GltfViewer"
    "assimp_animation"
    "assimp_scene"
    "assimp_simple"
    "basisu"
    "cgltf"
    "cimgui"
    "clear"
    "cube"
    "cubemap_jpeg"
    "cubemaprt"
    "debugtext"
    "debugtext_context"
    "drawcallperf"
    "dyntex"
    "fontstash"
    "fontstash_layers"
    "imgui_usercallback"
    "instancing"
    "loadpng"
    "miprender"
    "mrt"
    "offscreen"
    "ozz_shdfeatures"
    "plmpeg"
    "sdf"
    "sgl"
    "sgl_lines"
    "shadows"
    "shapes_transform"
    "spine_inspector"
    "spine_simple"
    "spine_skinsets"
    "texview"
    "vertextexture"
)

echo "🖼️  Generating thumbnails for ${#EXAMPLES[@]} examples..."
echo "   Source: $SCREENSHOTS_DIR"
echo "   Output: $THUMBNAILS_DIR"
echo "   Size: 400x300 pixels"
echo ""

SUCCESS_COUNT=0
FAILED_COUNT=0

for example in "${EXAMPLES[@]}"; do
    SOURCE="$SCREENSHOTS_DIR/${example}.png"
    DEST="$THUMBNAILS_DIR/${example}.png"
    
    if [ ! -f "$SOURCE" ]; then
        echo "⚠️  Skipped: $example (source not found)"
        ((FAILED_COUNT++))
        continue
    fi
    
    echo "Processing: $example..."
    
    dotnet run --project "$BUILDER_PROJECT" -- \
        --task imageprocess \
        --source "$SOURCE" \
        --dest "$DEST" \
        --width 400 \
        --height 300 \
        --mode crop > /dev/null 2>&1
    
    if [ $? -eq 0 ] && [ -f "$DEST" ]; then
        FILE_SIZE=$(du -h "$DEST" | cut -f1)
        echo "✅ $example ($FILE_SIZE)"
        ((SUCCESS_COUNT++))
    else
        echo "❌ Failed: $example"
        ((FAILED_COUNT++))
    fi
done

echo ""
echo "========================================="
echo "✅ Successfully generated: $SUCCESS_COUNT thumbnails"
if [ $FAILED_COUNT -gt 0 ]; then
    echo "❌ Failed: $FAILED_COUNT thumbnails"
fi
echo "========================================="
echo ""
echo "Thumbnails saved to: $THUMBNAILS_DIR"
