# Store Packaging

What a project sets, and what SokolApplicationBuilder is asked, to get the packages the stores take:
a signed Android App Bundle (`.aab`) for the Play Store and a signed `.ipa` for the App Store.

Everything here is optional. A project that sets none of these properties builds exactly as before.

Related guides: [PACKAGE_PREFIX_CONFIGURATION.md](PACKAGE_PREFIX_CONFIGURATION.md),
[APP_VERSION_CONFIGURATION.md](APP_VERSION_CONFIGURATION.md), [AAB_BUILD_GUIDE.md](AAB_BUILD_GUIDE.md).

## Identity and version

In the project's `Directory.Build.props`:

```xml
<PropertyGroup>
   <AppVersion>1.2.3</AppVersion>

   <!-- Android: the id is {AndroidPackagePrefix}.{AndroidPackageName} -->
   <AndroidPackagePrefix>com.company</AndroidPackagePrefix>
   <AndroidPackageName>game</AndroidPackageName>
   <AndroidDisplayName>My Game</AndroidDisplayName>
   <AndroidVersionCode>7</AndroidVersionCode>

   <!-- iOS: the id is {IOSBundlePrefix}.{IOSBundleName} -->
   <IOSBundlePrefix>com.company</IOSBundlePrefix>
   <IOSBundleName>game</IOSBundleName>
   <IOSDisplayName>My Game</IOSDisplayName>
   <IOSBuildNumber>42</IOSBuildNumber>
   <IOSDevelopmentTeam>ABCDE12345</IOSDevelopmentTeam>
</PropertyGroup>
```

| Property | What it sets | Without it |
|---|---|---|
| `AndroidPackageName` | The last part of the application id. | The project's name: `{AndroidPackagePrefix}.{project}`. |
| `AndroidDisplayName` | The name under the icon (`android:label`). Spaces and any characters are fine. | The project's name. |
| `AndroidVersionCode` | The build's number for the Play Store. Every upload needs a higher one. | `1`. |
| `IOSBundleName` | The last part of the bundle id. | The id is `com.elix22.{project}-ios-app`. |
| `IOSDisplayName` | The name under the icon. | `IOSBundleName`, else `{project} iOS App`. |
| `IOSBuildNumber` | `CFBundleVersion`. App Store Connect refuses a number it has seen for the same version. | The app version. |
| `IOSDevelopmentTeam` | The team that signs the app. | `--development-team`, else the team used for the project last. |

An id that is published can never change. A project can be renamed: set `AndroidPackageName` and
`IOSBundleName` before the first upload, so that the id does not follow the project's name.

## Android: the keystore

A release build is signed. By default the builder uses one keystore for the whole machine: it asks
for it once, on the console, and keeps the answer in `~/.sokolnet_config/release_keystore.config`.

A project can name its own keystore instead:

```xml
<PropertyGroup>
   <AndroidKeystore>../keys/game.keystore</AndroidKeystore>
   <AndroidKeyAlias>upload</AndroidKeyAlias>
</PropertyGroup>
```

- `AndroidKeystore` is a path relative to the project folder, an absolute path, or one that starts
  with `~`.
- The passwords are never read from a file. They come from the environment:
  `ANDROID_KEYSTORE_PASSWORD`, and `ANDROID_KEY_PASSWORD` when the key's password differs.
- Nothing is asked on the console. A keystore that is missing, an alias that is not set, or a
  password that is not in the environment stops the build with an error.

```bash
ANDROID_KEYSTORE_PASSWORD=... dotnet run --project tools/SokolApplicationBuilder -- \
    --task build --architecture android --type release --subtask aab --path /path/to/project
```

The bundle is written to `output/Android/release/{project}-release.aab`.

A release that cannot be signed fails: the builder ends with exit code 1 and leaves no package in
the output folder. The signing tools get the passwords through their environment, so a password
is not on a command line and not in the build's log.

To make a keystore:

```bash
keytool -genkeypair -keystore game.keystore -alias upload -keyalg RSA -keysize 2048 -validity 10000
```

Keep the file and its password. Without them the app can never be updated.

## iOS: the .ipa

```bash
dotnet run --project tools/SokolApplicationBuilder -- \
    --task build --architecture ios --type release --path /path/to/project \
    --subtask ipa --export-method app-store-connect
```

After the usual build, `--subtask ipa` archives the Xcode project the build generated and exports
the archive. The package is written to `output/iOS/release/{project}-release.ipa`, beside the
`.app`. The app's code is not compiled a second time.

| `--export-method` | For | Needs |
|---|---|---|
| `app-store-connect` (default) | The App Store and TestFlight. | An Apple Distribution certificate. The app's id is registered in the developer account when it is not there yet. |
| `release-testing` | Ad hoc installs on registered devices. | An Apple Distribution certificate. |
| `debugging` | Your own devices. A way to try the whole path. | An Apple Development certificate. |
| `enterprise` | In-house distribution. | An enterprise account. |

Signing is automatic, by the development team: Xcode fetches or makes the certificate and the
provisioning profile the method needs (`-allowProvisioningUpdates`).

The logs of the two steps are in the project's `ios/ipa/` folder (`archive.log`, `export.log`).
When a step fails, the end of its log is printed.

With `--install`, the app is installed on the device first, and the package is made after it.

Uploading the package is not part of the build: use Xcode's Organizer, Transporter, or
`xcrun altool`.
