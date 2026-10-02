# sokol_links — open a link (Android · iOS · macOS · Windows · Linux · Web)

Links plugin for Sokol.NET apps: a web URL opens in the **default browser**, a `mailto:` URL in the
**mail app** — the system's own handler on every platform.

| Platform | Web URL (`http`/`https`) | `mailto:` | `false` when |
|---|---|---|---|
| Android | `Intent.ACTION_VIEW` | `Intent.ACTION_SENDTO` (email apps only) | `ActivityNotFoundException` — no browser / no mail app |
| iOS | `UIApplication openURL:options:completionHandler:` | same | the URL does not parse (the open itself reports asynchronously) |
| macOS | `NSWorkspace openURL:` | same | no app handles the URL |
| Windows | `ShellExecute` (managed `Process.Start`, `UseShellExecute`) | same | no handler registered for the scheme |
| Linux | `xdg-open`, run as a process | same | `xdg-open` is not on the PATH |
| Web | `window.open(url, "_blank")` — a new tab, the game stays | an anchor click (the page is not unloaded) | the browser blocked the popup |

## Model

```csharp
if (!Links.Open("https://example.com/privacy"))   // or "mailto:support@example.com"
    Clipboard.Set(url);                            // fallback: let the user copy it
```

`Links.Open` is synchronous and non-blocking. It answers whether the system **accepted** the request;
keep a way to copy the link for the `false` case. Only absolute `http`, `https` and `mailto` URLs are
opened — anything else is refused, so a link assembled from data can never run a script
(`javascript:`) or open a local file. `Links.Supported` says whether the platform has a backend at all.

Call `Open` from the tap that asked for it (a `Button.Clicked` handler): browsers let a page open a
window only shortly after a user gesture.

**Store policy is the app's call, not the plugin's.** An app in a children's category may have to put
a parental gate in front of anything that leaves the app (App Store Kids Category, Google Play
Families) — gate the call site, the plugin opens what it is given.

## Integration (consuming app)

1. **Managed sources** — in the app `.csproj` (or `Directory.Build.props`):

   ```xml
   <Compile Include="$(SokolNetHome)/plugins/Links/managed/*.cs">
       <Link>Plugins\Links\%(Filename)%(Extension)</Link>
   </Compile>
   ```

   The managed layer keys on the usual platform symbols: `__ANDROID__`, `__IOS__`, `__MACOS__`,
   `__WINDOWS__`, `__LINUX__`, `WEB` (see `templates/template_app/template.csproj`). A build that
   defines none of them gets the stub (`Supported == false`, `Open` returns false).

2. **`Directory.Build.props`** — the `*Path` properties wire the native lib into the APK / app bundle:

   ```xml
   <PropertyGroup>
      <AndroidNativeLibrary_sokol_linksPath>../../plugins/Links/libs/android</AndroidNativeLibrary_sokol_linksPath>
      <IOSNativeLibrary_sokol_linksPath>../../plugins/Links/libs/ios/arm64/release</IOSNativeLibrary_sokol_linksPath>
   </PropertyGroup>
   ```

3. **macOS** — copy the dylib next to the executable in the app's `CopyCustomContent*` targets:

   ```xml
   <Copy SourceFiles="$(SokolNetHome)/plugins/Links/libs/macos/$(OSArch)/release/libsokol_links.dylib" DestinationFolder="$(OutDir)" SkipUnchangedFiles="true" />
   ```

4. **Web** — add the static archive to the WASM link (the JS is inline `EM_JS`, no `--js-library`):

   ```xml
   <NativeFileReference Include="$(SokolNetHome)/plugins/Links/libs/emscripten/x86/release/sokol_links.a" />
   ```

5. **Windows / Linux** — nothing to link or copy.

No permissions, entitlements, manifest entries or Info.plist keys are needed on any platform: Android's
package visibility (11+) restricts querying other apps, not starting an implicit intent, and iOS's
`LSApplicationQueriesSchemes` gates `canOpenURL:`, which is not used.

## Layout

```
managed/   Links.cs (public API + Windows ShellExecute + Linux xdg-open)  SokolLinks.cs (P/Invoke)
native/    sokol_links.h             C ABI: bool sokollinks_open(const char* url)
           sokol_links_android.c     JNI: Uri.parse + Intent + Activity.startActivity (no Java helper)
           sokol_links_apple.m       UIApplication (iOS) / NSWorkspace (macOS)
           sokol_links_web.c         window.open / anchor click via EM_JS (Emscripten)
scripts/   build-android.sh  build-ios.sh  build-macos.sh  build-web.sh
libs/      prebuilt outputs, ALL built and committed by CI: android/<abi>/release/libsokol_links.so,
           ios/<target>/{debug,release}/sokol_links.framework, macos/<arch>/release/libsokol_links.dylib,
           emscripten/x86/release/sokol_links.a
```

CI: `.github/workflows/build-links-plugin.yml` builds every backend (and compiles the managed layer
for every platform symbol set) on every push that touches `plugins/Links/`, and commits the Android,
iOS, macOS and Web libraries back to `plugins/Links/libs/` on `main`. Never commit a local build — the
scripts are for local iteration only.
