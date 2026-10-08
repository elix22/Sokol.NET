# sokol_net — WebSocket + WebRTC data channels (Android · iOS · macOS)

Networking plugin for Sokol.NET apps, built on [libdatachannel](https://github.com/paullouisageneau/libdatachannel)
(v0.24.6) with [Mbed TLS](https://github.com/Mbed-TLS/mbedtls) (3.6 LTS) as its TLS/DTLS backend:

- **WebSocket client** — `ws://` and `wss://` with certificate verification (TLS 1.2).
- **WebRTC data channels** — peer-to-peer over ICE (host / STUN / TURN-over-UDP candidates), reliable-ordered
  or unordered/partially-reliable channels. No audio/video (built with `NO_MEDIA`).
- **Bundled CA roots** — Mozilla's root store (`certs/cacert.pem`), embedded in the library and returned by
  `sokolnet_ca_bundle()`. Mbed TLS has no system roots of its own, so pass it for every `wss://` connection.

| Platform | Library | Notes |
|---|---|---|
| Android | `libs/android/<abi>/release/libsokol_net.so` | API 26+, c++_static inside, 16 KB page-aligned on arm64-v8a / x86_64 |
| iOS | `libs/ios/<target>/release/sokol_net.framework` | iOS 15.0+ |
| macOS | `libs/macos/<arch>/release/libsokol_net.dylib` | macOS 11.0+ |
| Windows, Linux, Web | — | not yet |

Each library exports only libdatachannel's C API (`rtc*`, see `ext/libdatachannel/include/rtc/rtc.h`) and the
`sokolnet_*` helpers (`native/sokol_net.h`). The C# bindings are in `managed/SokolNet.cs`.

## Model

```csharp
using static SokolNet;

var cfg = new rtcWsConfiguration { caCertificatePemFile = sokolnet_ca_bundle(), connectionTimeoutMs = 10000 };
int ws = rtcCreateWebSocketEx("wss://example.com/socket", cfg);
rtcSetOpenCallback(ws, (IntPtr)(delegate* unmanaged[Cdecl]<int, IntPtr, void>)&OnOpen);
rtcSetMessageCallback(ws, (IntPtr)(delegate* unmanaged[Cdecl]<int, IntPtr, int, IntPtr, void>)&OnMessage);
// … SokolNet.Send(ws, bytes); rtcDeleteWebSocket(ws);
```

⛔ **Threading.** libdatachannel calls every callback on its own threads (ordered per PeerConnection and per
channel). Callbacks must be `[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]` static methods
that copy what they need and hand it to the app's thread (e.g. a `ConcurrentQueue` drained each frame). Never
call `rtcCleanup` from a callback.

Limits of this build, by design of libjuice (libdatachannel's ICE agent):
- **TURN over UDP only.** `turns:` and `?transport=tcp` entries are skipped with a warning. On networks that block
  UDP, use a WebSocket fallback.
- **No ICE restart.** After a network change, create a new PeerConnection.
- TURN credentials are URL-decoded: write a `:` inside a username as `%3A`.

## Integration (consuming app)

1. **Managed sources** — in the app `.csproj` (or `Directory.Build.props`):
   ```xml
   <Compile Include="$(SokolNetHome)/plugins/Net/managed/*.cs">
       <Link>Plugins\Net\%(Filename)%(Extension)</Link>
   </Compile>
   ```
   The bindings compile for `__ANDROID__`, `__IOS__` and `__MACOS__`; other builds get nothing. The project needs
   `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` for the function-pointer callbacks.
2. **`Directory.Build.props`** — native library paths for the APK / app bundle:
   ```xml
   <PropertyGroup>
      <AndroidNativeLibrary_sokol_netPath>../../plugins/Net/libs/android</AndroidNativeLibrary_sokol_netPath>
      <IOSNativeLibrary_sokol_netPath>../../plugins/Net/libs/ios/arm64/release</IOSNativeLibrary_sokol_netPath>
   </PropertyGroup>
   ```
3. **macOS** — copy `libs/macos/$(OSArch)/release/libsokol_net.dylib` next to the executable in the app's
   `CopyCustomContent*` targets (as for the other plugins).
4. **Android** — the app needs the `INTERNET` permission.

## Building

```bash
git submodule update --init --recursive ext/libdatachannel ext/mbedtls
./plugins/Net/scripts/build-macos.sh   [arm64|x86_64|all]
./plugins/Net/scripts/build-ios.sh     [device|simulator-arm64|simulator-x64|all]
ANDROID_NDK=/path/to/ndk ./plugins/Net/scripts/build-android.sh [abi…]
```

The scripts build Mbed TLS and libdatachannel as static libraries (in `$SOKOLNET_BUILD_DIR`, default
`$TMPDIR/sokol-net-build`), then link them with `native/` into the one library above. The shipped libraries are
built by CI (`.github/workflows/build-net-plugin.yml`).

- `native/mbedtls_user_config.h` — the Mbed TLS options libdatachannel needs (DTLS-SRTP, threading); used for both
  builds so they agree.
- `patches/*.patch` — applied to `ext/libdatachannel` for the build and reverted afterwards (the submodule stays
  clean). `0001` exposes `caCertificatePemFile` in `rtcWsConfiguration` (the C API had no way to pass CA roots).
- `certs/cacert.pem` — Mozilla's CA certificates as extracted by the curl project (https://curl.se/docs/caextract.html).
  Update it from there, check its published SHA-256, and rebuild.

## Licences

| Component | Licence |
|---|---|
| libdatachannel, libjuice | MPL-2.0 |
| usrsctp | BSD-3-Clause |
| Mbed TLS | Apache-2.0 |
| plog, nlohmann/json | MIT |
| `certs/cacert.pem` (Mozilla CA certificate data) | MPL-2.0 |

Apps shipping this plugin must include these notices. The source of every MPL-2.0 file, including the patch in
`patches/`, is available in this repository and upstream.
