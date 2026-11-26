# sokol_filepicker — the system's file pickers (Android · iOS · macOS · Windows · Linux)

FilePicker plugin for Sokol.NET apps: **open** a file the user picks, or **save** to a place and name the user picks —
anywhere the system's own picker reaches (device storage, iCloud Drive, Google Drive, Dropbox, OneDrive, a USB stick,
… through their document providers) — and **keep access** to it across launches for a recent-files list.

| Platform | Picker | A file is | Access after a restart |
|---|---|---|---|
| Android | Storage Access Framework: `ACTION_OPEN_DOCUMENT` / `ACTION_CREATE_DOCUMENT` from a see-through helper activity | a `content://` URI | the URI permission is made persistent at pick time |
| iOS | `UIDocumentPickerViewController` (open in place; save = export of the bytes) | a security-scoped file URL | a bookmark |
| macOS | `NSOpenPanel` / `NSSavePanel` as a sheet on the app's window | a file URL | a (security-scoped where possible) bookmark |
| Windows | the common file dialogs (`GetOpenFileNameW` / `GetSaveFileNameW`) on their own STA thread | a path | the path |
| Linux | `zenity` or `kdialog`, run as a process | a path | the path |
| Web | none (`Supported == false`) | | |

## Model

```csharp
FilePicker.Open(new[] { "map" }, r =>                 // answered from FilePicker.Update()
{
    if (r.Status != FilePickerStatus.Picked) return;   // Cancelled or Failed
    string handle = r.Handle!;                         // keep it; r.Name is what the user sees ("dungeon.map")
    string? token = FilePicker.Bookmark(handle);       // store with the recent-files list
    Task.Run(() => Load(FilePicker.Read(handle)));     // I/O may block (a cloud file downloads first): a worker
});

FilePicker.Save("dungeon.map", bytes, new[] { "map" }, r => { /* r.Handle = the new file, already written */ });

// every frame, on the game thread:
FilePicker.Update();

// later, after a restart:
string? handle = FilePicker.Resolve(token);            // null: moved, deleted, or access taken back
```

- `Open` and `Save` return at once (false when a picker is already up or none can be shown, and then the callback is
  not called). The answer reaches the callback from `FilePicker.Update()`, which the app calls once per frame.
- `Read`, `Write`, `Size`, `Modified` may block: call them from a worker thread. On Apple they go through
  `NSFileCoordinator`, so a cloud file is fetched first and its provider sees the change.
- **A handle is not a path.** Never build paths from it, never write a file next to it: the app may only hold access to
  that one file.
- Extensions filter where the system can. Android shows every file (custom extensions have no MIME type) and iOS shows
  every file for an extension the system does not know: check the picked name.
- `Modified` and `Size` come from the provider's own records and may lag a change made outside it (Android's Downloads
  provider did): to tell whether someone else changed a file since it was opened, compare its content.
- Android: the picker covers the app, so the app's frame loop pauses until it closes. The helper activity uses
  `screenOrientation="behind"`: the picker keeps the app's orientation.

## Integration (consuming app)

1. **Managed sources** — in the app `.csproj` (or `Directory.Build.props`):

   ```xml
   <Compile Include="$(SokolNetHome)/plugins/FilePicker/managed/*.cs">
       <Link>Plugins\FilePicker\%(Filename)%(Extension)</Link>
   </Compile>
   ```

   The managed layer keys on `__ANDROID__`, `__IOS__`, `__MACOS__`, `__WINDOWS__`, `__LINUX__`; any other build gets
   the stub (`Supported == false`). It needs `AllowUnsafeBlocks`.

2. **`Directory.Build.props`** — paths relative to the project (the builder reads them as written):

   ```xml
   <PropertyGroup>
      <AndroidNativeLibrary_sokol_filepickerPath>../../plugins/FilePicker/libs/android</AndroidNativeLibrary_sokol_filepickerPath>
      <IOSNativeLibrary_sokol_filepickerPath>../../plugins/FilePicker/libs/ios/arm64/release</IOSNativeLibrary_sokol_filepickerPath>
      <AndroidJavaSource_sokolfilepickerPath>../../plugins/FilePicker/platform/android/java</AndroidJavaSource_sokolfilepickerPath>
   </PropertyGroup>
   ```

   The native library path also activates `platform/android/manifest/Providers.xml` (the helper activity).

3. **macOS** — copy the dylib next to the executable in the app's `CopyCustomContent*` targets:

   ```xml
   <Copy SourceFiles="$(SokolNetHome)/plugins/FilePicker/libs/macos/$(OSArch)/release/libsokol_filepicker.dylib" DestinationFolder="$(OutDir)" SkipUnchangedFiles="true" />
   ```

4. **Windows / Linux** — nothing to link or copy.

No permission is needed on any platform: the user's pick is the grant (Android needs no storage permission for SAF, iOS
and macOS give the app access to exactly the picked file).

## Layout

```
managed/   FilePicker.cs (public API + Windows common dialogs + Linux zenity/kdialog)   SokolFilePicker.cs (P/Invoke)
native/    sokol_filepicker.h            C ABI (start / poll / result / load+take / write / size / modified / bookmark / resolve)
           sokol_filepicker_android.c    JNI calls into FilePickerBridge
           sokol_filepicker_apple.m      UIDocumentPickerViewController (iOS), NSOpenPanel / NSSavePanel (macOS)
           CMakeLists.txt
platform/android/java/com/sokol/filepicker/   FilePickerActivity (see-through, receives the picker's answer), FilePickerBridge
platform/android/manifest/Providers.xml       the activity's manifest entry
scripts/   build-android.sh  build-ios.sh  build-macos.sh
libs/      CI-built libraries (never commit a local build)
```
