# sokol_review — the store's own rating prompt (Android · iOS)

Asks the store to show **its own** rating prompt inside the app: Google Play
[In-App Review](https://developer.android.com/guide/playcore/in-app-review) on Android
(`com.google.android.play:review`) and StoreKit's
[`SKStoreReviewController`](https://developer.apple.com/documentation/storekit/skstorereviewcontroller) on iOS. Every other
platform gets a managed no-op stub (`Review.Supported == false`), so call sites need no `#if`.

| Platform | API | Notes |
|---|---|---|
| Android | `ReviewManager.requestReviewFlow()` → `launchReviewFlow()` (Java helper `com.sokol.review.SokolReview`) | Play applies a time-bound quota; only a build installed from Google Play can show the card |
| iOS | `SKStoreReviewController requestReviewInScene:` (iOS 14+, the foreground window scene), `requestReview` before | at most 3 times in 365 days per user; a development-signed build always shows it and submits nothing; TestFlight never shows it |

## Model

```csharp
if (Review.Supported && /* a natural pause after a positive moment */)
    Review.Request();          // async; the store decides whether the prompt really appears
// Review.State: Idle → Requesting → Done (or Failed, Review.ErrorCode on Android)
```

**Neither store reports whether its prompt was shown or what the user chose** — by design. `Done` means the store's flow
finished. Do not build logic on the answer.

**Store rules are the app's job** (both stores' guidance): ask after a positive moment at a natural pause — never mid-task,
never at first launch, never from a button (a "Rate us" button must open the store page instead, e.g. the Links plugin with
`https://apps.apple.com/app/id<ID>?action=write-review` / the Play listing), and not more often than the store would show it.

### Testing

`Review.Test.UseFake(true)` (Android) swaps in Play's `FakeReviewManager`: the whole flow completes with no UI and **no call to
Play**, so a sideloaded or test build can prove the plumbing. iOS has no fake.

## Integration (consuming app)

1. **Managed sources** — in the app `.csproj` / `Directory.Build.props`:

   ```xml
   <Compile Include="$(SokolNetHome)/plugins/Review/managed/*.cs">
       <Link>Plugins\Review\%(Filename)%(Extension)</Link>
   </Compile>
   ```

2. **`Directory.Build.props`** — the `*Path` properties wire the native libraries and mark the plugin ACTIVE to the builder,
   which then adds `platform/android/gradle-deps.txt` (`review`) to the Gradle build and compiles the Java helper:

   ```xml
   <PropertyGroup>
      <AndroidNativeLibrary_sokol_reviewPath>../../plugins/Review/libs/android</AndroidNativeLibrary_sokol_reviewPath>
      <AndroidJavaSource_sokolreviewPath>../../plugins/Review/platform/android/java</AndroidJavaSource_sokolreviewPath>
      <IOSNativeLibrary_sokol_reviewPath>../../plugins/Review/libs/ios/arm64/release</IOSNativeLibrary_sokol_reviewPath>
   </PropertyGroup>
   ```

## Building the native libraries

CI (`.github/workflows/build-review-plugin.yml`) builds Android (three ABIs, 64-bit 16 KB aligned) and iOS (device + both
simulators) on every change and commits them to `libs/`. Locally: `scripts/build-android.sh` (needs `ANDROID_NDK` and the
prebuilt `libsokol.so`) and `scripts/build-ios.sh [device|simulator-arm64|simulator-x64|all]`.
