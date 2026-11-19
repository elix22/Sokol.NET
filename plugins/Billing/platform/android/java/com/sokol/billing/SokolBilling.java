package com.sokol.billing;

import android.app.Activity;

import com.android.billingclient.api.AcknowledgePurchaseParams;
import com.android.billingclient.api.BillingClient;
import com.android.billingclient.api.BillingClientStateListener;
import com.android.billingclient.api.BillingFlowParams;
import com.android.billingclient.api.BillingResult;
import com.android.billingclient.api.ConsumeParams;
import com.android.billingclient.api.PendingPurchasesParams;
import com.android.billingclient.api.ProductDetails;
import com.android.billingclient.api.Purchase;
import com.android.billingclient.api.QueryProductDetailsParams;
import com.android.billingclient.api.QueryPurchasesParams;

import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Play Billing helper for the sokol_billing plugin. Called from native code
 * (sokol_billing_android.c); results flow back through nativeOnEvent into the
 * plugin's C event queue, drained by the app's game thread.
 *
 * One-time (INAPP) products, non-consumable and consumable. Purchases are acknowledged here as soon as
 * they are reported PURCHASED (Play refunds unacknowledged purchases after
 * three days); the app persists the proof from the same event.
 */
public final class SokolBilling {

    /** Upcall into sokol_billing_android.c — safe from any thread. */
    static native void nativeOnEvent(int type, int code, String sku, String price,
                                     String proof, String signature);

    /* Mirror of sokolbilling_event_type in sokol_billing.h. */
    static final int EV_PRODUCT_INFO       = 1;
    static final int EV_PRODUCT_FAILED     = 2;
    static final int EV_PURCHASE_OK        = 3;
    static final int EV_PURCHASE_CANCELLED = 4;
    static final int EV_PURCHASE_FAILED    = 5;
    static final int EV_RESTORE_DONE       = 6;
    static final int EV_SYNC_DONE          = 7;
    static final int EV_CONSUMED           = 8;

    static Activity activity;
    /** Created on the UI thread by init(), read from the game thread. */
    static volatile BillingClient client;
    static final Map<String, ProductDetails> products = new HashMap<>();

    private SokolBilling() {}

    public static void init(final Activity act) {
        activity = act;
        act.runOnUiThread(() -> {
            if (client != null) return;
            client = BillingClient.newBuilder(act)
                .enablePendingPurchases(
                    PendingPurchasesParams.newBuilder().enableOneTimeProducts().build())
                // ⛔ Load-bearing. Billing 8+ re-establishes a dropped connection itself,
                // right before the API call that needs it, and if it cannot, that call's own
                // callback still fires with SERVICE_DISCONNECTED. That library guarantee is
                // what lets every entry point below call straight through with no readiness
                // queue — and a queue is exactly what once stranded a restore forever when
                // the connection failed offline (user-reported 2026-08-01, airplane mode).
                // A caller must always get an answer; here the library owes it, not us.
                .enableAutoServiceReconnection()
                .setListener(SokolBilling::onPurchasesUpdated)
                .build();
            connect();
        });
    }

    /** Only the FIRST connection is ours to make; every later one is automatic. */
    static void connect() {
        client.startConnection(new BillingClientStateListener() {
            @Override public void onBillingSetupFinished(BillingResult r) {
                if (r.getResponseCode() != BillingClient.BillingResponseCode.OK) return;
                /* Replay owned purchases so the app's entitlement cache heals
                   (refunds/family changes reconcile the same way on resume). */
                queryOwned(false);
            }
            @Override public void onBillingServiceDisconnected() {
                // No-op by design: Play's guidance for enableAutoServiceReconnection is to
                // NOT call startConnection() here. Racing the library's own reconnect would
                // put two connection attempts in flight.
            }
        });
    }

    /** True (having already answered the caller) if init() has not built the client yet.
        A call that races app startup must still get an event — and must not NPE the
        game thread, where an uncaught exception takes the whole app down. */
    static boolean notInitialised(int failEvent, String sku) {
        if (client != null) return false;
        nativeOnEvent(failEvent, BillingClient.BillingResponseCode.SERVICE_UNAVAILABLE,
                      sku, null, null, null);
        return true;
    }

    public static void queryProduct(final String sku) {
        if (notInitialised(EV_PRODUCT_FAILED, sku)) return;
        queryDetails(sku, (details, code) -> {
            if (details != null) {
                ProductDetails.OneTimePurchaseOfferDetails offer =
                    details.getOneTimePurchaseOfferDetails();
                nativeOnEvent(EV_PRODUCT_INFO, 0, sku,
                    offer != null ? offer.getFormattedPrice() : "", null, null);
            } else {
                nativeOnEvent(EV_PRODUCT_FAILED, code, sku, null, null, null);
            }
        });
    }

    public static void purchase(final String sku) {
        if (notInitialised(EV_PURCHASE_FAILED, sku)) return;
        queryDetails(sku, (details, code) -> {
            if (details == null) {
                // The store answering OK without the product means it really is unavailable;
                // any other code is the transport failing, and the caller wants to see that.
                nativeOnEvent(EV_PURCHASE_FAILED,
                    code == BillingClient.BillingResponseCode.OK
                        ? BillingClient.BillingResponseCode.ITEM_UNAVAILABLE : code,
                    sku, null, null, null);
                return;
            }
            activity.runOnUiThread(() -> {
                BillingFlowParams params = BillingFlowParams.newBuilder()
                    .setProductDetailsParamsList(Collections.singletonList(
                        BillingFlowParams.ProductDetailsParams.newBuilder()
                            .setProductDetails(details)
                            .build()))
                    .build();
                BillingResult r = client.launchBillingFlow(activity, params);
                if (r.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                    nativeOnEvent(EV_PURCHASE_FAILED, r.getResponseCode(), sku, null, null, null);
                }
                /* OK -> the result arrives in onPurchasesUpdated. */
            });
        });
    }

    public static void restore() {
        if (notInitialised(EV_RESTORE_DONE, null)) return;
        queryOwned(true);
    }

    /** Background re-enumeration (app foregrounded). Same query as restore(), but it completes
        with SYNC_DONE so a consumer never mistakes it for the answer to a user's Restore tap. */
    public static void sync() {
        if (notInitialised(EV_SYNC_DONE, null)) return;
        queryOwned(false);
    }

    /** TEST TOOL: consume the owned purchase of `sku`, so Play stops reporting it
        (to the app the next sync is indistinguishable from a revocation) and the
        SKU becomes purchasable again. Entitlement products are never consumed in
        production — this exists to reset a refunded-but-not-revoked "zombie"
        purchase that Play will neither re-sell nor let the Console refund again.
        Always ends in a re-enumeration, so the caller gets a SYNC_DONE either way. */
    public static void consume(final String sku) {
        if (notInitialised(EV_SYNC_DONE, null)) return;
        QueryPurchasesParams params = QueryPurchasesParams.newBuilder()
            .setProductType(BillingClient.ProductType.INAPP)
            .build();
        client.queryPurchasesAsync(params, (r, purchases) -> {
            if (r.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                nativeOnEvent(EV_SYNC_DONE, r.getResponseCode(), null, null, null, null);
                return;
            }
            Purchase target = null;
            for (Purchase p : purchases)
                if (p.getProducts().contains(sku)) { target = p; break; }
            if (target == null) { queryOwned(false); return; }
            client.consumeAsync(
                ConsumeParams.newBuilder()
                    .setPurchaseToken(target.getPurchaseToken())
                    .build(),
                (cr, token) -> queryOwned(false));
        });
    }

    /** CONSUMABLE: the app has granted the purchase of `sku` (from its PURCHASE_OK) — consume it
        so the SKU can be bought again. Play holds at most one unconsumed purchase per product, so
        the SKU names it. Answers CONSUMED with Play's response code (ITEM_NOT_OWNED when there is
        nothing to consume — e.g. it was consumed already). */
    public static void finishConsumable(final String sku) {
        if (notInitialised(EV_CONSUMED, sku)) return;
        QueryPurchasesParams params = QueryPurchasesParams.newBuilder()
            .setProductType(BillingClient.ProductType.INAPP)
            .build();
        client.queryPurchasesAsync(params, (r, purchases) -> {
            if (r.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                nativeOnEvent(EV_CONSUMED, r.getResponseCode(), sku, null, null, null);
                return;
            }
            Purchase target = null;
            for (Purchase p : purchases)
                if (p.getPurchaseState() == Purchase.PurchaseState.PURCHASED
                    && p.getProducts().contains(sku)) { target = p; break; }
            if (target == null) {
                nativeOnEvent(EV_CONSUMED, BillingClient.BillingResponseCode.ITEM_NOT_OWNED,
                              sku, null, null, null);
                return;
            }
            client.consumeAsync(
                ConsumeParams.newBuilder()
                    .setPurchaseToken(target.getPurchaseToken())
                    .build(),
                (cr, token) -> nativeOnEvent(EV_CONSUMED, cr.getResponseCode(), sku, null, null, null));
        });
    }

    /** `code` is the store's response code; it is OK when `details` is non-null, and when
        the store answered OK but does not carry the product. */
    interface DetailsCallback { void run(ProductDetails details, int code); }

    static void queryDetails(final String sku, final DetailsCallback cb) {
        ProductDetails cached;
        synchronized (products) { cached = products.get(sku); }
        if (cached != null) { cb.run(cached, BillingClient.BillingResponseCode.OK); return; }

        QueryProductDetailsParams params = QueryProductDetailsParams.newBuilder()
            .setProductList(Collections.singletonList(
                QueryProductDetailsParams.Product.newBuilder()
                    .setProductId(sku)
                    .setProductType(BillingClient.ProductType.INAPP)
                    .build()))
            .build();
        // Billing 8+ hands back a QueryProductDetailsResult (which also carries the
        // products it could NOT fetch) instead of a bare List<ProductDetails>.
        client.queryProductDetailsAsync(params, (r, result) -> {
            ProductDetails found = null;
            if (r.getResponseCode() == BillingClient.BillingResponseCode.OK && result != null) {
                for (ProductDetails d : result.getProductDetailsList()) {
                    if (sku.equals(d.getProductId())) { found = d; break; }
                }
            }
            if (found != null) {
                synchronized (products) { products.put(sku, found); }
            }
            cb.run(found, r.getResponseCode());
        });
    }

    /** Emit PURCHASE_OK for every owned purchase, then the matching completion event:
        RESTORE_DONE for a user-initiated restore, SYNC_DONE for the automatic replay. */
    static void queryOwned(final boolean explicitRestore) {
        QueryPurchasesParams params = QueryPurchasesParams.newBuilder()
            .setProductType(BillingClient.ProductType.INAPP)
            .build();
        client.queryPurchasesAsync(params, (r, purchases) -> {
            int rc = r.getResponseCode();
            if (rc == BillingClient.BillingResponseCode.OK) {
                for (Purchase p : purchases) reportPurchase(p);
            }
            // ⛔ Carry the response code. A FAILED query reports zero purchases, which is
            // indistinguishable from "you own nothing" unless the consumer is told the query
            // never answered — and an entitlement cache that reconciles against that will
            // revoke a paying customer for being offline. code 0 (OK) = the store answered.
            //
            // The AUTOMATIC replay must also be announced (as SYNC_DONE): the PURCHASE_OK
            // stream is add-only, so without a completion event nothing downstream can ever
            // learn that a purchase was refunded/revoked — Google's `queryPurchasesAsync`
            // guidance is that the returned set IS the entitlement. A separate type keeps it
            // from being mistaken for the answer to a user's "Restore purchases" tap.
            nativeOnEvent(explicitRestore ? EV_RESTORE_DONE : EV_SYNC_DONE,
                          rc, null, null, null, null);
        });
    }

    static void onPurchasesUpdated(BillingResult r, List<Purchase> purchases) {
        int code = r.getResponseCode();
        if (code == BillingClient.BillingResponseCode.OK && purchases != null) {
            for (Purchase p : purchases) reportPurchase(p);
        } else if (code == BillingClient.BillingResponseCode.USER_CANCELED) {
            nativeOnEvent(EV_PURCHASE_CANCELLED, code, null, null, null, null);
        } else if (code == BillingClient.BillingResponseCode.ITEM_ALREADY_OWNED) {
            /* Owned but the app lost the cache — replay it. */
            queryOwned(false);
        } else {
            nativeOnEvent(EV_PURCHASE_FAILED, code, null, null, null, null);
        }
    }

    static void reportPurchase(Purchase p) {
        if (p.getPurchaseState() != Purchase.PurchaseState.PURCHASED) return;
        List<String> skus = p.getProducts();
        String sku = skus.isEmpty() ? "" : skus.get(0);
        nativeOnEvent(EV_PURCHASE_OK, 0, sku, null, p.getOriginalJson(), p.getSignature());
        if (!p.isAcknowledged()) {
            client.acknowledgePurchase(
                AcknowledgePurchaseParams.newBuilder()
                    .setPurchaseToken(p.getPurchaseToken())
                    .build(),
                result -> {});
        }
    }
}
