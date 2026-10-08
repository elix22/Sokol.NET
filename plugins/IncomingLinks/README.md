# sokol_inlinks — receive the link that opened the app (Android)

IncomingLinks plugin for Sokol.NET apps: an **Android App Link** (`https://` on a domain the app owns) or a
**custom URL scheme** opens the app, and the game reads the URL — whether the link started the app (cold
start) or arrived while it was running.

| Platform | How the link arrives | Notes |
|---|---|---|
| Android | `SokolNativeActivity` records the `ACTION_VIEW` intent's URI — `onCreate` (cold start) and `onNewIntent` (the activity is `singleTask`, so a link reaching the running app lands there) — and the plugin takes it over JNI | needs `AndroidFullscreen=true` (that selects `SokolNativeActivity`); with the plain `NativeActivity` only the cold-start link is seen |
| iOS, macOS, Windows, Linux, Web | not implemented yet | `Supported == false`, `TakePending()` returns null |

## Model

```csharp
// once per frame, on the game thread
string? url = IncomingLinks.TakePending();
if (url != null && TryParseInvite(url, out var invite))   // the app's own strict check
    ShowJoinPrompt(invite);                                // never act on a link silently
```

Each link is returned **once**, exactly as the system delivered it — the `#fragment` included. A newer link
replaces one the game has not taken yet. The plugin does not interpret the URL: it is input from outside the
app, so check the exact shape you expect and ignore anything else.

## Integration (consuming app)

1. **Managed sources** — in the app `.csproj` (or `Directory.Build.props`):

   ```xml
   <Compile Include="$(SokolNetHome)/plugins/IncomingLinks/managed/*.cs">
       <Link>Plugins\IncomingLinks\%(Filename)%(Extension)</Link>
   </Compile>
   ```

2. **`Directory.Build.props`** — the native library, and `SokolNativeActivity`:

   ```xml
   <PropertyGroup>
      <AndroidFullscreen>true</AndroidFullscreen>
      <AndroidNativeLibrary_sokol_inlinksPath>../../plugins/IncomingLinks/libs/android</AndroidNativeLibrary_sokol_inlinksPath>
   </PropertyGroup>
   ```

3. **The intent filters** — the app declares which links open it in
   `platform/android/manifest/ActivityIntentFilters.xml` (the builder merges it **inside** `<activity>`;
   `$(Property)` tokens are substituted from `Directory.Build.props` like the other manifest fragments):

   ```xml
   <intent-filter android:autoVerify="true">
     <action android:name="android.intent.action.VIEW" />
     <category android:name="android.intent.category.DEFAULT" />
     <category android:name="android.intent.category.BROWSABLE" />
     <data android:scheme="https" android:host="example.com" android:pathPrefix="/open/" />
   </intent-filter>
   ```

4. **App Links verification** — publish `https://example.com/.well-known/assetlinks.json` (HTTP 200,
   `application/json`, no redirect) with the app's package name and the SHA-256 of **every** certificate that
   signs an installed copy: the Play App Signing key (store installs), the upload key, and the key of any
   sideloaded test build. Android 12+ verifies when the app is installed and on `pm verify-app-links
   --re-verify`; Android 11 and older verify **at install only**, and only when the device is online. Until
   verified, a link opens the browser (12+) or a chooser.

   ```bash
   adb shell pm get-app-links <package>                     # Android 12+: "example.com: verified"
   adb shell dumpsys package d                              # Android 11-: "Status: always"
   adb shell am start -a android.intent.action.VIEW -c android.intent.category.BROWSABLE \
       -d 'https://example.com/open/test#frag'              # no package: proves the verified link
   ```

A custom scheme (`<data android:scheme="myapp" />`) needs no verification, but chat apps only make
`http(s)` links tappable — use it for a web page's "Open in the app" button.

## Layout

```
managed/   IncomingLinks.cs (public API)  SokolIncomingLinks.cs (P/Invoke)
native/    sokol_inlinks.h            C ABI: const char* sokolinlinks_take(void)
           sokol_inlinks_android.c    JNI: SokolNativeActivity.takeIncomingUri() (plain NativeActivity: getIntent() once)
scripts/   build-android.sh
libs/      prebuilt outputs, built and committed by CI: android/<abi>/release/libsokol_inlinks.so
```

CI: `.github/workflows/build-inlinks-plugin.yml` builds the Android libraries (and compiles the managed layer
for every platform symbol set) on every push that touches `plugins/IncomingLinks/`, and commits them back to
`plugins/IncomingLinks/libs/` on `main`. Never commit a local build — the script is for local iteration only.
