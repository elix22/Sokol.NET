// JavaScript library for Sokol Emscripten utility functions
// This file provides the required Emscripten utility functions for EM_JS code in sokol_app.h

mergeInto(LibraryManager.library, {
    // stringToUTF8OnStack - converts string to UTF8 and stores on Emscripten stack
    $stringToUTF8OnStack: function(str) {
        if (typeof str === 'string') {
            var size = lengthBytesUTF8(str) + 1;
            var ret = stackAlloc(size);
            stringToUTF8(str, ret, size);
            return ret;
        } else if (typeof str === 'number') {
            // If str is already a pointer, convert it to string first
            var jsStr = UTF8ToString(str);
            var size = lengthBytesUTF8(jsStr) + 1;
            var ret = stackAlloc(size);
            stringToUTF8(jsStr, ret, size);
            return ret;
        }
        return 0;
    },

    // withStackSave - saves stack pointer, calls function, then restores stack
    $withStackSave: function(func) {
        var stack = stackSave();
        try {
            var ret = func();
            return ret;
        } finally {
            stackRestore(stack);
        }
    },

    // findCanvasEventTarget - finds the canvas element for event handling
    $findCanvasEventTarget: function(target) {
        // For Sokol applications, we typically use the main canvas element
        return Module['canvas'] || document.getElementById('canvas');
    }
});
