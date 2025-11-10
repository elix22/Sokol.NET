# KHR_materials_variants Implementation Guide

## Extension Overview

**KHR_materials_variants** allows a glTF asset to define multiple material variants for mesh primitives, enabling runtime switching between different appearances (e.g., different colors, textures, finishes) without duplicating geometry.

**Specification**: https://github.com/KhronosGroup/glTF/tree/main/extensions/2.0/Khronos/KHR_materials_variants

---

## Current Implementation Status

### ✅ Already Implemented (from previous work)
- **KHR_materials_transmission**: Glass refraction with Snell's law and Beer's law attenuation
- **KHR_texture_transform**: Texture coordinate transformations for all 5 texture types

### ⏸️ Pending Implementation
- **KHR_materials_variants**: Material switching at runtime

---

## Extension Structure

### 1. glTF Root Extension
Defines the list of available variant names:

```json
{
  "extensions": {
    "KHR_materials_variants": {
      "variants": [
        { "name": "Midnight" },
        { "name": "Beach" },
        { "name": "Sunrise" }
      ]
    }
  }
}
```

### 2. Mesh Primitive Extension
Maps variants to materials for each primitive:

```json
{
  "primitives": [
    {
      "material": 0,  // Default material
      "extensions": {
        "KHR_materials_variants": {
          "mappings": [
            {
              "variants": [0],  // "Midnight" variant
              "material": 1     // Material index for this variant
            },
            {
              "variants": [1, 2],  // "Beach" and "Sunrise" variants
              "material": 2
            }
          ]
        }
      }
    }
  ]
}
```

---

## Implementation Requirements

### A. Data Structures (C# Code)

**1. Variant Definition Storage**
```csharp
public class MaterialVariant
{
    public string Name { get; set; }
    public int Index { get; set; }
}

public class VariantMapping
{
    public List<int> VariantIndices { get; set; }
    public int MaterialIndex { get; set; }
}

public class PrimitiveVariantInfo
{
    public int DefaultMaterialIndex { get; set; }
    public List<VariantMapping> Mappings { get; set; }
}
```

**2. Model-Level Tracking**
- Store list of all available variants from root extension
- Track current active variant index
- Map each mesh primitive to its variant mappings

### B. Parsing Logic

**Location**: `SharpGltfModel.cs` or new `MaterialVariantsManager.cs`

**Tasks**:
1. Parse `KHR_materials_variants` extension at model root level
2. Extract variant names and create variant list
3. For each mesh primitive:
   - Check for `KHR_materials_variants` extension
   - Parse mappings array
   - Store variant-to-material associations

**Example Parsing Code**:
```csharp
// At model root
var variantsExt = model.LogicalParent.Extensions
    .FirstOrDefault(e => e.Key == "KHR_materials_variants");
if (variantsExt.Value != null)
{
    var variants = variantsExt.Value["variants"] as JArray;
    foreach (var variant in variants)
    {
        string name = variant["name"].ToString();
        // Store variant info
    }
}

// At primitive level
var primitiveExt = primitive.Extensions
    .FirstOrDefault(e => e.Key == "KHR_materials_variants");
if (primitiveExt.Value != null)
{
    var mappings = primitiveExt.Value["mappings"] as JArray;
    // Parse and store mappings
}
```

### C. Runtime Switching

**Requirements**:
1. **UI Component**: Add ImGui dropdown/list to select active variant
2. **Material Swapping**: Update each primitive's material reference based on active variant
3. **Rendering Update**: Ensure material uniforms are updated before next frame

**Flow**:
```
User selects variant in UI
  ↓
Update active variant index
  ↓
For each mesh primitive:
  - Look up material index for active variant
  - If found in mappings: use variant material
  - Else: use default material
  ↓
Update material uniforms
  ↓
Render frame
```

### D. UI Integration

**Location**: `Main.cs` in ImGui rendering section

**UI Elements Needed**:
```csharp
ImGui.Begin("Material Variants");

if (_variantManager.HasVariants)
{
    // Dropdown showing all available variants
    if (ImGui.BeginCombo("Active Variant", _variantManager.CurrentVariantName))
    {
        foreach (var variant in _variantManager.Variants)
        {
            bool isSelected = _variantManager.CurrentVariantIndex == variant.Index;
            if (ImGui.Selectable(variant.Name, isSelected))
            {
                _variantManager.SetActiveVariant(variant.Index);
                // Trigger material update
            }
            if (isSelected)
                ImGui.SetItemDefaultFocus();
        }
        ImGui.EndCombo();
    }
}

ImGui.End();
```

---

## Implementation Steps

### Phase 1: Data Structures & Parsing
1. Create `MaterialVariantsManager` class
2. Add variant definition storage
3. Parse root-level variants list
4. Parse primitive-level mappings
5. Test parsing with glTF files containing variants

### Phase 2: Material Lookup & Switching
1. Implement variant-to-material lookup logic
2. Add method to switch active variant
3. Update primitive material references when variant changes
4. Test material switching programmatically (without UI)

### Phase 3: UI Integration
1. Add ImGui UI for variant selection
2. Wire UI events to variant switching logic
3. Test with user interaction

### Phase 4: Edge Cases & Polish
1. Handle primitives without variant mappings (use default)
2. Handle missing materials gracefully
3. Add visual feedback during switching
4. Optimize material updates (only changed primitives)

---

## Test Models

**glTF Sample Models with KHR_materials_variants**:
- `GlamVelvetSofa` - Multiple fabric color variants
- `MaterialsVariantsShoe` - Different shoe colorways
- Any model from: https://github.com/KhronosGroup/glTF-Sample-Models/tree/master/2.0

---

## Key Considerations

### Performance
- Minimize material state changes per frame
- Cache variant-to-material mappings
- Only update changed primitives

### Compatibility
- Ensure default material is used if variant extension is missing
- Handle malformed extension data gracefully
- Support models with partial variant coverage (some primitives have variants, others don't)

### User Experience
- Show clear indication of active variant
- Provide preview of variant appearance (if feasible)
- Remember user's variant selection across sessions

---

## Next Steps for New Chat

When starting implementation:

1. **Confirm scope**: UI-driven runtime switching? Programmatic API? Both?
2. **Choose test model**: Pick a glTF file with variants for testing
3. **Start with parsing**: Get variant data from glTF files first
4. **Then switching logic**: Implement material lookup and update
5. **Finally UI**: Add user interface last

**Recommended Starting Point**: Parse the variants extension and print available variants to console as a proof-of-concept.

---

## References

- **Specification**: https://github.com/KhronosGroup/glTF/tree/main/extensions/2.0/Khronos/KHR_materials_variants
- **Sample Viewer**: https://github.khronos.org/glTF-Sample-Viewer-Release/
- **Sample Models**: https://github.com/KhronosGroup/glTF-Sample-Models/tree/master/2.0

