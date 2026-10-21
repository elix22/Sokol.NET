package com.sokol.notifications;

import android.content.Context;

import androidx.annotation.NonNull;
import androidx.work.Data;
import androidx.work.Worker;
import androidx.work.WorkerParameters;

/**
 * Posts one scheduled notification. Runs in the app process, possibly started for this alone (no
 * activity, no native code): everything it needs is in the request's input Data.
 */
public final class SokolNotificationWorker extends Worker {

    public SokolNotificationWorker(@NonNull Context context, @NonNull WorkerParameters params) {
        super(context, params);
    }

    @NonNull
    @Override
    public Result doWork() {
        Data d = getInputData();
        long expire = d.getLong("expire", 0);
        // Doze or a battery saver ran this late: a stale reminder (e.g. at night) is dropped.
        if (expire > 0 && System.currentTimeMillis() > expire * 1000L) return Result.success();
        SokolNotifications.post(getApplicationContext(), d.getInt("id", 0), d.getString("title"),
            d.getString("body"), d.getString("channel"), d.getString("icon"));
        return Result.success();
    }
}
