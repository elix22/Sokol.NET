# Share Plugin

Cross-platform share feature: lets players share their score along with a generated PNG
score card via the platform's native share sheet (WhatsApp, Telegram, Mail, etc.).
On Android and iOS it also shares any file the app has written (`SharePlugin.ShareFile`), and
puts text on the clipboard (`SharePlugin.SetClipboard`).

## Platforms

| Platform | Mechanism | Image |
|----------|-----------|-------|
| Android  | `Intent.ACTION_SEND` + FileProvider | ✅ |
| iOS      | `UIActivityViewController` | ✅ |
| macOS    | `NSSharingServicePicker` (native share sheet) | ✅ |
| Windows  | MAPI (`MAPISendDocuments`) — opens default mail client with image attached | ✅ |
| Linux    | `xdg-email --attach` — opens default mail client with image attached | ✅ |

Web (WASM) is not supported.

## Integration

### 1 — Managed C# (required on all platforms)

Add to your `.csproj`:
```xml
<ItemGroup>
  <Compile Include="$(SokolNetHome)/plugins/Share/managed/*.cs" />
</ItemGroup>
```

### 2 — Native library

`sokol_share` is a **self-contained shared library** with its own
`native/CMakeLists.txt`. It is built independently of the main sokol library and
outputs to `plugins/Share/libs/`:

```
plugins/Share/libs/
├── android/
│   ├── arm64-v8a/release/libsokol_share.so
│   ├── armeabi-v7a/release/libsokol_share.so
│   └── x86_64/release/libsokol_share.so
├── ios/
│   ├── arm64/{debug,release}/sokol_share.framework
│   ├── simulator-arm64/{debug,release}/sokol_share.framework
│   └── simulator-x64/{debug,release}/sokol_share.framework
└── macos/
    ├── arm64/release/libsokol_share.dylib
    └── X64/release/libsokol_share.dylib
```

Use the platform build scripts (run from any directory):

```bash
# iOS (device + both simulators)
./plugins/Share/scripts/build-ios.sh [device|simulator-arm64|simulator-x64|all]

# Android (armeabi-v7a, arm64-v8a, x86_64) — requires $ANDROID_NDK
./plugins/Share/scripts/build-android.sh

# macOS (arm64 + X64)
./plugins/Share/scripts/build-macos.sh

# Windows / Linux — no native library needed (MAPI / xdg-email via managed code)
```

### 3 — Wire up native libraries in your project

**Android** — add to `Directory.Build.props`:
```xml
<PropertyGroup>
  <AndroidNativeLibrary_sokol_sharePath>../../plugins/Share/libs/android</AndroidNativeLibrary_sokol_sharePath>
  <AndroidGradleDependency_AndroidXCore>androidx.core:core:1.12.0</AndroidGradleDependency_AndroidXCore>
</PropertyGroup>
```

**iOS** — add to `Directory.Build.props`:
```xml
<PropertyGroup>
  <IOSNativeLibrary_sokol_sharePath>../../plugins/Share/libs/ios/arm64/release</IOSNativeLibrary_sokol_sharePath>
</PropertyGroup>
```

**macOS** — add a copy step to your `.csproj` `CopyCustomContentMacOS` target:
```xml
<libSokolShare>$(SokolNetHome)/plugins/Share/libs/macos/$(OSArch)/release/libsokol_share.dylib</libSokolShare>
...
<Copy SourceFiles="$(libSokolShare)" DestinationFolder="$(OutDir)" SkipUnchangedFiles="true" />
```

Also add `__MACOS__` to `DefineConstants` in your `.csproj`:
```xml
<DefineConstants Condition="'$(IsOSX)'=='true'">$(DefineConstants);__MACOS__</DefineConstants>
```

### 4 — Android FileProvider (Android only)

Copy `platform/android/` from `examples/SmilingFruits/platform/android/` as a reference
into your app's `platform/android/` folder. The builder automatically injects
`Providers.xml` into the manifest and copies `res/xml/file_provider_paths.xml` into the APK.

### 5 — `sokol_app.h` accessor (Android only, one-time)

Add to the public API section of `ext/sokol/sokol_app.h`:
```c
#if defined(__ANDROID__)
SOKOL_APP_API_DECL ANativeActivity* sapp_android_get_native_activity(void);
SOKOL_APP_API_IMPL ANativeActivity* sapp_android_get_native_activity(void) {
    return _sapp.android.activity;
}
#endif
```

## Usage

```csharp
// Call when the player taps the Share button at game over:
SharePlugin.ShareScore(score, "My Game");
```

### Share a file (Android and iOS only)

```csharp
// Any file the app has written: an exported image, a ZIP, a JSON document ... The share sheet gets the
// file itself, with its name (Mail, chat apps, Drive, AirDrop, Save to Files, Print, ...).
if (SharePlugin.CanShareFile)                       // true on Android and iOS only
    SharePlugin.ShareFile(path, "image/png");       // false: missing file / cannot be shared
```

- `mime`: Android offers the apps that accept that type (`null` = `application/octet-stream`); iOS reads
  the type from the file's extension.
- Android: the file must be under the app's files folder (`sfs_get_pref_path`), which
  `platform/android/res/xml/file_provider_paths.xml` exposes through the plugin's FileProvider.
- Never throws: a Java exception (for example a path outside the shared folder) is cleared and reported
  as `false`.

## Android: 16 KB page alignment (mandatory)

`scripts/build-android.sh` passes `-DANDROID_SUPPORT_FLEXIBLE_PAGE_SIZES=ON` and the CI workflow
(`.github/workflows/build-share-plugin.yml`) fails unless the 64-bit libraries are 16 KB aligned
(Google Play, Android 15+ devices). The libraries in `libs/` are built by that workflow, never locally.

## File I/O

All file operations use `Sokol.SFilesystem` exclusively.
The generated PNG is written to `sfs_get_temp_dir()` as `score_card.png` on all platforms.
