// Saved data on the web (GameEditor.Framework.Core.SaveData). The app's own folder, /libsokol, is kept in the
// browser's IndexedDB: read back before the game starts, and written a moment after the game writes a file there.
// The engine's build file (Game.targets) gives this file to the linker for every game built for the web.
Module['preRun'] = Module['preRun'] || [];
if (typeof Module['preRun'] === 'function') Module['preRun'] = [Module['preRun']];
Module['preRun'].push(function () {
    try { FS.mkdir('/libsokol'); } catch (e) { }
    try { FS.mount(IDBFS, {}, '/libsokol'); }
    catch (e) { console.warn('Saved data will not outlive this page: the browser gives no storage (' + e + ').'); return; }

    addRunDependency('saved-data');
    FS.syncfs(true, function (err) {
        if (err) console.warn('Saved data could not be read from the browser: ' + err);
        removeRunDependency('saved-data');
    });

    var writing = false;
    function keep() {
        if (writing) return;
        writing = true;
        FS.syncfs(false, function (err) {
            writing = false;
            if (err) console.warn('Saved data could not be kept in the browser: ' + err);
        });
    }
    setInterval(keep, 2000);
    if (typeof document !== 'undefined') document.addEventListener('visibilitychange', function () { if (document.hidden) keep(); });
    if (typeof window !== 'undefined') window.addEventListener('pagehide', keep);
});
