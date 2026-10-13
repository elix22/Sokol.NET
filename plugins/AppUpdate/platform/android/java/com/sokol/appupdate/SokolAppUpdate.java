package com.sokol.appupdate;

import android.app.Activity;

import com.google.android.play.core.appupdate.AppUpdateInfo;
import com.google.android.play.core.appupdate.AppUpdateManager;
import com.google.android.play.core.appupdate.AppUpdateManagerFactory;
import com.google.android.play.core.appupdate.AppUpdateOptions;
import com.google.android.play.core.appupdate.testing.FakeAppUpdateManager;
import com.google.android.play.core.install.InstallException;
import com.google.android.play.core.install.InstallStateUpdatedListener;
import com.google.android.play.core.install.model.AppUpdateType;

/**
 * Play In-App Updates helper for the sokol_appupdate plugin. Called from native
 * code (sokol_appupdate_android.c); every answer is folded into one state
 * snapshot and pushed back through nativeOnState, which the app's game thread
 * reads without JNI.
 *
 * Everything runs on the UI thread (AppUpdateManager's tasks complete there, and
 * FakeAppUpdateManager is not thread-safe). The FLEXIBLE flow is started with
 * startUpdateFlow(info, activity, options), whose Task carries the result code,
 * so the host activity needs no onActivityResult plumbing.
 */
public final class SokolAppUpdate {

    /** Upcall into sokol_appupdate_android.c. */
    static native void nativeOnState(int availability, int flexibleAllowed, int installStatus,
                                     int versionCode, int stalenessDays, int flow, int errorCode,
                                     long bytesDownloaded, long totalBytes);

    /* Mirrors of SOKOLAPPUPDATE_AVAIL_ERROR / SOKOLAPPUPDATE_FLOW_* in sokol_appupdate.h. */
    static final int AVAIL_ERROR   = -1;
    static final int FLOW_IDLE     = 0;
    static final int FLOW_SHOWING  = 1;
    static final int FLOW_ACCEPTED = 2;
    static final int FLOW_DECLINED = 3;
    static final int FLOW_FAILED   = 4;
    /** Activity result of a failed in-app update flow (ActivityResult.RESULT_IN_APP_UPDATE_FAILED). */
    static final int RESULT_IN_APP_UPDATE_FAILED = 1;

    static Activity activity;
    static AppUpdateManager manager;
    static FakeAppUpdateManager fake;
    /** The last check's answer; one flow may be started from it, then it is spent. */
    static AppUpdateInfo info;

    static int availability, flexibleAllowed, installStatus, versionCode, flow, errorCode;
    static int stalenessDays = -1;
    static long bytesDownloaded, totalBytes;

    static final InstallStateUpdatedListener listener = state -> {
        installStatus   = state.installStatus();
        bytesDownloaded = state.bytesDownloaded();
        totalBytes      = state.totalBytesToDownload();
        if (state.installErrorCode() != 0) errorCode = state.installErrorCode();
        push();
    };

    private SokolAppUpdate() {}

    static void push() {
        nativeOnState(availability, flexibleAllowed, installStatus, versionCode, stalenessDays,
                      flow, errorCode, bytesDownloaded, totalBytes);
    }

    static int errorOf(Exception e) {
        return e instanceof InstallException ? ((InstallException) e).getErrorCode() : -100;
    }

    public static void init(final Activity act) {
        activity = act;
        act.runOnUiThread(() -> {
            if (manager != null) return;
            manager = AppUpdateManagerFactory.create(act.getApplicationContext());
            manager.registerListener(listener);
        });
    }

    public static void check() {
        final Activity act = activity;
        if (act == null) return;
        act.runOnUiThread(() -> {
            if (manager == null) return;
            manager.getAppUpdateInfo()
                .addOnSuccessListener(i -> {
                    info            = i;
                    availability    = i.updateAvailability();
                    flexibleAllowed = i.isUpdateTypeAllowed(AppUpdateType.FLEXIBLE) ? 1 : 0;
                    installStatus   = i.installStatus();
                    versionCode     = i.availableVersionCode();
                    Integer days    = i.clientVersionStalenessDays();
                    stalenessDays   = days != null ? days : -1;
                    bytesDownloaded = i.bytesDownloaded();
                    totalBytes      = i.totalBytesToDownload();
                    errorCode       = 0;
                    push();
                })
                .addOnFailureListener(e -> {
                    info         = null;
                    availability = AVAIL_ERROR;
                    errorCode    = errorOf(e);
                    push();
                });
        });
    }

    public static boolean startFlexible() {
        final Activity act = activity;
        final AppUpdateInfo i = info;
        if (act == null || manager == null || i == null || !i.isUpdateTypeAllowed(AppUpdateType.FLEXIBLE))
            return false;
        info = null;   // an AppUpdateInfo starts one flow only
        act.runOnUiThread(() -> {
            flow = FLOW_SHOWING;
            push();
            manager.startUpdateFlow(i, act, AppUpdateOptions.defaultOptions(AppUpdateType.FLEXIBLE))
                .addOnSuccessListener(code -> {
                    flow = code == Activity.RESULT_OK ? FLOW_ACCEPTED
                         : code == Activity.RESULT_CANCELED ? FLOW_DECLINED : FLOW_FAILED;
                    push();
                })
                .addOnFailureListener(e -> {
                    flow      = FLOW_FAILED;
                    errorCode = errorOf(e);
                    push();
                });
        });
        return true;
    }

    public static void complete() {
        final Activity act = activity;
        if (act == null) return;
        act.runOnUiThread(() -> { if (manager != null) manager.completeUpdate(); });
    }

    // ── Testing: Play's FakeAppUpdateManager ─────────────────────────────────

    public static void useFake(final int availableVersionCode) {
        final Activity act = activity;
        if (act == null) return;
        act.runOnUiThread(() -> {
            if (manager != null) manager.unregisterListener(listener);
            info = null;
            flow = FLOW_IDLE;
            errorCode = 0;
            if (availableVersionCode > 0) {
                fake = new FakeAppUpdateManager(act.getApplicationContext());
                fake.setUpdateAvailable(availableVersionCode, AppUpdateType.FLEXIBLE);
                manager = fake;
            } else {
                fake = null;
                manager = AppUpdateManagerFactory.create(act.getApplicationContext());
            }
            manager.registerListener(listener);
            push();
        });
    }

    public static void fake(final String action) {
        final Activity act = activity;
        if (act == null) return;
        act.runOnUiThread(() -> {
            FakeAppUpdateManager f = fake;
            if (f == null) return;
            switch (action) {
                case "accept":            f.userAcceptsUpdate(); break;
                case "reject":            f.userRejectsUpdate(); break;
                case "download-start":    f.downloadStarts(); break;
                case "download-complete": f.downloadCompletes(); break;
                case "download-fail":     f.downloadFails(); break;
                case "cancel-download":   f.userCancelsDownload(); break;
                case "install-complete":  f.installCompletes(); break;
                case "install-fail":      f.installFails(); break;
                default: break;
            }
        });
    }

    public static int fakeFlags() {
        FakeAppUpdateManager f = fake;
        if (f == null) return 0;
        return (f.isConfirmationDialogVisible() ? 1 : 0) | (f.isInstallSplashScreenVisible() ? 2 : 0);
    }
}
