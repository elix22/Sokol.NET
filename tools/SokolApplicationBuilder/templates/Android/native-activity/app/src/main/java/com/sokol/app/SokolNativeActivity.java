package com.sokol.app;

import android.app.NativeActivity;
import android.content.Context;
import android.os.Build;
import android.os.Bundle;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.KeyEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.Window;
import android.view.WindowManager;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputMethodManager;
import android.widget.EditText;
import android.widget.FrameLayout;

public class SokolNativeActivity extends NativeActivity {
    
    // Load native library early so JNI methods are available
    static {
        System.loadLibrary("sokol");
    }
    
    private InputMethodManager inputMethodManager;
    private EditText hiddenEditText;
    private TextWatcher textWatcher;
    private boolean isProcessingText = false;
    
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        
        // Get InputMethodManager for keyboard control
        inputMethodManager = (InputMethodManager) getSystemService(Context.INPUT_METHOD_SERVICE);
        
        // Create hidden EditText for capturing keyboard input
        createHiddenEditText();
        
        // Enable immersive fullscreen mode if the theme is set to fullscreen
        enableImmersiveMode();
    }
    
    private void createHiddenEditText() {
        // Create an invisible EditText to receive keyboard input
        hiddenEditText = new EditText(this);
        hiddenEditText.setLayoutParams(new ViewGroup.LayoutParams(1, 1));
        hiddenEditText.setAlpha(0.0f);
        hiddenEditText.setImeOptions(EditorInfo.IME_FLAG_NO_FULLSCREEN | EditorInfo.IME_FLAG_NO_EXTRACT_UI);
        
        // Create text watcher as member variable so we can remove/add it
        textWatcher = new TextWatcher() {
            private int lastSentLength = 0;
            
            @Override
            public void beforeTextChanged(CharSequence s, int start, int count, int after) {
            }
            
            @Override
            public void onTextChanged(CharSequence s, int start, int before, int count) {
            }
            
            @Override
            public void afterTextChanged(Editable s) {
                String currentText = s.toString();
                int currentLength = currentText.length();
                
                // Skip if we're programmatically modifying text
                if (isProcessingText) {
                    return;
                }
                
                // If text grew, send only the new characters
                if (currentLength > lastSentLength) {
                    String newChars = currentText.substring(lastSentLength);
                    for (char c : newChars.toCharArray()) {
                        nativeOnKeyboardChar(c);
                    }
                    lastSentLength = currentLength;
                }
                // If text shrank, handle backspace
                else if (currentLength < lastSentLength) {
                    int deleteCount = lastSentLength - currentLength;
                    for (int i = 0; i < deleteCount; i++) {
                        nativeOnKeyboardKey(67, true);  // KEYCODE_DEL down
                        nativeOnKeyboardKey(67, false); // KEYCODE_DEL up
                    }
                    lastSentLength = currentLength;
                }
                
                // Limit EditText length to prevent it from growing too large (keep last 100 chars)
                if (currentLength > 100) {
                    isProcessingText = true;
                    String trimmed = currentText.substring(currentLength - 100);
                    s.replace(0, s.length(), trimmed);
                    lastSentLength = 100;
                    isProcessingText = false;
                }
            }
        };
        
        // Add text change listener to forward text to native code
        hiddenEditText.addTextChangedListener(textWatcher);
        
        // Add the EditText to content view
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                FrameLayout contentView = new FrameLayout(SokolNativeActivity.this);
                contentView.addView(hiddenEditText);
                setContentView(contentView);
            }
        });
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus) {
            enableImmersiveMode();
        }
    }

    @SuppressWarnings("deprecation")
    private void enableImmersiveMode() {
        Window window = getWindow();
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            // Android 11 (API 30) and above - use WindowInsetsController
            window.setDecorFitsSystemWindows(false);
            android.view.WindowInsetsController controller = window.getInsetsController();
            if (controller != null) {
                controller.hide(android.view.WindowInsets.Type.statusBars() | android.view.WindowInsets.Type.navigationBars());
                controller.setSystemBarsBehavior(android.view.WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
            }
        } else if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.KITKAT) {
            // Android 4.4 (KitKat) to Android 10 - use deprecated setSystemUiVisibility for backward compatibility
            View decorView = window.getDecorView();
            decorView.setSystemUiVisibility(
                View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                | View.SYSTEM_UI_FLAG_FULLSCREEN
                | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
            );
        }
        
        // Keep screen on
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
    }
    
    // Called from native code via JNI to show/hide the soft keyboard
    public void showKeyboard(final boolean show) {
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (hiddenEditText != null && inputMethodManager != null) {
                    if (show) {
                        // Request focus and show keyboard
                        // The TextWatcher will automatically clear after each character
                        hiddenEditText.requestFocus();
                        inputMethodManager.showSoftInput(hiddenEditText, InputMethodManager.SHOW_IMPLICIT);
                    } else {
                        // Hide keyboard and clear focus
                        inputMethodManager.hideSoftInputFromWindow(hiddenEditText.getWindowToken(), 0);
                        hiddenEditText.clearFocus();
                    }
                }
            }
        });
    }
    
    // Native methods to forward keyboard events
    private native void nativeOnKeyboardChar(int codepoint);
    private native void nativeOnKeyboardKey(int keycode, boolean down);
}
