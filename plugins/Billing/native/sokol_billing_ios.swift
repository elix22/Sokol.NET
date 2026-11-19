/* sokol_billing_ios.swift -- StoreKit 2 implementation for the sokol_billing
   plugin. Exposes the C ABI via @_cdecl; events flow into the shared C queue
   (sokol_billing_queue.c, linked into this framework) via sokolbilling__emit,
   so sokolbilling_poll_event works identically on both platforms.

   One-time and consumable products (a consumable is finished when reported). Requires iOS 15+ (StoreKit 2).
   PURCHASE_OK.proof carries the transaction's JWS representation — Apple-signed,
   verifiable offline by the app's entitlement layer. */

import Foundation
import StoreKit

@_silgen_name("sokolbilling__emit")
private func sokolbilling__emit(
    _ type: Int32, _ code: Int32,
    _ sku: UnsafePointer<CChar>?, _ price: UnsafePointer<CChar>?,
    _ proof: UnsafePointer<CChar>?, _ signature: UnsafePointer<CChar>?)

/* Mirror of sokolbilling_event_type in sokol_billing.h. */
private let EV_PRODUCT_INFO: Int32       = 1
private let EV_PRODUCT_FAILED: Int32     = 2
private let EV_PURCHASE_OK: Int32        = 3
private let EV_PURCHASE_CANCELLED: Int32 = 4
private let EV_PURCHASE_FAILED: Int32    = 5
private let EV_RESTORE_DONE: Int32       = 6
private let EV_CONSUMED: Int32           = 8

private func emit(_ type: Int32, code: Int32 = 0, sku: String? = nil,
                  price: String? = nil, proof: String? = nil) {
    let cSku   = sku.map   { strdup($0) } ?? nil
    let cPrice = price.map { strdup($0) } ?? nil
    let cProof = proof.map { strdup($0) } ?? nil
    sokolbilling__emit(type, code, cSku, cPrice, cProof, nil)
    free(cSku)
    free(cPrice)
    free(cProof)
}

@MainActor
private final class SB {
    static var products: [String: Product] = [:]
    static var updatesTask: Task<Void, Never>? = nil
}

private func reportVerified(_ result: VerificationResult<Transaction>) -> Transaction? {
    guard case .verified(let t) = result, t.revocationDate == nil else { return nil }
    emit(EV_PURCHASE_OK, sku: t.productID, proof: result.jwsRepresentation)
    return t
}

@_cdecl("sokolbilling_init")
public func sokolbilling_init() {
    Task { @MainActor in
        if SB.updatesTask == nil {
            /* Out-of-band transactions: Ask to Buy approvals, family sharing,
               purchases finished after a crash. */
            SB.updatesTask = Task.detached {
                for await update in Transaction.updates {
                    if let t = reportVerified(update) { await t.finish() }
                }
            }
        }
        /* Replay current entitlements so the app's cache heals on launch
           (also how a revocation disappears: the entitlement stops arriving). */
        for await result in Transaction.currentEntitlements {
            _ = reportVerified(result)
        }
    }
}

@_cdecl("sokolbilling_query_product")
public func sokolbilling_query_product(_ cSku: UnsafePointer<CChar>?) {
    guard let cSku = cSku else { return }
    let sku = String(cString: cSku)
    Task { @MainActor in
        do {
            if let p = try await Product.products(for: [sku]).first {
                SB.products[sku] = p
                emit(EV_PRODUCT_INFO, sku: sku, price: p.displayPrice)
            } else {
                emit(EV_PRODUCT_FAILED, code: 404, sku: sku)
            }
        } catch {
            emit(EV_PRODUCT_FAILED, code: -1, sku: sku)
        }
    }
}

@_cdecl("sokolbilling_purchase")
public func sokolbilling_purchase(_ cSku: UnsafePointer<CChar>?) {
    guard let cSku = cSku else { return }
    let sku = String(cString: cSku)
    Task { @MainActor in
        do {
            var product = SB.products[sku]
            if product == nil {
                product = try await Product.products(for: [sku]).first
                if let p = product { SB.products[sku] = p }
            }
            guard let product = product else {
                emit(EV_PURCHASE_FAILED, code: 404, sku: sku)
                return
            }
            switch try await product.purchase() {
            case .success(let verification):
                if let t = reportVerified(verification) {
                    await t.finish()
                } else {
                    emit(EV_PURCHASE_FAILED, code: -2, sku: sku)  /* unverified */
                }
            case .userCancelled:
                emit(EV_PURCHASE_CANCELLED, sku: sku)
            case .pending:
                emit(EV_PURCHASE_FAILED, code: -3, sku: sku)      /* Ask to Buy — arrives later via updates */
            @unknown default:
                emit(EV_PURCHASE_FAILED, code: -4, sku: sku)
            }
        } catch {
            emit(EV_PURCHASE_FAILED, code: -1, sku: sku)
        }
    }
}

@_cdecl("sokolbilling_restore")
public func sokolbilling_restore() {
    Task { @MainActor in
        /* User-initiated restore: sync may prompt for App Store sign-in.
           A cancelled sync still enumerates whatever is cached locally. */
        try? await AppStore.sync()
        for await result in Transaction.currentEntitlements {
            _ = reportVerified(result)
        }
        /* code 0 = the enumeration is AUTHORITATIVE, and on StoreKit 2 it always is:
           currentEntitlements reads Apple's own signed local cache, so an offline device
           still lists what it owns. That is why a failed sync is NOT reported as an error
           here, unlike Android — there, a failed queryPurchasesAsync returns an empty list
           that is indistinguishable from "owns nothing", so it must report its code.
           ⛔ Do not "fix" this into reporting sync failures: it would block legitimate
           offline reconciliation on iOS for no gain. */
        emit(EV_RESTORE_DONE)
    }
}

/* Deliberately does NOTHING on iOS, and the symbol exists only so the managed P/Invoke
   links (a missing one breaks the whole app's static link, not just this call).

   The Android counterpart exists because Play's queryPurchasesAsync is the only way to
   learn that a purchase was refunded. StoreKit has no such gap: Transaction.updates
   already pushes revocations to a running app, and currentEntitlements is Apple's own
   signed local cache. Emitting SYNC_DONE here would hand a consumer an "authoritative"
   answer that says nothing new, and any consumer that reconciles on it would then be
   reconciling iOS entitlements against a code path never designed for them.
   Whether iOS ever wants an equivalent is a consumer-side decision, not this plugin's. */
@_cdecl("sokolbilling_sync")
public func sokolbilling_sync() { }

/* Android-only test tool (see sokol_billing.h). The symbol must exist for the
   app's static link; StoreKit has no consume for non-consumables. */
@_cdecl("sokolbilling_consume")
public func sokolbilling_consume(_ sku: UnsafePointer<CChar>?) { }

/* Consumables: StoreKit 2 already finished the transaction when PURCHASE_OK was
   emitted (and a finished consumable never appears in currentEntitlements), so
   there is nothing left to consume — answer at once so the managed flow is the
   same on both platforms. */
@_cdecl("sokolbilling_finish_consumable")
public func sokolbilling_finish_consumable(_ cSku: UnsafePointer<CChar>?) {
    guard let cSku = cSku else { return }
    emit(EV_CONSUMED, sku: String(cString: cSku))
}
