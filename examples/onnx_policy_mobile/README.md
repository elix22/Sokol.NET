# onnx_policy_mobile

Runs two exported robot control policies (ONNX) with [onnxruntime](https://github.com/microsoft/onnxruntime)
1.31.1 on random observations and reports the inference latency, on desktop, Android and iOS (NativeAOT).
Results are drawn on screen and written to the system log (logcat tag `SOKOL`, iOS syslog / device console, stderr).

| Asset | Policy | Input → output |
|---|---|---|
| `Assets/inverted_pendulum_policy.onnx` | PPO, 2 × 64 Tanh, Gymnasium InvertedPendulum MJCF | `obs [batch, 4]` → `action [batch, 1]` |
| `Assets/hopper_policy.onnx` | PPO, 2 × 64 Tanh, Gymnasium Hopper MJCF | `obs [batch, 11]` → `action [batch, 3]` |

Each graph takes RAW observations (the observation normaliser is folded in) and clamps the action to the
actuator ctrl range.

Per model the app prints the session load time, a checksum (sum of the actions for 16 observations from a
seeded `System.Random`, identical on every platform), and mean / p50 / p99 / min latency of 2 000 timed
`Run` calls after 200 warm-up calls, at batch 1 and batch 64. CPU execution provider, one intra-op thread,
input and output tensors pinned once and reused. When it is done it saves one screenshot with
`ScreenCapture` (iOS: the app's `Documents/onnx_policy_mobile.png`; desktop: next to the binary).

## Build

```bash
# Desktop (JIT)
dotnet run --project examples/onnx_policy_mobile/onnx_policy_mobile.csproj

# Android / iOS (NativeAOT)
dotnet run --project tools/SokolApplicationBuilder -- --task build --architecture android --type release --path $PWD/examples/onnx_policy_mobile --install --run
dotnet run --project tools/SokolApplicationBuilder -- --task build --architecture ios --type release --path $PWD/examples/onnx_policy_mobile --compile --install --run --development-team <TEAM_ID>

# iOS screenshot
ios-deploy --id <udid> --bundle_id <bundle id> --download=/Documents/onnx_policy_mobile.png --to <dir>
```

How the native library gets in (see `onnx_policy_mobile.csproj`):

- **Desktop**: the NuGet package's `runtimes/<rid>/native` library is copied next to the app.
- **Android**: the package ships `onnxruntime.aar`; `ExtractOnnxRuntimeNative` unzips it to `obj/onnxruntime/android`
  and `AndroidNativeLibrary_OnnxRuntimePath` (Directory.Build.props) puts `libonnxruntime.so` into the APK
  (~34 MB per ABI).
- **iOS**: the package ships a static `onnxruntime.xcframework`; it is unzipped to `obj/onnxruntime/ios` and linked
  into the NativeAOT library (`DirectPInvoke onnxruntime!OrtGetApiBase` + `-lc++ -lsqlite3`, UIKit, Foundation,
  weak CoreML). Minimum iOS 15.1 (the framework's deployment target).

No web build: onnxruntime's WebAssembly flavour is a separate (JavaScript) package.

Licence: onnxruntime is MIT (Microsoft). The Gymnasium MJCF models the policies were trained on are MIT.
