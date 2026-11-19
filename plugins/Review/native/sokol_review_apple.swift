// sokol_review_apple.swift -- the App Store's rating prompt (iOS).
//   iOS 16+ : StoreKit AppStore.requestReview(in:) — the current API (Swift-only, @MainActor).
//   iOS 15  : SKStoreReviewController.requestReview(in:) — the only API there (deprecated from iOS 18, which never runs this
//             branch). The app's floor is iOS 15.0.
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

@MainActor private func ask() {
    guard let scene = activeScene() else { setState(FAILED); return }
    if #available(iOS 16.0, *) {
        AppStore.requestReview(in: scene)
    } else {
        SKStoreReviewController.requestReview(in: scene)
    }
    setState(DONE)
}

@_cdecl("sokolreview_request")
public func sokolreview_request() -> Bool {
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
