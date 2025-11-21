# Thumbnails for Examples Showcase

This directory contains thumbnail images for each example in the showcase.

## Image Specifications

- **Format**: PNG (preferred) or JPG
- **Dimensions**: 800x600 pixels (4:3 aspect ratio)
- **File size**: < 200KB recommended
- **Naming**: `{example-name}.png` (e.g., `cube.png`, `shadows.png`)

## How to Generate Thumbnails

### Option 1: Manual Screenshots

1. Run each example on desktop or web
2. Wait for the example to load and show interesting content
3. Take a screenshot
4. Crop to 800x600 pixels
5. Save as PNG in this directory

### Option 2: Automated Screenshot Tool

You can use a screenshot automation tool to capture thumbnails:

```bash
# Example using headless browser (requires playwright or puppeteer)
# This is just a concept - you'd need to implement the actual script

for example in cube shadows instancing # ... etc
do
    # Start local server
    cd ../examples/$example/bin/Debug/net8.0/wwwroot
    python3 -m http.server 8080 &
    SERVER_PID=$!
    
    # Wait for server to start
    sleep 2
    
    # Take screenshot (pseudocode)
    # screenshot-tool http://localhost:8080 --output ../../../../../../docs/thumbnails/$example.png --size 800x600
    
    # Stop server
    kill $SERVER_PID
    cd -
done
```

### Option 3: Use Placeholder Until Real Thumbnails

The showcase page gracefully handles missing thumbnails by showing a 🎮 emoji on a gradient background.

## Current Status

Currently using placeholder emoji backgrounds. Real thumbnails should be added for better visual appeal.

## Examples Requiring Thumbnails

- [ ] cube
- [ ] clear
- [ ] instancing
- [ ] offscreen
- [ ] mrt
- [ ] shadows
- [ ] sgl
- [ ] sgl_lines
- [ ] shapes_transform
- [ ] dyntex
- [ ] debugtext
- [ ] debugtext_context
- [ ] fontstash
- [ ] fontstash_layers
- [ ] assimp_simple
- [ ] assimp_animation
- [ ] assimp_scene
- [ ] cgltf
- [ ] GltfViewer
- [ ] ozz_shdfeatures
- [ ] spine_simple
- [ ] spine_skinsets
- [ ] spine_inspector
- [ ] loadpng
- [ ] basisu
- [ ] cubemap_jpeg
- [ ] cubemaprt
- [ ] miprender
- [ ] vertextexture
- [ ] texview
- [ ] sdf
- [ ] cimgui
- [ ] imgui_usercallback
- [ ] drawcallperf
- [ ] plmpeg
