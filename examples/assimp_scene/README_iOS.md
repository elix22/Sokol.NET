# iOS Asset Handling for XML-Based 3D Formats

## Issue: Binary Plist Conversion

iOS/Xcode automatically converts XML-like files to binary plist format during the app bundling process. This affects XML-based 3D formats like:
- COLLADA (`.dae`)
- glTF (`.gltf`) 
- Other XML-based formats

### Problem
When these files are bundled into an iOS app, Xcode detects the XML structure and converts them to binary plist (`bplist00` format). This causes parsers like Assimp to fail with "malformed XML" errors because they receive binary data instead of text XML.

### Solution
**Rename your XML/text-based asset files to use a different extension that iOS doesn't recognize:**

| Original Extension | Recommended Alternative | Notes |
|-------------------|------------------------|-------|
| `.dae` (COLLADA) | `.collada` or `.dae.data` | XML-based, will be converted |
| `.gltf` (glTF text) | `.gltf.data` or `.gltfdata` | JSON/text-based, may be converted |
| `.xml` | `.xml.data` | XML-based, will be converted |
| `.glb` (glTF binary) | No change needed | Binary format, safe from conversion |
| `.obj` | No change needed | Plain text, not XML-like |
| `.fbx` | No change needed | Binary format |

This prevents the automatic conversion while still allowing parsers to correctly identify and parse the format using the format hint parameter.

### Implementation Example

**1. Rename your asset file:**
```bash
cp duck.dae duck.collada
# or
cp model.gltf model.gltf.data
```

**2. Update your code to load the renamed file:**
```csharp
// Use .collada extension to prevent iOS binary plist conversion
request.path = util_get_file_path("duck.collada");

// Extract extension for format hint
string path = response->path != IntPtr.Zero ? Marshal.PtrToStringUTF8((IntPtr)response->path) ?? "" : "";
string formatHint = Path.GetExtension(path).TrimStart('.'); // "collada"

// Assimp will correctly identify it as COLLADA format
Scene scene = importer.ImportFileFromStream(stream, ppSteps, formatHint);
```

### Platform Compatibility
- ✅ **macOS Desktop**: Works with all extensions (`.dae`, `.collada`, `.gltf`, etc.)
- ✅ **Web Browser**: Works with all extensions
- ✅ **Android**: Works with all extensions  
- ⚠️ **iOS**: Requires non-XML extensions (`.collada`, `.gltf.data`, etc.)

### Why This Happens
iOS's build system optimizes XML files by converting them to binary plist format for faster loading. While this works great for actual plist files, it breaks 3D asset files that need to remain as XML.

### Alternative Solutions Attempted
❌ `COPY_PHASE_STRIP=NO` - Doesn't affect asset processing  
❌ Post-build `plutil` conversion - Too complex and fragile  
❌ CMake file attributes - Not supported for this use case  
✅ **File extension workaround - Simple, reliable, and works universally**
