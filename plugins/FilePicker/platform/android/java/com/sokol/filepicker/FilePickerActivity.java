package com.sokol.filepicker;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;

/**
 * A see-through activity that shows the system's document picker (Storage Access Framework) and receives its answer:
 * a NativeActivity cannot override onActivityResult itself, so the picker is started from here. Open asks for an
 * existing document (ACTION_OPEN_DOCUMENT), save for a new one (ACTION_CREATE_DOCUMENT, then the bytes waiting in the
 * bridge are written to it). Either way the read/write permission is made persistent, so the app can open the same
 * document after a restart (recent files). The outcome goes to FilePickerBridge, which the native side polls.
 */
public final class FilePickerActivity extends Activity {
    static final String EXTRA_MODE = "com.sokol.filepicker.mode";   // 0 open, 1 save
    static final String EXTRA_NAME = "com.sokol.filepicker.name";   // save: the suggested file name
    static final String EXTRA_MIME = "com.sokol.filepicker.mime";   // save: the MIME type of the new document
    private static final int REQUEST = 0x5F1C;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (savedInstanceState != null) return;   // recreated while the picker is up: its answer still comes here
        Intent in = getIntent();
        int mode = in.getIntExtra(EXTRA_MODE, 0);
        Intent pick;
        if (mode == 1) {
            pick = new Intent(Intent.ACTION_CREATE_DOCUMENT);
            pick.addCategory(Intent.CATEGORY_OPENABLE);
            String mime = in.getStringExtra(EXTRA_MIME);
            pick.setType(mime != null ? mime : "application/octet-stream");
            String name = in.getStringExtra(EXTRA_NAME);
            if (name != null) pick.putExtra(Intent.EXTRA_TITLE, name);
        } else {
            pick = new Intent(Intent.ACTION_OPEN_DOCUMENT);
            pick.addCategory(Intent.CATEGORY_OPENABLE);
            pick.setType("*/*");   // custom extensions have no MIME type: the app checks what was picked
        }
        pick.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION
                | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
        try {
            startActivityForResult(pick, REQUEST);
        } catch (Exception e) {   // no document provider on the device
            FilePickerBridge.finish(FilePickerBridge.FAILED, null, null);
            finish();
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST) return;
        Uri uri = data != null ? data.getData() : null;
        if (resultCode != RESULT_OK || uri == null) {
            FilePickerBridge.finish(FilePickerBridge.CANCELLED, null, null);
        } else {
            try {
                getContentResolver().takePersistableUriPermission(uri,
                        Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
            } catch (Exception ignored) {
                // Some providers grant only the session's access: the document still opens now.
            }
            boolean ok = true;
            if (getIntent().getIntExtra(EXTRA_MODE, 0) == 1) ok = FilePickerBridge.writePending(this, uri);
            FilePickerBridge.finish(ok ? FilePickerBridge.PICKED : FilePickerBridge.FAILED, uri.toString(),
                    FilePickerBridge.displayName(this, uri));
        }
        finish();
        overridePendingTransition(0, 0);
    }
}
