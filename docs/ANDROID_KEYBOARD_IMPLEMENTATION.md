# Android Soft Keyboard Implementation

## Overview

This document describes the Android soft keyboard and clipboard implementation for sokol-csharp, which enables proper text input and copy/paste support on ### Testing

### Platforms Verified
- ✅ Desktop (Windows/macOS/Linux) - Standard keyboard input + clipboard
- ✅ Web (WebAssembly) - Browser keyboard events + clipboard
- ✅ iOS - Native soft keyboard via UIKit integration + clipboard
- ✅ Android - Soft keyboard via hidden EditText pattern + clipboard via ClipboardManager

### Test Procedure - Keyboard
1. Build and deploy to Android device
2. Tap on ImGui InputText field
3. Verify soft keyboard appears
4. Type characters - each should appear exactly once
5. Test backspace - should delete one character at a time
6. Test rapid typing - no character duplication
7. Test long text input - EditText should trim after 100 chars

### Test Procedure - Clipboard
1. Type text in an ImGui InputText field
2. Select text and use context menu or keyboard shortcut to copy
3. Navigate to another app and paste - text should appear
4. Copy text from another app
5. Return to your app and paste into ImGui InputText field - text should appear
6. Verify Ctrl+C and Ctrl+V work (if physical keyboard attached)using ImGui and other text input systems.

## Architecture

The implementation uses a **hidden EditText pattern** to capture Android's Input Method Editor (IME) input:

```
User types → Android Soft Keyboard → Hidden EditText → TextWatcher → JNI → Native C → sokol events
```

### Key Components

1. **SokolNativeActivity.java** - Java side implementation
2. **sokol_app.h** - Native C side with JNI callbacks
3. **Hidden EditText** - Invisible 1x1 EditText that captures IME input

## Why This Approach?

Android's native input system (`AInputQueue`) **does not support soft keyboard text input**. It only receives hardware key events. To capture soft keyboard input, we must use Java's `EditText` with `TextWatcher`, which properly integrates with Android's IME system.

## Implementation Details

### Java Side (SokolNativeActivity.java)

**Location:** `tools/SokolApplicationBuilder/templates/Android/native-activity/app/src/main/java/com/sokol/app/SokolNativeActivity.java`

**Key Features:**

1. **Static Library Loading:**
   ```java
   static {
       System.loadLibrary("sokol");  // Load before JNI methods called
   }
   ```

2. **Hidden EditText Creation:**
   - 1x1 pixel size, fully transparent
   - Prevents fullscreen IME mode
   - Added to content view but invisible to user

3. **TextWatcher with lastSentLength Tracking:**
   ```java
   private int lastSentLength = 0;
   
   // Only send NEW characters
   if (currentLength > lastSentLength) {
       String newChars = currentText.substring(lastSentLength);
       for (char c : newChars.toCharArray()) {
           nativeOnKeyboardChar(c);
       }
       lastSentLength = currentLength;
   }
   ```

4. **Critical Pattern:** 
   - Android IME accumulates characters in EditText
   - TextWatcher receives ALL accumulated text on each change
   - **Solution:** Track what was already sent (`lastSentLength`) and only forward the difference
   - This prevents character duplication

5. **EditText Trimming:**
   - When EditText exceeds 100 characters, keep only last 100
   - Prevents unbounded memory growth

### Native Side (sokol_app.h)

**Location:** `ext/sokol/sokol_app.h` (around line ~9342)

**Features Implemented:**
1. **Soft Keyboard Control** - Show/hide Android soft keyboard via JNI
2. **Text Input Capture** - Forward IME input to sokol events
3. **Clipboard Support** - Copy/paste functionality using ClipboardManager

**Conditional Compilation:**
The enhanced keyboard and clipboard implementation is guarded by `SOKOL_ANDROID_KEYBOARD_EXT` preprocessor flag:
```c
#ifdef SOKOL_ANDROID_KEYBOARD_EXT
    /* Enhanced JNI-based implementation */
#else
    /* Original sokol implementation - just updates internal state */
    _sapp.onscreen_keyboard_shown = shown;
#endif
```

**Enabling the Extension:**
Define `SOKOL_ANDROID_KEYBOARD_EXT` before including `sokol_app.h` in `ext/sokol.c`:
```c
#ifdef __ANDROID__
#define SOKOL_ANDROID_KEYBOARD_EXT
#include <pthread.h>
...
#endif
```

**Functions:**

1. **_sapp_android_show_keyboard_ext(bool shown)**
   - Uses JNI to call Java's InputMethodManager
   - Shows/hides the soft keyboard
   - Called by sokol's public API when extension is enabled

2. **Java_com_sokol_app_SokolNativeActivity_nativeOnKeyboardChar()**
   - JNI callback from Java for character input
   - Creates SAPP_EVENTTYPE_CHAR event
   - Forwards to sokol event system

3. **Java_com_sokol_app_SokolNativeActivity_nativeOnKeyboardKey()**
   - JNI callback from Java for special keys (backspace)
   - Handles keycode 67 (KEYCODE_DEL)
   - Creates SAPP_EVENTTYPE_KEY_DOWN/UP events

4. **_sapp_android_set_clipboard_string(const char* str)**
   - Uses JNI to call Java's ClipboardManager
   - Sets text to system clipboard
   - Called by sokol's sapp_set_clipboard_string()

5. **_sapp_android_get_clipboard_string()**
   - Uses JNI to call Java's ClipboardManager
   - Retrieves text from system clipboard
   - Called by sokol's sapp_get_clipboard_string()

## Upstream Merge Strategy

### Problem
`sokol_app.h` is maintained by the upstream sokol project. Adding Android-specific code directly into this file can cause merge conflicts when pulling upstream updates.

### Solution
The Android keyboard extension uses **conditional compilation** to coexist with upstream code:

1. **New separate function** - Extension implemented in `_sapp_android_show_keyboard_ext()`, not modifying original
2. **Original code 100% unchanged** - The original `_sapp_android_show_keyboard()` private function remains completely untouched
3. **Conditional at public API level** - Only the public API function `sapp_show_keyboard()` has a conditional to choose which implementation to call
4. **Guarded with `#ifdef SOKOL_ANDROID_KEYBOARD_EXT`** - Extension code only compiles when flag is defined
5. **Zero modification to private functions** - All upstream private functions remain identical to original sokol
6. **Minimal dependencies** - Only uses standard sokol APIs:
   - `_sapp_events_enabled()`
   - `_sapp_init_event()`
   - `_sapp_call_event()`

### Implementation Structure

The implementation keeps ALL original sokol code completely unchanged and adds a new function for the enhanced implementation. The conditional call is made at the public API level:

```c
/* Original sokol private function - 100% UNCHANGED from upstream */
_SOKOL_PRIVATE void _sapp_android_show_keyboard(bool shown) {
    SOKOL_ASSERT(_sapp.valid);
    if (shown) {
        ANativeActivity_showSoftInput(_sapp.android.activity, ANATIVEACTIVITY_SHOW_SOFT_INPUT_FORCED);
    } else {
        ANativeActivity_hideSoftInput(_sapp.android.activity, ANATIVEACTIVITY_HIDE_SOFT_INPUT_NOT_ALWAYS);
    }
    _sapp.onscreen_keyboard_shown = shown;
}

#ifdef SOKOL_ANDROID_KEYBOARD_EXT
/* New enhanced implementation function - only compiled when extension is enabled */
_SOKOL_PRIVATE void _sapp_android_show_keyboard_ext(bool shown) {
    SOKOL_ASSERT(_sapp.valid);
    // ... JNI code to call InputMethodManager via Java showKeyboard() method ...
    _sapp.onscreen_keyboard_shown = shown;
}

/* JNI callbacks only compiled when extension is enabled */
JNIEXPORT void JNICALL Java_com_sokol_app_SokolNativeActivity_nativeOnKeyboardChar(...) { }
JNIEXPORT void JNICALL Java_com_sokol_app_SokolNativeActivity_nativeOnKeyboardKey(...) { }
#endif

/* Public API function - minimal conditional to choose which implementation */
SOKOL_API_IMPL void sapp_show_keyboard(bool show) {
    #if defined(_SAPP_IOS)
    _sapp_ios_show_keyboard(show);
    #elif defined(_SAPP_ANDROID)
        #ifdef SOKOL_ANDROID_KEYBOARD_EXT
        _sapp_android_show_keyboard_ext(show);  // Use enhanced version
        #else
        _sapp_android_show_keyboard(show);      // Use original version
        #endif
    #else
    _SOKOL_UNUSED(show);
    #endif
}
```

### When Merging Upstream

1. **Pull upstream changes** normally - the extension code is separate and won't conflict
2. **Check the public API** `sapp_show_keyboard()` - it should still call `_sapp_android_show_keyboard()` for Android
3. **If upstream modifies the public API function**:
   - Update the `#else` branch in the Android section to match their changes
   - Keep the `#ifdef SOKOL_ANDROID_KEYBOARD_EXT` branch calling `_sapp_android_show_keyboard_ext()`
4. **Private functions remain untouched** - `_sapp_android_show_keyboard()` is never modified, so no merge needed
5. **Your extension function** `_sapp_android_show_keyboard_ext()` is completely separate and unaffected by any upstream changes
6. **Ensure flag is defined** in `ext/sokol.c` before including `sokol_app.h`

### Benefits

✅ **Zero impact on original code** - Original function implementation is completely untouched  
✅ **No merge conflicts** - Extension is in a separate function, can't conflict with upstream changes  
✅ **Easy to disable** - Just remove or comment out the `#define SOKOL_ANDROID_KEYBOARD_EXT`  
✅ **Upstream compatible** - Falls back to original behavior when flag is not defined  
✅ **Clean separation** - Extension code is in its own function with clear guards  
✅ **Easy maintenance** - Any upstream changes to the original function are easy to merge

## Testing

### Platforms Verified
- ✅ Desktop (Windows/macOS/Linux) - Standard keyboard input
- ✅ Web (WebAssembly) - Browser keyboard events
- ✅ iOS - Native soft keyboard via UIKit integration
- ✅ Android - Soft keyboard via hidden EditText pattern

### Test Procedure
1. Build and deploy to Android device
2. Tap on ImGui InputText field
3. Verify soft keyboard appears
4. Type characters - each should appear exactly once
5. Test backspace - should delete one character at a time
6. Test rapid typing - no character duplication
7. Test long text input - EditText should trim after 100 chars

## Debugging

### Common Issues

1. **Characters duplicating:**
   - Symptom: Each keypress shows multiple characters
   - Cause: Sending ALL EditText content instead of just new characters
   - Solution: Use `lastSentLength` tracking pattern

2. **First keypress doesn't work:**
   - Symptom: Second character appears on first press
   - Cause: Clearing EditText after each character resets IME
   - Solution: Let EditText accumulate, only send difference

3. **No keyboard appears:**
   - Check `System.loadLibrary("sokol")` is in static initializer
   - Verify JNI method signatures match exactly
   - Check InputMethodManager permissions in AndroidManifest.xml

### Enable Debug Logging (Temporarily)

Add logging back to Java TextWatcher:
```java
import android.util.Log;

Log.e("SokolKeyboard", "afterTextChanged: text='" + currentText + "' lastSent=" + lastSentLength);
```

Monitor with: `adb logcat | grep SokolKeyboard`

## Files Modified

### Template Files (used for new projects)
- `tools/SokolApplicationBuilder/templates/Android/native-activity/app/src/main/java/com/sokol/app/SokolNativeActivity.java`

### Example Files (for testing)
- `examples/cimgui/Android/native-activity/app/src/main/java/com/sokol/app/SokolNativeActivity.java`

### Native Code
- `ext/sokol.c` - Added `#define SOKOL_ANDROID_KEYBOARD_EXT` before including sokol_app.h (Android only)
- `ext/sokol/sokol_app.h` - Added conditional Android keyboard extension section

### Platform Includes
- `ext/sokol/sokol_app.h` (line ~2339) - Added `#include <jni.h>` for Android platform

## Credits

Implementation developed through iterative debugging and testing on Android devices, solving the core challenge of Android's IME character accumulation behavior.

## References

- [Android Input Method Framework](https://developer.android.com/develop/ui/views/touch-and-input/creating-input-method)
- [JNI Documentation](https://docs.oracle.com/javase/8/docs/technotes/guides/jni/)
- [sokol_app.h Documentation](https://github.com/floooh/sokol/blob/master/sokol_app.h)
