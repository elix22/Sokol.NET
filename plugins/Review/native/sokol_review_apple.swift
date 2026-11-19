// sokol_review_apple.swift -- the App Store's rating prompt (iOS 16+).
//   StoreKit AppStore.requestReview(in:) — the current API (Swift-only, @MainActor, iOS 16+).
//   iOS 15: no prompt (owner decision 2026-11-19) — the only API there, SKStoreReviewController, is deprecated, so no
//   deprecated call is made anywhere; sokolreview_request() returns false and the state stays IDLE.
// StoreKit decides by itself whether the prompt appears (at most 3 times in 365 days per user; a development-signed build
// always shows it and submits nothing; TestFlight never shows it) and does not report the outcome — so the state goes
// REQUESTING -> DONE, or FAILED when the app has no window scene to show it in.
// The C entry points declared in sokol_review.h are exported with @_cdecl.

import Foundation
import StoreKit
import UIKit

private let lock = NSLock()
private var state: Int32 = 0                   // SOKOLREVIEW_* (sokol_review.h)

private let IDLE: Int32 = 0, REQUESTING: Int32 = 1, DONE: Int32 = 2, FAILED: Int32 = 3

private func setState(_ s: Int32) { lock.lock(); state = s; lock.unlock() }

@MainActor private func activeScene() -> UIWindowScene? {
    let scenes = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
    return scenes.first { $0.activationState == .foregroundActive } ?? scenes.first
}

@available(iOS 16.0, *)
@MainActor private func ask() {
    guard let scene = activeScene() else { setState(FAILED); return }
    AppStore.requestReview(in: scene)
    setState(DONE)
}

@_cdecl("sokolreview_request")
public func sokolreview_request() -> Bool {
    guard #available(iOS 16.0, *) else { return false }   // iOS 15: no current API → no prompt
    lock.lock()
    let busy = state == REQUESTING
    if !busy { state = REQUESTING }
    lock.unlock()
    if busy { return false }
    Task { @MainActor in ask() }               // StoreKit's prompt must be asked for on the main actor
    return true
}

@_cdecl("sokolreview_get_state")
public func sokolreview_get_state() -> Int32 { lock.lock(); defer { lock.unlock() }; return state }

@_cdecl("sokolreview_get_error")
public func sokolreview_get_error() -> Int32 { 0 }

@_cdecl("sokolreview_test_use_fake")
public func sokolreview_test_use_fake(_ on: Bool) { }

/// How this copy was installed (testing only — see sokol_review.h): "development" when the embedded provisioning profile
/// allows debugging (get-task-allow), "adhoc" for another embedded profile, else "testflight" (sandbox receipt) / "appstore".
@_cdecl("sokolreview_test_install_source")
public func sokolreview_test_install_source(_ buf: UnsafeMutablePointer<CChar>?, _ cap: Int32) -> Int32 {
    guard let buf = buf, cap > 0 else { return 0 }
    var source = "appstore"
    if let path = Bundle.main.path(forResource: "embedded", ofType: "mobileprovision"),
       let data = FileManager.default.contents(atPath: path) {
        // the profile is a CMS envelope around a plain-text XML plist
        let text = String(decoding: data, as: UTF8.self)
        if let key = text.range(of: "<key>get-task-allow</key>") {
            source = text[key.upperBound...].prefix(64).contains("<true/>") ? "development" : "adhoc"
        } else {
            source = "adhoc"
        }
    } else if Bundle.main.appStoreReceiptURL?.lastPathComponent == "sandboxReceipt" {
        source = "testflight"
    }
    let bytes = Array(source.utf8.prefix(Int(cap) - 1))
    for (i, b) in bytes.enumerated() { buf[i] = CChar(bitPattern: b) }
    buf[bytes.count] = 0
    return Int32(bytes.count)
}
