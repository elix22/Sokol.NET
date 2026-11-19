# sokol_ads — AdMob interstitials and rewarded ads (Android + iOS)

Google Mobile Ads plugin for Sokol.NET apps: **Google Mobile Ads SDK 25 + UMP 4** on
Android, **GoogleMobileAds 13 + UserMessagingPlatform 3** on iOS (the two are versioned
independently — iOS has no UMP 4). Interstitials and rewarded ads; the max ad
content rating is pinned to **G** in both shims, and `npaOnly` requests carry the
non-personalized-ads flag on every request. Desktop and web have no ad SDK — the
managed layer stubs every call deterministically (`AdsPlugin.Available` is `false`).

## Model

All calls are non-blocking; SDK callbacks are queued natively and drained from the
game loop:

```csharp
AdsPlugin.Init(npaOnly: true, childDirected: false);   // once at app start
AdsPlugin.OnEvent += e => { ... };    // Loaded / LoadFailed / Shown / Dismissed / Consent*
AdsPlugin.GatherConsent();            // UMP flow — at the FIRST ad-eligible moment, not app start
AdsPlugin.LoadInterstitial(AdsPlugin.TestInterstitialUnit);   // preload
if (AdsPlugin.InterstitialReady) AdsPlugin.ShowInterstitial(); // -> Shown, then Dismissed

// game loop, next to your other Poll calls:
AdsPlugin.Poll();
```

`Dismissed` is the important event — resume music and navigation on it. A failed
show still emits `Dismissed` after `LoadFailed`, so the resume path runs exactly once
either way.

### Rewarded ads

The player asks for one (a second chance, double coins…) and gets the reward only if they watched it:

```csharp
AdsPlugin.LoadRewarded(AdsPlugin.TestRewardedUnit);   // preload -> RewardedLoaded | RewardedLoadFailed
if (AdsPlugin.RewardedReady) AdsPlugin.ShowRewarded();  // -> RewardedShown, RewardEarned (watched), RewardedDismissed
```

- **Grant on `RewardEarned`, never on `RewardedDismissed`.** A player who closes early gets only `RewardedDismissed`;
  one who watched gets `RewardEarned` first. Resume music / navigation on `RewardedDismissed` (it always comes, a failed
  show included).
- `ShowRewarded` consumes the loaded ad — load the next one after `RewardedDismissed`.
- Events 7–11 (`RewardedLoaded`, `RewardedLoadFailed`, `RewardedShown`, `RewardedDismissed`, `RewardEarned`) share the
  interstitials' native queue; the interstitial API is unchanged.
- Verified on a Galaxy Z Flip4 from Play internal testing with Google's test unit (2026-11-19).

## Ids: application id vs unit ids

- **APPLICATION_ID is platform configuration, not an API input.**
  - Android: the plugin's manifest fragment injects
    `<meta-data com.google.android.gms.ads.APPLICATION_ID>` with the builder token
    `$(AdMobAppId_Android|<Google TEST app id>)` — set `AdMobAppId_Android` in the
    app's `Directory.Build.props` for production; unset, the Google TEST app id is
    used and production inventory can never be served by accident.
  - iOS: `IOSInfoPlistKey_GADApplicationIdentifier` in `Directory.Build.props`
    (plus an `IOSInfoPlistRawFragment_SKAdNetworkItems` array).
- **Ad unit ids are runtime parameters** of `LoadInterstitial` / `LoadRewarded`.
  `AdsPlugin.TestInterstitialUnit` / `AdsPlugin.TestRewardedUnit` are Google's published test units for the
  platform — use them in every non-store build (clicking real ads in dev gets the account banned).

## Integration (consuming app)

```xml
<!-- Directory.Build.props -->
<PropertyGroup>
   <AndroidNativeLibrary_sokol_adsPath>../../plugins/Ads/libs/android</AndroidNativeLibrary_sokol_adsPath>
   <AndroidJavaSource_sokoladsPath>../../plugins/Ads/platform/android/java</AndroidJavaSource_sokoladsPath>
   <AdMobAppId_Android></AdMobAppId_Android>   <!-- empty = Google test app id -->
   <IOSNativeLibrary_sokol_adsPath>../../plugins/Ads/libs/ios/arm64/release</IOSNativeLibrary_sokol_adsPath>
   <IOSNativeLibrary_GoogleMobileAdsPath>../../plugins/Ads/vendor/ios/device</IOSNativeLibrary_GoogleMobileAdsPath>
   <IOSInfoPlistKey_GADApplicationIdentifier>ca-app-pub-3940256099942544~1458002511</IOSInfoPlistKey_GADApplicationIdentifier>
</PropertyGroup>
<ItemGroup>
   <Compile Include="$(SokolNetHome)/plugins/Ads/managed/*.cs" />
</ItemGroup>
```

The `AndroidNativeLibrary_*` path marks the plugin ACTIVE: the builder then
auto-injects `platform/android/gradle-deps.txt` (play-services-ads + UMP) and the
manifest fragment. `AndroidJavaSource_*` compiles `com.sokol.ads.SokolAds` into the
APK.

**iOS SDK is fetched, not vendored** (Google's SDK; `vendor/` is gitignored):

```bash
./plugins/Ads/scripts/fetch-googlemobileads-ios.sh            # first run: downloads + extracts
./plugins/Ads/scripts/fetch-googlemobileads-ios.sh --force    # UPDATE to Google's current SDK
```

`vendor/ios/device/` then holds `GoogleMobileAds.framework` +
`UserMessagingPlatform.framework` for the builder's framework embedding (one
`IOSNativeLibrary_*Path` covers both — the builder globs `*.framework`). An app can
instead point `IOSStaticFrameworks_*Path` at `vendor/ios` itself to link the
`.xcframework`s into the app executable; that wiring does not use `device/`.

⛔ **`--force` is the only way to update.** Google serves this zip from an unversioned
url, so a plain re-run cannot tell a current SDK from a years-old one — it reports the
installed version and stops. Without `--force` you stay on whatever was fetched first,
however long ago.

## Layout

```
managed/   AdsPlugin.cs (public API + events)  SokolAds.cs (P/Invoke)
native/    sokol_ads.h            C ABI (poll model, int-only events)
           sokol_ads_queue.c      shared lock-protected event ring + ready flag
           sokol_ads_android.c    JNI bridge -> com.sokol.ads.SokolAds
           sokol_ads_ios.m        GoogleMobileAds + UMP (ObjC, ARC)
platform/android/
           gradle-deps.txt        play-services-ads + user-messaging-platform
           manifest/Providers.xml APPLICATION_ID meta-data ($(Prop|default) token)
           java/com/sokol/ads/SokolAds.java
scripts/   build-android.sh  build-ios.sh  fetch-googlemobileads-ios.sh
libs/      prebuilt outputs (committed): android/<abi>/release/libsokol_ads.so,
           ios/arm64/{debug,release}/sokol_ads.framework
vendor/    (gitignored) fetched Google SDK xcframeworks + device slices
```

## Builds (CI)

`.github/workflows/build-ads-plugin.yml` builds Android (armeabi-v7a, arm64-v8a, x86_64) and iOS on every change under
`plugins/Ads/` (except `libs/`) and commits the libraries back to `libs/` — every shipped library is a CI build, never a
local one. The 64-bit Android libraries are linked with 16 KB page alignment (`-Wl,-z,max-page-size=16384`, Google Play for
Android 15+); the workflow fails if they are not.
