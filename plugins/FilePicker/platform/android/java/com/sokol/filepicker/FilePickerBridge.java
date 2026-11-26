package com.sokol.filepicker;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.provider.DocumentsContract;
import android.provider.OpenableColumns;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.OutputStream;

/**
 * The native side's entry points (static, called over JNI): start a pick, poll its outcome, and read, write and
 * stat documents by their content:// URI. Reads and writes may run on any thread; start() moves to the UI thread.
 */
public final class FilePickerBridge {
    public static final int IDLE = 0, PICKED = 1, CANCELLED = 2, FAILED = 3, RUNNING = 4;

    private static int state = IDLE;
    private static String resultUri, resultName;
    private static byte[] pendingWrite;   // save: the bytes for the document the user is choosing

    /** Shows the picker: mode 0 open, 1 save (data written once the user picked the place). False while one runs. */
    public static synchronized boolean start(final Activity activity, int mode, String name, String mime, byte[] data) {
        if (state == RUNNING) return false;
        state = RUNNING;
        resultUri = null;
        resultName = null;
        pendingWrite = mode == 1 ? data : null;
        final Intent i = new Intent(activity, FilePickerActivity.class);
        i.putExtra(FilePickerActivity.EXTRA_MODE, mode);
        if (name != null) i.putExtra(FilePickerActivity.EXTRA_NAME, name);
        if (mime != null) i.putExtra(FilePickerActivity.EXTRA_MIME, mime);
        activity.runOnUiThread(new Runnable() {
            @Override public void run() {
                try { activity.startActivity(i); }
                catch (Exception e) { finish(FAILED, null, null); }
            }
        });
        return true;
    }

    /** The state; a finished state is returned once (the next poll answers IDLE). */
    public static synchronized int poll() {
        int s = state;
        if (s != RUNNING) state = IDLE;
        return s;
    }

    public static synchronized String resultUri() { return resultUri; }
    public static synchronized String resultName() { return resultName; }

    static synchronized void finish(int s, String uri, String name) {
        state = s;
        resultUri = uri;
        resultName = name;
        pendingWrite = null;
    }

    static boolean writePending(Context ctx, Uri uri) {
        byte[] data;
        synchronized (FilePickerBridge.class) { data = pendingWrite; }
        return data == null || write(ctx, uri.toString(), data);
    }

    /** The whole document, or null. */
    public static byte[] read(Context ctx, String uri) {
        try (InputStream in = ctx.getContentResolver().openInputStream(Uri.parse(uri))) {
            if (in == null) return null;
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            byte[] buf = new byte[64 * 1024];
            int n;
            while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
            return out.toByteArray();
        } catch (Exception e) {
            return null;
        }
    }

    /** Replaces the document's content ("wt": truncate first). */
    public static boolean write(Context ctx, String uri, byte[] data) {
        try (OutputStream out = ctx.getContentResolver().openOutputStream(Uri.parse(uri), "wt")) {
            if (out == null) return false;
            out.write(data);
            return true;
        } catch (Exception e) {
            return false;
        }
    }

    /** "size|modified" (bytes, Unix milliseconds; -1 when the provider does not say), or null when the document is
     *  gone or no longer readable. */
    public static String stat(Context ctx, String uri) {
        try (Cursor c = ctx.getContentResolver().query(Uri.parse(uri), null, null, null, null)) {
            if (c == null || !c.moveToFirst()) return null;
            long size = -1, modified = -1;
            int si = c.getColumnIndex(OpenableColumns.SIZE);
            if (si >= 0 && !c.isNull(si)) size = c.getLong(si);
            int mi = c.getColumnIndex(DocumentsContract.Document.COLUMN_LAST_MODIFIED);
            if (mi >= 0 && !c.isNull(mi)) modified = c.getLong(mi);
            return size + "|" + modified;
        } catch (Exception e) {
            return null;
        }
    }

    /** True while the app still holds a persisted permission for the document (recent files after a restart). */
    public static boolean hasAccess(Context ctx, String uri) {
        try {
            Uri u = Uri.parse(uri);
            for (android.content.UriPermission p : ctx.getContentResolver().getPersistedUriPermissions())
                if (p.getUri().equals(u) && p.isReadPermission()) return true;
        } catch (Exception ignored) { }
        return false;
    }

    static String displayName(Context ctx, Uri uri) {
        try (Cursor c = ctx.getContentResolver().query(uri, new String[] { OpenableColumns.DISPLAY_NAME }, null, null, null)) {
            if (c != null && c.moveToFirst() && !c.isNull(0)) return c.getString(0);
        } catch (Exception ignored) { }
        String s = uri.getLastPathSegment();
        return s != null ? s : uri.toString();
    }
}
