# Notifications plugin

Local (on-device) scheduled notifications: the app schedules its own text for a time, and the OS shows it even when
the app isn't running. No push service, no server, no SDK account — nothing leaves the device.

| Platform | Backend |
|----------|---------|
| Android 8+ (API 26) | WorkManager one-time work posts the notification (survives reboots and app updates; time approximate) |
| iOS 15+ | `UNUserNotificationCenter` with a calendar trigger (fires on time, survives reboots) |
| Desktop, Web | none — `Notifications.Available == false`, permission reads `Denied`, calls complete as no-ops |

## API

```csharp
Notifications.Init();                                    // once at start
Notifications.Configure("Reminders", "icons/notify.png"); // Android: channel name + small icon (asset path)
Notifications.OnEvent += e => { /* Permission / Pending / PendingDone */ };
// every frame:
Notifications.Poll();

if (Notifications.Permission == NotificationPermission.NotDetermined)
    Notifications.RequestPermission();                   // → Permission event (Granted / Denied)

Notifications.Schedule(id: 1, at: DateTimeOffset.Now.AddDays(2), expires: null, "My App", "Time for a game?");
Notifications.Cancel(1);
Notifications.CancelAll();

int tapped = Notifications.TakeOpened();                 // id the user tapped to open the app, once (0 = none)
Notifications.OpenSettings();                            // after a denial
```

- **Ids** are chosen by the app, 1..63 (iOS keeps at most 64 pending requests per app). Scheduling a pending id
  replaces it. `Cancel` / `CancelAll` also remove a notification already shown.
- **Text** comes from the app, already localized — the plugin holds none.
- **Android timing:** Doze and battery savers can run the work late. Pass `expires` to drop a notification that
  would arrive too late (e.g. at night) instead of showing it.
- **The app in front:** iOS shows nothing while the app is in the foreground; Android shows it.
- **`TakeOpened`:** check it at start and on every return to the foreground (`SAPP_EVENTTYPE_RESUMED`). Android
  delivers a tap on a running app through `onNewIntent`, which needs the `singleTask` `SokolNativeActivity`
  (`AndroidFullscreen`, the default).

## Permission

- **Android 13+** needs the `POST_NOTIFICATIONS` runtime permission. Declare it, and keep it out of the generic
  launch-time prompt so the app can ask at a moment that makes sense:
  ```xml
  <AndroidPermissions>…;android.permission.POST_NOTIFICATIONS</AndroidPermissions>
  <AndroidPermissionsNoAutoRequest>android.permission.POST_NOTIFICATIONS</AndroidPermissionsNoAutoRequest>
  ```
  Android shows no dialog at all after two refusals; `RequestPermission` then answers `Denied` and the app should
  offer `OpenSettings()`.
- **Android 8–12:** no prompt; notifications are on unless the user switched them off.
- **iOS:** `requestAuthorization(.alert, .sound)`. No entitlement, no APNs, no `UIBackgroundModes`.

## Opt-in (Directory.Build.props)

```xml
<AndroidNativeLibrary_sokol_notificationsPath>../../plugins/Notifications/libs/android</AndroidNativeLibrary_sokol_notificationsPath>
<AndroidJavaSource_sokolnotificationsPath>../../plugins/Notifications/platform/android/java</AndroidJavaSource_sokolnotificationsPath>
<IOSNativeLibrary_sokol_notificationsPath>../../plugins/Notifications/libs/ios/arm64/release</IOSNativeLibrary_sokol_notificationsPath>
```
```xml
<Compile Include="$(SokolNetHome)/plugins/Notifications/managed/*.cs" />
```
`androidx.work:work-runtime` comes in through `platform/android/gradle-deps.txt` automatically.

## Building

The shipped libraries are built by CI (`.github/workflows/build-notifications-plugin.yml`). Local builds, for
development only: `scripts/build-android.sh` (needs `ANDROID_NDK` and the prebuilt `libsokol.so`) and
`scripts/build-ios.sh [device|simulator-arm64|simulator-x64|all]`.
