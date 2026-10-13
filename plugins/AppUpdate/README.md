# sokol_appupdate — Google Play In-App Updates (Android)

Lets a Sokol.NET app tell its players that a newer version is on Google Play and install it without leaving
the app, using Play's own [In-App Updates](https://developer.android.com/guide/playcore/in-app-updates) API
(`com.google.android.play:app-update`). Android only: iOS has no equivalent API, and every other platform
gets a managed no-op stub (`AppUpdate.Supported == false`), so call sites need no `#if`.

The plugin exposes the **FLEXIBLE** flow: Play shows a small sheet, the update downloads in the background
while the app keeps running, and the app decides when to install it (which restarts the app).

## Model

```csharp
AppUpdate.Init();                       // once at app start
AppUpdate.Check();                      // at start and on every resume (async)
AppUpdate.Changed += s => { ... };      // the state changed (raised from Poll)

// game loop, next to your other Poll calls:
AppUpdate.Poll();

var s = AppUpdate.State;
if (s.Availability == AppUpdateAvailability.Available && s.FlexibleAllowed && /* a good moment */)
    AppUpdate.StartFlexible();          // Play's sheet; s.Flow → Showing → Accepted / Declined / Failed
if (s.Install == AppUpdateInstall.Downloaded)
    /* offer a "Restart" button */ AppUpdate.Complete();
```

- **Only a build installed from Google Play gets an answer.** A sideloaded build reads
  `Availability == Error`. Test the real flow through Play's internal app sharing or a testing track
  (the test account must have installed the app from Play once, and the installed versionCode must be
  lower than the offered one).
- Each answer from `Check()` starts **one** flow; call `Check()` again before offering again.
- The flow is started with `startUpdateFlow(info, activity, options)`, whose `Task` carries the result
  code, so the host activity needs no `onActivityResult` handling.
- Play's answers arrive on the Android UI thread and are folded into one state snapshot; `Poll()` reads it
  without JNI, so calling it every frame is cheap.

### Testing without Play

`AppUpdate.Test.UseFake(versionCode)` swaps in Play's `FakeAppUpdateManager`, which offers `versionCode`
as a FLEXIBLE update; `AppUpdate.Test.Fake(action)` then plays the user and Play — `accept`, `reject`,
`download-start`, `download-complete`, `download-fail`, `cancel-download`, `install-complete`,
`install-fail` — and `AppUpdate.Test.FakeFlags` says whether the fake's sheet (1) or install splash (2)
is "visible". `UseFake(0)` returns to the real manager. The fake draws no UI.

## Integration (consuming app)

1. **Managed sources** — in the app `.csproj` / `Directory.Build.props`:

   ```xml
   <Compile Include="$(SokolNetHome)/plugins/AppUpdate/managed/*.cs">
       <Link>Plugins\AppUpdate\%(Filename)%(Extension)</Link>
   </Compile>
   ```

2. **`Directory.Build.props`** — the `*Path` properties wire the native lib and mark the plugin ACTIVE to the
   builder, which then adds `platform/android/gradle-deps.txt` (`app-update`) to the Gradle build and compiles
   the Java helper `com.sokol.appupdate.SokolAppUpdate`:

   ```xml
   <PropertyGroup>
      <AndroidNativeLibrary_sokol_appupdatePath>../../plugins/AppUpdate/libs/android</AndroidNativeLibrary_sokol_appupdatePath>
      <AndroidJavaSource_sokolappupdatePath>../../plugins/AppUpdate/platform/android/java</AndroidJavaSource_sokolappupdatePath>
   </PropertyGroup>
   ```

## Building

The Android libraries are built by CI (`.github/workflows/build-appupdate-plugin.yml`) and committed back
to `libs/android/`. To build locally (never commit local builds):

```bash
ANDROID_NDK=/path/to/ndk ./plugins/AppUpdate/scripts/build-android.sh
```
