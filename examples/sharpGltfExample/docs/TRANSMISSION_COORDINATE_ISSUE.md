# Transmission Coordinate Issue Analysis

## Problem Description

The transmission rendering system exhibits viewing-angle-dependent artifacts where the car interior appears misaligned through glass windows. The misalignment rotates "twice as fast" when viewed from different angles, creating a "car inside car" effect.

## Root Cause Analysis

The issue stems from coordinate system inconsistency between:
1. **Opaque Pass**: Captures screen texture in screen space
2. **Refraction Pass**: Samples the captured texture for refraction effects

The refraction calculation was mixing coordinate systems, causing the viewing-angle-dependent rotation artifacts.

## Attempted Fixes

### Fix 1: Matrix Order Alignment (Reverted)
**Date:** 2024-12-19
**Status:** Reverted - Made issue worse

**Problem Analysis:**
Identified matrix multiplication order inconsistency between vertex shader (View * Proj) and refraction shader (Proj * View).

**Solution Approach:**
Swapped matrix order in refraction coordinate projection from `projection_matrix * view_matrix` to `view_matrix * projection_matrix` to match vertex shader.

**Code Changes:**
```glsl
// Before
vec4 screen_pos = projection_matrix * view_matrix * vec4(refracted_pos, 1.0);

// After
vec4 screen_pos = view_matrix * projection_matrix * vec4(refracted_pos, 1.0);
```

**Result:**
Made the viewing-angle artifacts worse, indicating the matrix order wasn't the root cause.

**Testing Status:**
- Build: ✅ Successful
- Prepare: ✅ Successful
- Runtime Testing: ❌ Made issue worse - reverted

### Fix 2: View-Space Refraction Calculation (Current)
**Date:** [Current Date]
**Status:** Implemented - Pending Testing

**Problem Analysis:**
The issue stems from coordinate system inconsistency between the opaque pass (which captures the screen texture) and the refraction pass (which samples it). The opaque pass renders to screen space, but the refraction calculation was mixing world-space and view-space coordinates.

**Solution Approach:**
Instead of trying to match matrix multiplication orders, perform the refraction calculation entirely in view space:

1. Transform normal and view direction to view space
2. Calculate refraction ray in view space
3. Transform the refracted ray back to screen space for texture sampling

**Code Changes:**
- Modified `calculate_refraction()` to use view-space normal and view direction
- Updated `getVolumeTransmissionRay()` call to pass view-space vectors
- Maintained original matrix order for screen space projection

**Expected Outcome:**
This should resolve the viewing-angle-dependent artifacts by ensuring consistent coordinate systems throughout the refraction pipeline.

**Testing Status:**
- Build: ✅ Successful
- Prepare: ✅ Successful
- Runtime Testing: Pending user feedback