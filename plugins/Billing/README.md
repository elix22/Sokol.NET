# sokol_billing — one-time and consumable in-app purchases (Android + iOS)

Store billing plugin for Sokol.NET apps: **Google Play Billing Library 9** on
Android, **StoreKit 2** on iOS. One-time (non-consumable) and consumable products.
Desktop and web have no store — the managed layer stubs every call so UI flows
compile and behave deterministically on all platforms (`Billing.Available` is
`false` there).

## Model

All calls are non-blocking. Store SDK callbacks arrive on platform threads and
are queued natively; the app drains them from its game loop:

```csharp
Billing.Init();                      // once at app start; owned purchases replay as PurchaseOk
Billing.OnEvent += e => { ... };     // ProductInfo / PurchaseOk / PurchaseCancelled / ...
Billing.QueryProduct("my_sku");      // -> ProductInfo (localized price) | ProductFailed
Billing.Purchase("my_sku");          // -> PurchaseOk | PurchaseCancelled | PurchaseFailed
Billing.Restore();                   // -> PurchaseOk per owned SKU, then RestoreDone
Billing.FinishConsumable("coins_s"); // a CONSUMABLE, once granted -> Consumed (Code 0 = can be bought again)

// game loop, next to your other Poll calls:
Billing.Poll();
```

`PurchaseOk` carries the store's verification material for the app's own
(offline) entitlement check — the plugin never interprets or persists it:

- **Android**: `Proof` = the purchase's original JSON, `Signature` = its RSA
  signature (verify against your Play licensing public key).
- **iOS**: `Proof` = the StoreKit 2 JWS representation (Apple-signed),
  `Signature` = null.

Acknowledge/finish semantics live in the plugin: Play purchases are
acknowledged as soon as they are reported (unacknowledged purchases refund
after 3 days), StoreKit transactions are finished after they are reported.

### Consumables (coin bundles and the like)

The store does not know a consumable from a one-time product — the app decides, by calling `FinishConsumable`:

1. On `PurchaseOk` for a consumable SKU: verify the proof, grant it **once per purchase** (persist the order id from the
   proof first — the same purchase replays as `PurchaseOk` on every connect until it is consumed), then
2. `Billing.FinishConsumable(sku)` → `Consumed`. `Code == 0`: consumed, the SKU can be bought again. Any other code: still
   owned — it replays on the next connect, so the grant must stay idempotent. Retrying after a few seconds is fine (seen on
   a Z Flip: SERVICE_UNAVAILABLE right after the purchase, OK on the retry).
3. ⛔ Never call it for a non-consumable — on Play that would take the product away.

Android: `consumeAsync` on the SKU's owned purchase (`ITEM_NOT_OWNED` when there is none). iOS: StoreKit 2 has already
finished the transaction when `PurchaseOk` was emitted, so `Consumed` answers at once with `Code == 0`.

### Prices at app start

Right after `Init` the store is not connected yet: a `QueryProduct` made then answers `ProductFailed` with
SERVICE_UNAVAILABLE (2). Ask again once `SyncDone` arrives with `Code == 0` (the store is connected), or when the store
screen opens.

## Integration (consuming app)

1. **Managed sources** — in the app `.csproj`:

   ```xml
   <Compile Include="$(SokolNetHome)/plugins/Billing/managed/*.cs">
       <Link>Plugins\Billing\%(Filename)%(Extension)</Link>
   </Compile>
   ```

2. **`Directory.Build.props`** — the `*Path` properties both wire the native
   lib and mark the plugin ACTIVE to the builder (which then auto-injects
   `platform/android/gradle-deps.txt`):

   ```xml
   <PropertyGroup>
      <AndroidNativeLibrary_sokol_billingPath>../../plugins/Billing/libs/android</AndroidNativeLibrary_sokol_billingPath>
      <AndroidJavaSource_sokolbillingPath>../../plugins/Billing/platform/android/java</AndroidJavaSource_sokolbillingPath>
      <IOSNativeLibrary_sokol_billingPath>../../plugins/Billing/libs/ios/arm64/release</IOSNativeLibrary_sokol_billingPath>
   </PropertyGroup>
   ```

   (`AndroidJavaSource_*` is required — the Play Billing integration lives in
   `com.sokol.billing.SokolBilling`, compiled into the APK by the builder.)

3. **Store setup** — products are created in Play Console / App Store Connect
   under the app's package/bundle id; the SKU strings are runtime parameters
   of the API, not build config. No manifest or Info.plist entries are needed:
   the Play Billing AAR merges its own `com.android.vending.BILLING`
   permission, and StoreKit needs nothing.

4. **Sandbox testing** — Play: upload to an internal-testing track + license
   testers; iOS: a Sandbox Apple ID (Settings → App Store → Sandbox Account).
   Real purchases only work when the store knows the exact signed build/bundle.

## Layout

```
managed/   Billing.cs (public API + events)  SokolBilling.cs (P/Invoke)
native/    sokol_billing.h            C ABI (event queue, poll model)
           sokol_billing_queue.c      shared lock-protected event ring
           sokol_billing_android.c    JNI bridge -> com.sokol.billing.SokolBilling
           sokol_billing_ios.swift    StoreKit 2 shim (@_cdecl entry points)
platform/android/
           gradle-deps.txt            com.android.billingclient:billing
           java/com/sokol/billing/SokolBilling.java
scripts/   build-android.sh  build-ios.sh
libs/      prebuilt outputs (committed): android/<abi>/release/libsokol_billing.so,
           ios/<target>/{debug,release}/sokol_billing.framework
```

Rebuild native libs after changing `native/`:

```bash
export ANDROID_NDK=...            # for Android
./plugins/Billing/scripts/build-android.sh
./plugins/Billing/scripts/build-ios.sh          # device + both simulators
```

iOS note: the framework's deployment target is 15.0 (the StoreKit 2 floor) and
it contains Swift — apps targeting older iOS must not call into it there.

## Builds (CI)

`.github/workflows/build-billing-plugin.yml` builds Android (armeabi-v7a, arm64-v8a, x86_64) and iOS on every change under
`plugins/Billing/` (except `libs/`) and commits the libraries back to `libs/` — every shipped library is a CI build, never a
local one. The 64-bit Android libraries are linked with 16 KB page alignment (Google Play, Android 15+); the workflow fails if
they are not. Verified on a Galaxy Z Flip4 from Play internal testing (2026-11-19): one-time and consumable test purchases,
a refund revoked.
