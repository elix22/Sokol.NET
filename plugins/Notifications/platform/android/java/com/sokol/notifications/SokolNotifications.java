package com.sokol.notifications;

import android.app.Activity;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.drawable.Icon;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.service.notification.StatusBarNotification;

import androidx.work.Data;
import androidx.work.ExistingWorkPolicy;
import androidx.work.OneTimeWorkRequest;
import androidx.work.WorkInfo;
import androidx.work.WorkManager;

import java.io.InputStream;
import java.util.List;
import java.util.concurrent.TimeUnit;

/**
 * Local notifications helper for the sokol_notifications plugin. Called from native code
 * (sokol_notifications_android.c); asynchronous answers flow back through nativeOnEvent into the
 * plugin's C event queue, drained by the app's game thread.
 *
 * Scheduling is WorkManager one-time work: it survives reboots and app updates by itself, with no
 * boot receiver and no exact-alarm permission. The time is approximate (Doze may run it late),
 * which is why each request carries an expiry after which {@link SokolNotificationWorker} drops it.
 * Everything the Worker needs travels in the request's input Data, so it runs without app code —
 * also when the app process was started only to run it.
 */
public final class SokolNotifications {

    /** Upcall into sokol_notifications_android.c — safe from any thread. */
    static native void nativeOnEvent(int type, int value, long time);

    /* Mirror of sokolnotify_event_type / sokolnotify_status_t in sokol_notifications.h. */
    static final int EV_PERMISSION   = 1;
    static final int EV_PENDING      = 2;
    static final int EV_PENDING_DONE = 3;
    static final int ST_NOT_DETERMINED = 0;
    static final int ST_GRANTED        = 1;
    static final int ST_DENIED         = 2;

    static final String TAG          = "sokolnotify";      // WorkManager tag + notification tag
    static final String TAG_AT       = "sokolnotify-at-";  // + fire time, read back by queryPending
    static final String TAG_ID       = "sokolnotify-id-";  // + id, read back by queryPending
    static final String CHANNEL_ID   = "sokolnotify";
    static final String EXTRA_ID     = "sokol_notify_id";
    /* A distinct action, so a tap on a running app is not mistaken for a launcher tap: an intent
       equal to the task's root intent only brings the task forward and never reaches onNewIntent. */
    static final String ACTION_OPEN  = "com.sokol.notifications.OPEN";
    static final String PERMISSION   = "android.permission.POST_NOTIFICATIONS";

    static Activity activity;
    static Context appContext;
    static volatile String channelName = "Notifications";
    static volatile String smallIconAsset;

    private SokolNotifications() {}

    public static void init(Activity act) {
        activity = act;
        appContext = act.getApplicationContext();
    }

    public static void configure(String name, String iconAsset) {
        if (name != null && !name.isEmpty()) channelName = name;
        smallIconAsset = iconAsset;
        // Renames the channel the user sees in the system settings (language change).
        ensureChannel(appContext, channelName);
    }

    public static int status() {
        if (appContext == null) return ST_DENIED;
        NotificationManager nm = appContext.getSystemService(NotificationManager.class);
        if (nm != null && nm.areNotificationsEnabled()) return ST_GRANTED;
        // Below API 33 there is no runtime prompt: off means the user switched it off in settings.
        if (Build.VERSION.SDK_INT < 33) return ST_DENIED;
        // Not granted yet. Android cannot tell "never asked" from "refused twice" (then it shows no
        // dialog at all); requestPermission() finds out and reports DENIED in the second case.
        return appContext.checkSelfPermission(PERMISSION) == PackageManager.PERMISSION_GRANTED
            ? ST_DENIED   // permission granted but notifications switched off in settings
            : ST_NOT_DETERMINED;
    }

    private static final int PERM_POLL_MS  = 250;
    private static final int PERM_SHOWN_MS = 1500;  // slow phones take a moment to raise it
    // No time limit while the dialog is up: a player may take minutes to answer. A 60 s cap read a late
    // "Allow" as refused (the app stayed off while Android had granted it).

    /**
     * Raise the POST_NOTIFICATIONS dialog (API 33+) and report the outcome as EV_PERMISSION.
     * The grant result goes to the Activity's onRequestPermissionsResult, which this plugin does not
     * own, so the answer is POLLED on the main looper instead. While the
     * dialog is up the activity has no window focus; focus back without a grant = refused. Focus
     * never lost within PERM_SHOWN_MS = Android showed no dialog (refused twice before) = refused.
     */
    public static void requestPermission() {
        final int now = status();
        if (now != ST_NOT_DETERMINED || activity == null) {
            nativeOnEvent(EV_PERMISSION, now == ST_GRANTED ? ST_GRANTED : ST_DENIED, 0);
            return;
        }
        final Activity act = activity;
        final Handler main = new Handler(Looper.getMainLooper());
        main.post(() -> {
            try {
                act.requestPermissions(new String[] { PERMISSION }, 4712);
            } catch (Throwable t) {
                nativeOnEvent(EV_PERMISSION, ST_DENIED, 0);
                return;
            }
            main.postDelayed(new Runnable() {
                int tries = 0;
                boolean focusLost = false;
                @Override public void run() {
                    tries++;
                    if (status() == ST_GRANTED) { nativeOnEvent(EV_PERMISSION, ST_GRANTED, 0); return; }
                    // The activity went away under the dialog: its focus will never come back to answer.
                    if (act.isFinishing() || act.isDestroyed()) { nativeOnEvent(EV_PERMISSION, ST_DENIED, 0); return; }
                    boolean focused = act.hasWindowFocus();
                    if (!focused) focusLost = true;
                    boolean answered = focused && (focusLost || tries * PERM_POLL_MS >= PERM_SHOWN_MS);
                    if (answered) { nativeOnEvent(EV_PERMISSION, ST_DENIED, 0); return; }
                    main.postDelayed(this, PERM_POLL_MS);
                }
            }, PERM_POLL_MS);
        });
    }

    public static void schedule(int id, long fireUnix, long expireUnix, String title, String body) {
        if (appContext == null) return;
        long delayMs = Math.max(0, fireUnix * 1000L - System.currentTimeMillis());
        Data data = new Data.Builder()
            .putInt("id", id)
            .putLong("expire", expireUnix)
            .putString("title", title)
            .putString("body", body)
            .putString("channel", channelName)
            .putString("icon", smallIconAsset)
            .build();
        OneTimeWorkRequest req = new OneTimeWorkRequest.Builder(SokolNotificationWorker.class)
            .setInitialDelay(delayMs, TimeUnit.MILLISECONDS)
            .setInputData(data)
            .addTag(TAG)
            .addTag(TAG_AT + fireUnix)
            .addTag(TAG_ID + id)
            .build();
        WorkManager.getInstance(appContext).enqueueUniqueWork(TAG + "-" + id, ExistingWorkPolicy.REPLACE, req);
    }

    public static void cancel(int id) {
        if (appContext == null) return;
        WorkManager.getInstance(appContext).cancelUniqueWork(TAG + "-" + id);
        NotificationManager nm = appContext.getSystemService(NotificationManager.class);
        if (nm != null) nm.cancel(TAG, id);
    }

    public static void cancelAll() {
        if (appContext == null) return;
        // By tag, never cancelAllWork(): other code in the app may use WorkManager too.
        WorkManager.getInstance(appContext).cancelAllWorkByTag(TAG);
        NotificationManager nm = appContext.getSystemService(NotificationManager.class);
        if (nm == null) return;
        for (StatusBarNotification sbn : nm.getActiveNotifications())
            if (TAG.equals(sbn.getTag())) nm.cancel(TAG, sbn.getId());
    }

    public static void queryPending() {
        if (appContext == null) { nativeOnEvent(EV_PENDING_DONE, 0, 0); return; }
        final Context c = appContext;
        new Thread(() -> {
            try {
                List<WorkInfo> infos = WorkManager.getInstance(c).getWorkInfosByTag(TAG).get();
                for (WorkInfo wi : infos) {
                    if (wi.getState().isFinished()) continue;
                    int id = 0;
                    long at = 0;
                    for (String t : wi.getTags()) {
                        if (t.startsWith(TAG_AT)) at = Long.parseLong(t.substring(TAG_AT.length()));
                        else if (t.startsWith(TAG_ID)) id = Integer.parseInt(t.substring(TAG_ID.length()));
                    }
                    nativeOnEvent(EV_PENDING, id, at);
                }
            } catch (Throwable t) {
                // fall through: an unreadable list is reported as an empty one
            }
            nativeOnEvent(EV_PENDING_DONE, 0, 0);
        }, "sokolnotify-query").start();
    }

    /** The id of the notification that opened (or reopened) the activity, once; 0 = none. */
    public static int takeOpened() {
        Activity act = activity;
        if (act == null) return 0;
        Intent i = act.getIntent();   // the newest intent: SokolNativeActivity.onNewIntent calls setIntent
        if (i == null || !ACTION_OPEN.equals(i.getAction())) return 0;
        int id = i.getIntExtra(EXTRA_ID, 0);
        i.removeExtra(EXTRA_ID);
        i.setAction(Intent.ACTION_MAIN);
        return id;
    }

    public static void openSettings() {
        Activity act = activity;
        if (act == null) return;
        act.runOnUiThread(() -> {
            try {
                Intent i = new Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS)
                    .putExtra(Settings.EXTRA_APP_PACKAGE, act.getPackageName());
                act.startActivity(i);
            } catch (Throwable t) {
                try {
                    act.startActivity(new Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                        android.net.Uri.fromParts("package", act.getPackageName(), null)));
                } catch (Throwable ignored) { }
            }
        });
    }

    // ── Shared with SokolNotificationWorker (may run without an activity) ─────────────────────

    static void ensureChannel(Context c, String name) {
        if (c == null) return;
        NotificationManager nm = c.getSystemService(NotificationManager.class);
        if (nm == null) return;
        NotificationChannel ch = new NotificationChannel(CHANNEL_ID, name, NotificationManager.IMPORTANCE_DEFAULT);
        nm.createNotificationChannel(ch);   // creating an existing channel only updates its name
    }

    static void post(Context c, int id, String title, String body, String channel, String iconAsset) {
        NotificationManager nm = c.getSystemService(NotificationManager.class);
        if (nm == null || !nm.areNotificationsEnabled()) return;
        ensureChannel(c, channel != null ? channel : "Notifications");

        Intent launch = c.getPackageManager().getLaunchIntentForPackage(c.getPackageName());
        if (launch == null) return;
        ComponentName target = launch.getComponent();
        Intent open = new Intent(ACTION_OPEN)
            .setComponent(target)
            .putExtra(EXTRA_ID, id)
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        PendingIntent pi = PendingIntent.getActivity(c, id, open,
            PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);

        Notification.Builder b = new Notification.Builder(c, CHANNEL_ID)
            .setContentTitle(title)
            .setContentText(body)
            .setStyle(new Notification.BigTextStyle().bigText(body))
            .setContentIntent(pi)
            .setAutoCancel(true)
            .setSmallIcon(smallIcon(c, iconAsset));
        nm.notify(TAG, id, b.build());
    }

    static Icon smallIcon(Context c, String asset) {
        if (asset != null && !asset.isEmpty()) {
            try (InputStream in = c.getAssets().open(asset)) {
                Bitmap bmp = BitmapFactory.decodeStream(in);
                if (bmp != null) return Icon.createWithBitmap(bmp);
            } catch (Throwable ignored) { }
        }
        return Icon.createWithResource(c, c.getApplicationInfo().icon);
    }
}
