/* sokol_billing.h -- one-time in-app purchases (non-consumable + consumable)
   for Sokol.NET apps.
   Android: Google Play Billing Library (via a Java helper class).
   iOS:     StoreKit 2 (Swift shim with @_cdecl entry points).

   All calls are non-blocking; results surface as queued events drained by
   sokolbilling_poll_event() — call it from the game thread once per frame
   (the NearNet.Poll model). Event string pointers are valid until the NEXT
   sokolbilling_poll_event() call; copy them before polling again.

   Thread safety: the public functions may be called from any thread, but the
   intended model is a single game thread. Store SDK callbacks arrive on
   platform threads and are queued internally.
*/
#pragma once
#ifndef SOKOL_BILLING_H
#define SOKOL_BILLING_H

#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef enum sokolbilling_event_type {
    SOKOLBILLING_EVENT_NONE               = 0,
    SOKOLBILLING_EVENT_PRODUCT_INFO       = 1, /* sku + localized price          */
    SOKOLBILLING_EVENT_PRODUCT_FAILED     = 2, /* sku + code                     */
    SOKOLBILLING_EVENT_PURCHASE_OK        = 3, /* sku + proof (+ signature)      */
    SOKOLBILLING_EVENT_PURCHASE_CANCELLED = 4, /* sku                            */
    SOKOLBILLING_EVENT_PURCHASE_FAILED    = 5, /* sku + code                     */
    SOKOLBILLING_EVENT_RESTORE_DONE       = 6, /* restore finished; code 0 = the
                                                  store ANSWERED (0+ purchases are
                                                  authoritative), non-zero = the
                                                  query failed and the enumeration
                                                  means nothing. Reconciling an
                                                  entitlement cache against a failed
                                                  query revokes offline customers.  */
    SOKOLBILLING_EVENT_SYNC_DONE          = 7, /* an AUTOMATIC owned-purchase sync
                                                  finished (on connect, not a user
                                                  restore); same code semantics as
                                                  RESTORE_DONE. Emitted so a cache
                                                  can learn that a SKU is no longer
                                                  owned — the PURCHASE_OK replay is
                                                  add-only and can never say that.
                                                  Kept distinct from RESTORE_DONE so
                                                  a background sync never answers the
                                                  user's "Restore purchases" tap.
                                                  Android only today; iOS reads
                                                  Apple's own signed local cache.    */
    SOKOLBILLING_EVENT_CONSUMED           = 8, /* sokolbilling_finish_consumable
                                                  answered: sku + code (0 = consumed,
                                                  the SKU can be bought again).      */
} sokolbilling_event_type;

typedef struct sokolbilling_event {
    int type;               /* sokolbilling_event_type                           */
    int code;               /* store-specific error code on *_FAILED, else 0     */
    const char* sku;        /* product id; may be NULL                           */
    const char* price;      /* localized price string (PRODUCT_INFO), else NULL  */
    const char* proof;      /* PURCHASE_OK verification material:                */
                            /*   Android: the purchase's original JSON payload   */
                            /*   iOS:     the StoreKit 2 JWS representation      */
    const char* signature;  /* Android: base64 RSA signature of proof; iOS: NULL */
} sokolbilling_event;

/* Connect to the store. Safe to call once at app start; owned purchases replay
   as PURCHASE_OK events after (re)connection so entitlement caches can heal. */
void sokolbilling_init(void);

/* Fetch localized product info -> PRODUCT_INFO | PRODUCT_FAILED. */
void sokolbilling_query_product(const char* sku);

/* Launch the platform purchase flow
   -> PURCHASE_OK | PURCHASE_CANCELLED | PURCHASE_FAILED. */
void sokolbilling_purchase(const char* sku);

/* Re-enumerate owned purchases
   -> zero or more PURCHASE_OK, then RESTORE_DONE. */
void sokolbilling_restore(void);

/* Same enumeration, but as a BACKGROUND sync -> zero or more PURCHASE_OK, then
   SYNC_DONE. Call it when the app returns to the foreground: the store's answer
   is what reveals a purchase that was refunded or revoked while you were away,
   and a cache that only ever hears PURCHASE_OK can never learn that.

   Distinct from sokolbilling_restore() because that one answers a USER's
   "Restore purchases" tap; a consumer that shows the result in its UI must be
   able to tell the two apart. Android only today -- on iOS this is a no-op,
   since StoreKit's currentEntitlements is Apple's own signed local cache. */
void sokolbilling_sync(void);

/* TEST TOOL (Android): consume the owned one-time purchase of `sku`. Play then
   stops reporting it -- to the app the next sync is indistinguishable from a
   revocation -- and the SKU becomes purchasable again. Exists to reset a
   refunded-but-not-revoked purchase, which Play will neither re-sell nor let
   the Console refund again. Completes like sokolbilling_sync() -> SYNC_DONE
   after a re-enumeration. No-op on iOS. */
void sokolbilling_consume(const char* sku);

/* CONSUMABLES: a consumable (e.g. a bundle of coins) arrives as PURCHASE_OK
   like any other product. Grant it (persisted, deduplicated by the order id in
   the proof), THEN call this to consume it -> CONSUMED (sku, code). Until it is
   consumed the store keeps it owned: it replays as PURCHASE_OK on the next
   connect (so a grant lost to a crash heals) and cannot be bought again.
   Android: consumeAsync on the owned purchase of `sku` (Play holds at most one
   unconsumed purchase per product). iOS: StoreKit 2 has already finished the
   transaction when PURCHASE_OK was emitted, so this answers CONSUMED at once. */
void sokolbilling_finish_consumable(const char* sku);

/* Pop one queued event. Returns false when the queue is empty.
   Strings in *out stay valid until the next call. */
bool sokolbilling_poll_event(sokolbilling_event* out);

#ifdef __cplusplus
}
#endif
#endif /* SOKOL_BILLING_H */
