# Sokol.NET Plugins

Optional, self-contained add-ons that extend Sokol.NET applications.

## Available Plugins

| Plugin | Platforms | Description |
|--------|-----------|-------------|
| [Share](Share/README.md) | Android, iOS, macOS, Windows, Linux | Share score + generated image via native share sheet |
| [Ads](Ads/README.md) | Android, iOS | AdMob interstitials + UMP consent (Google Mobile Ads SDK 23 / xcframework 13) |
| [Speech](Speech/README.md) | Android, iOS, macOS, Windows, Linux, Web | Platform text-to-speech (`Speech.Say(text, lang)`), offline-voice detection, runtime-only so no voice licence attaches |
| [Billing](Billing/README.md) | Android, iOS | One-time in-app purchases (Play Billing 7 / StoreKit 2), with the store's signed proof handed to the app for its own verification |
| [Links](Links/README.md) | Android, iOS, macOS, Windows, Linux, Web | Open a link with the system handler (`Links.Open(url)`): web URLs in the default browser, `mailto:` in the mail app |

`Ads` and `Billing` have no desktop or web backend. Rather than requiring `#if` at every call site,
their managed layer stubs each call and completes it through the normal event path (queries fail,
purchases fail with `CodeUnavailable`, restore reports a query that never answered), so UI flows
compile and behave deterministically everywhere; `AdsPlugin.Available` / `Billing.Available` report
whether a real backend exists.

## Plugin Convention

Each plugin follows the same structure:

```
plugins/<PluginName>/
├── README.md
├── managed/             ← C# source (link into your .csproj)
├── native/
│   ├── CMakeLists.txt   ← Standalone shared library — builds independently of sokol
│   └── *.c / *.m / *.swift  ← C / ObjC / Swift implementation
├── platform/            ← Platform glue the builder consumes at build time (see step 4)
│   └── android/
│       ├── java/        ← JNI helper classes compiled into the APK
│       ├── manifest/    ← Providers.xml, injected before </application>; Queries.xml, injected before <application> (a <queries> block, Android 11+ package visibility)
│       ├── res/         ← Resources (e.g. FileProvider paths)
│       └── gradle-deps.txt   ← Gradle coordinates added to app/build.gradle
├── vendor/              ← Optional: third-party SDKs fetched by a script (Ads only)
├── libs/                ← Build output (self-contained, never touches repo-root libs/)
│   ├── android/<abi>/<config>/lib<name>.so
│   ├── ios/<target>/<config>/<name>.framework    (target: arm64 | simulator-arm64 | simulator-x64)
│   └── macos/<arch>/<config>/lib<name>.dylib
└── scripts/             ← One build script per platform it supports
    ├── build-macos.sh
    ├── build-ios.sh
    ├── build-android.sh
    ├── build-linux.sh
    └── build-windows.ps1
```

### Opt-in steps

1. Add managed sources to your `.csproj`:
   ```xml
   <ItemGroup>
     <Compile Include="$(SokolNetHome)/plugins/<PluginName>/managed/*.cs" />
   </ItemGroup>
   ```
2. Build the native library using the platform script in `plugins/<PluginName>/scripts/`.
   Output lands in `plugins/<PluginName>/libs/` — independent of the main sokol library.
3. Add native library paths to your `Directory.Build.props` — plus the plugin's Java sources, if it
   ships any (`platform/android/java/`, as Ads and Billing do):
   ```xml
   <AndroidNativeLibrary_<name>Path>../../plugins/<PluginName>/libs/android</AndroidNativeLibrary_<name>Path>
   <IOSNativeLibrary_<name>Path>../../plugins/<PluginName>/libs/ios/arm64/release</IOSNativeLibrary_<name>Path>
   <AndroidJavaSource_<name>Path>../../plugins/<PluginName>/platform/android/java</AndroidJavaSource_<name>Path>
   <AndroidGradleDependency_<key>>group:artifact:version</AndroidGradleDependency_<key>>
   ```
4. **The rest of `platform/android/` needs no copying.** `AndroidNativeLibrary_*Path` is also how the
   builder detects which plugins an app actually uses, and for each active plugin it pulls in
   `res/`, `gradle-deps.txt` and `manifest/Providers.xml` automatically — so several plugins can
   each contribute a manifest fragment (Share + Ads do). Fragments are appended verbatim except for
   `$(PropertyName)` / `$(PropertyName|default)` tokens, substituted from `Directory.Build.props`:
   that is how a plugin references app configuration (e.g. the AdMob application id) without
   shipping it.
5. Add native library copy targets for macOS/Windows/Linux to your `.csproj` (see SmilingFruits for an example).
