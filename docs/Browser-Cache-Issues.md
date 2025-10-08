# Browser Cache Issues During WebAssembly Development

## Overview

When developing WebAssembly applications with frequent changes to HTML, JavaScript, or other static files, browsers may cache these files aggressively, preventing you from seeing your latest changes. This is a common issue during development and can be particularly frustrating when debugging rendering issues or testing new features.

## The Problem

Browsers cache static files (HTML, JavaScript, CSS, images, etc.) to improve performance and reduce network traffic. However, during development:

1. **HTML files** may be cached, preventing new code or scripts from loading
2. **JavaScript modules** may be cached, causing old code to execute
3. **WebAssembly binaries** may be cached, preventing bug fixes from taking effect

Even when you rebuild your application and see new files on disk, the browser may serve the old cached versions instead of requesting fresh files from the server.

### How to Identify Cache Issues

If you experience these symptoms, you likely have a cache issue:

- Changes to HTML files don't appear in the browser
- Console logs from old code still appear
- Bug fixes don't take effect
- Server logs show NO request for a file you know should be loaded (the browser isn't even asking for it)

## Solutions

### During Development

#### Option 1: Hard Refresh (Quickest)
Force the browser to bypass cache for the current page:

- **macOS**: Press **Cmd + Shift + R**
- **Windows/Linux**: Press **Ctrl + Shift + R**

This is the fastest way to see your latest changes without clearing all cache.

#### Option 2: Disable Cache in DevTools (Recommended for Active Development)
This prevents caching while you're actively developing:

1. Open your application in the browser
2. Open DevTools:
   - Press **F12**, or
   - **Cmd + Option + I** (macOS), or
   - **Ctrl + Shift + I** (Windows/Linux)
3. Go to the **Network** tab
4. Check the **"Disable cache"** checkbox at the top
5. **Keep DevTools open** while developing

As long as DevTools remains open, the browser won't cache any files and will always fetch the latest versions from the server.

#### Option 3: Empty Cache and Hard Reload
For a more thorough cache clearing:

1. Open DevTools (**F12** or **Cmd + Option + I**)
2. **Right-click** the refresh button (while DevTools is open)
3. Select **"Empty Cache and Hard Reload"**

This clears the cache for the current site and reloads the page.

#### Option 4: Clear Site Data
For complete cache removal for a specific site:

1. Open DevTools (**F12** or **Cmd + Option + I**)
2. Go to the **Application** tab (Chrome/Edge) or **Storage** tab (Firefox)
3. In the left sidebar, look for **"Storage"** or **"Clear storage"**
4. Click **"Clear site data"** button
5. Refresh the page

#### Option 5: Use Incognito/Private Mode
Open your application in a new incognito/private window:

- **Chrome/Edge**: **Cmd + Shift + N** (macOS) or **Ctrl + Shift + N** (Windows/Linux)
- **Firefox**: **Cmd + Shift + P** (macOS) or **Ctrl + Shift + P** (Windows/Linux)
- **Safari**: **Cmd + Shift + N**

Incognito mode starts with a clean cache, ensuring you see the latest files. However, subsequent refreshes in the same incognito window may still cache files.

#### Option 6: Change the Port
If all else fails, change the port number in your launch configuration:

In `.vscode/launch.json`, change the default port:
```json
"default": "8081"  // or any unused port
```

Since the browser has never cached files from the new port, it must fetch everything fresh.

### Server-Side Solutions

The project's `launch.json` is configured with cache control headers for `dotnet-serve`:

```json
"-h", "Cache-Control: no-store, no-cache, must-revalidate, max-age=0",
"-h", "Pragma: no-cache"
```

These headers tell the browser not to cache files, but browsers may still cache aggressively, especially for HTML files. The client-side solutions above are still necessary during active development.

### For Production

For production deployments, you typically want caching for performance. Use versioning strategies instead:

- **Content hashing**: Add hash to filenames (e.g., `main.a3f2d9b.js`)
- **Query parameters**: Add version to URLs (e.g., `index.html?v=1.2.3`)
- **Proper cache headers**: Set appropriate `Cache-Control` headers based on file types

## Best Practices

1. **Always keep DevTools open** during active development with cache disabled
2. **Use Hard Refresh (Cmd/Ctrl + Shift + R)** when you make quick changes
3. **Test in Incognito mode** before considering a feature "done"
4. **Commit frequently** so you can identify when caching vs. code issues occur
5. **Document your cache-busting strategy** for your team

## Related Issues

- **WebGL Context Issues**: If you see old console logs about WebGL initialization, cache is likely serving old HTML/JS files
- **Alpha Blending Problems**: Old shader code or initialization scripts may be cached
- **Missing Features**: New code in JavaScript modules may not load if cached versions exist

## Additional Resources

- [Chrome DevTools: Disable Cache](https://developer.chrome.com/docs/devtools/network/reference/#disable-cache)
- [MDN: HTTP Caching](https://developer.mozilla.org/en-US/docs/Web/HTTP/Caching)
- [Cache-Control Header Reference](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Cache-Control)

## Summary

**Quick Reference for Daily Development:**

| Issue | Solution | Speed |
|-------|----------|-------|
| Made a small change | **Cmd/Ctrl + Shift + R** | ⚡ Instant |
| Active development session | **DevTools → Disable cache** | ⚡⚡ Continuous |
| Cache completely stuck | **Empty Cache and Hard Reload** | ⚡⚡⚡ Thorough |
| Need absolutely fresh start | **Incognito mode** or **Change port** | ⚡⚡⚡⚡ Nuclear |

When in doubt, use **Hard Refresh** first. If that doesn't work, open DevTools and disable cache. That combination solves 99% of caching issues during development! 🚀
