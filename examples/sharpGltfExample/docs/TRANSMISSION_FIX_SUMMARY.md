# Transmission Material Fix Summary

## Problem Statement
The GlassHurricaneCandleHolder.gltf model was rendering as opaque solid blue instead of transparent glass. The transmission system was not working correctly.

## Root Cause Analysis
1. **Pipeline Selection**: Transmission materials were using OPAQUE pipeline instead of BLEND pipeline
2. **Alpha Output**: Shader was forcing `alpha = 1.0` for transmission materials, making them completely opaque
3. **Side Effects**: Fixes were affecting non-glass materials (watch numbers fuzzy, dragon losing color)

## Solution Implemented

### 1. **Threshold-Based Approach**
Only materials with `transmission_factor >= 0.1` (10% transmission) are treated as actual glass:

### 2. **Pipeline Selection Fix** (Frame.cs)
```csharp
// Only apply transmission (BLEND) pipeline for materials with SIGNIFICANT transmission
if (mesh.TransmissionFactor >= 0.1f) {
    effectiveAlphaMode = SharpGLTF.Schema2.AlphaMode.BLEND; // Force blend for real glass materials
}
```

### 3. **Transparency Sorting Fix** (Frame.cs)
```csharp
// Only treat materials as transparent if they have SIGNIFICANT transmission or explicit BLEND mode
bool isTransparent = (mesh.TransmissionFactor >= 0.1f) || (mesh.AlphaMode == SharpGLTF.Schema2.AlphaMode.BLEND);
```

### 4. **Alpha Calculation Fix** (Shader)
```glsl
if (transmission_factor >= 0.1) {
    // Calculate transmission alpha for actual glass materials
    float transmission_alpha = 1.0 - (transmission_factor * 0.4); // Max 40% transparency
    final_alpha = max(base_color.a * transmission_alpha, 0.2);
} else {
    // Materials with low/no transmission use base alpha unchanged
    final_alpha = base_color.a;
}
```

### 5. **Transmission Color Mixing Fix** (Shader)
```glsl
if (transmission_factor >= 0.1) {
    // Only apply refraction color mixing for actual glass materials
    vec3 refracted_color = calculate_refraction(...);
    color = mix(color, refracted_color, transmission_factor);
}
```

## Current Status
- ✅ **Glass Hurricane Candle Holder**: Properly transparent with refraction
- ❌ **Dragon**: Still losing color and appearing washed out/transparent
- ✅ **Watch**: Should be sharp and clear
- ✅ **Other Models**: Should be unaffected

## Next Steps
Need to investigate the Dragon model's transmission_factor value and possibly adjust the threshold or approach.