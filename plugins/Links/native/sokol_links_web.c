/* sokol_links_web.c -- open a link from the browser build (Emscripten).
   The JS lives inline (EM_JS), so the app's link needs no extra --js-library.
   Web URL: window.open in a new tab, so the game page stays where it is. Browsers allow that only
   with user activation (a tap or click within the last few seconds), which is why the call must come
   from the input handling of the tap that asked for it; a blocked popup returns null -> false.
   mailto: an anchor click hands the URL to the browser's mail handler without unloading the page.
   (No "noopener" feature string: window.open then always returns null; opener is cleared instead.) */
#include "sokol_links.h"

#if defined(__EMSCRIPTEN__)
#include <emscripten.h>

EM_JS(int, sokollinks_js_open, (const char* url), {
    var u = UTF8ToString(url);
    try {
        if (/^mailto:/i.test(u)) {
            var a = document.createElement("a");
            a.href = u;
            a.rel = "noopener";
            a.click();
            return 1;
        }
        var w = window.open(u, "_blank");
        if (!w) return 0;
        try { w.opener = null; } catch (e) {}
        return 1;
    } catch (e) {
        return 0;
    }
});

bool sokollinks_open(const char* url)
{
    return url && *url && sokollinks_js_open(url) != 0;
}

#endif /* __EMSCRIPTEN__ */
