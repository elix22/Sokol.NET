package com.sokol.review;

import android.app.Activity;

import com.google.android.play.core.review.ReviewException;
import com.google.android.play.core.review.ReviewInfo;
import com.google.android.play.core.review.ReviewManager;
import com.google.android.play.core.review.ReviewManagerFactory;
import com.google.android.play.core.review.testing.FakeReviewManager;

/**
 * Google Play In-App Review helper for the sokol_review plugin. Called from native code
 * (sokol_review_android.c); the outcome is pushed back through nativeOnState.
 *
 * requestReviewFlow() asks Play for a ReviewInfo; launchReviewFlow() hands it to Play, which decides by
 * itself whether the card appears (quota) and never tells the app whether it did or what the user chose —
 * so the flow ends in DONE either way. Everything runs on the UI thread.
 */
public final class SokolReview {

    /** Upcall into sokol_review_android.c. */
    static native void nativeOnState(int state, int error);

    /* Mirrors of SOKOLREVIEW_* in sokol_review.h. */
    static final int REQUESTING = 1;
    static final int DONE       = 2;
    static final int FAILED     = 3;

    static boolean fake;

    private SokolReview() {}

    public static boolean request(final Activity act) {
        if (act == null) return false;
        act.runOnUiThread(() -> {
            final ReviewManager manager = fake
                ? new FakeReviewManager(act.getApplicationContext())
                : ReviewManagerFactory.create(act.getApplicationContext());
            nativeOnState(REQUESTING, 0);
            manager.requestReviewFlow().addOnCompleteListener(task -> {
                if (!task.isSuccessful()) {
                    Exception e = task.getException();
                    nativeOnState(FAILED, e instanceof ReviewException ? ((ReviewException) e).getErrorCode() : -100);
                    return;
                }
                ReviewInfo info = task.getResult();
                manager.launchReviewFlow(act, info).addOnCompleteListener(done -> nativeOnState(DONE, 0));
            });
        });
        return true;
    }

    public static void useFake(boolean on) {
        fake = on;
    }
}
