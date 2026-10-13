using System;

/// <summary>Play's answer to "is there an update?" (UpdateAvailability), plus <see cref="Error"/> when the check failed —
/// always the case for a build that was not installed from Google Play.</summary>
public enum AppUpdateAvailability { Error = -1, Unknown = 0, None = 1, Available = 2, InProgress = 3 }

/// <summary>What became of the last <see cref="AppUpdate.StartFlexible"/>.</summary>
public enum AppUpdateFlow { Idle = 0, Showing = 1, Accepted = 2, Declined = 3, Failed = 4 }

/// <summary>Play's InstallStatus for the update being downloaded / installed.</summary>
public enum AppUpdateInstall { Unknown = 0, Pending = 1, Downloading = 2, Installing = 3, Installed = 4, Failed = 5, Canceled = 6, Downloaded = 11 }

public readonly struct AppUpdateState
{
    public AppUpdateAvailability Availability { get; init; }
    /// <summary>The FLEXIBLE flow is allowed for this update.</summary>
    public bool FlexibleAllowed { get; init; }
    public AppUpdateInstall Install { get; init; }
    /// <summary>The update's versionCode (0 = none).</summary>
    public int AvailableVersionCode { get; init; }
    /// <summary>Days since Play learned of the update, -1 = unknown.</summary>
    public int StalenessDays { get; init; }
    public AppUpdateFlow Flow { get; init; }
    /// <summary>Play's InstallErrorCode of the last failure, 0 = none.</summary>
    public int ErrorCode { get; init; }
    public long BytesDownloaded { get; init; }
    public long TotalBytes { get; init; }

    public override string ToString() =>
        $"avail={Availability} flex={(FlexibleAllowed ? 1 : 0)} install={Install} vc={AvailableVersionCode} " +
        $"stale={StalenessDays} flow={Flow} err={ErrorCode} bytes={BytesDownloaded}/{TotalBytes}";
}

/// <summary>
/// Google Play In-App Updates (Android only). <see cref="Check"/> asks Play whether a newer version exists,
/// <see cref="StartFlexible"/> shows Play's sheet for a FLEXIBLE update (it downloads in the background while
/// the app keeps running), and once <see cref="AppUpdateState.Install"/> reads
/// <see cref="AppUpdateInstall.Downloaded"/>, <see cref="Complete"/> installs it and restarts the app.
/// Answers arrive asynchronously into <see cref="State"/>, refreshed by <see cref="Poll"/> (call once per
/// frame from the game loop); <see cref="Changed"/> fires when it changes. Elsewhere <see cref="Supported"/>
/// is false and every call is a no-op, so call sites need no <c>#if</c>.
/// </summary>
public static class AppUpdate
{
#if __ANDROID__
    public static bool Supported => true;
#else
    public static bool Supported => false;
#endif

    public static AppUpdateState State { get; private set; } = new() { StalenessDays = -1 };

    /// <summary>Raised from within <see cref="Poll"/>, on the polling thread.</summary>
    public static event Action<AppUpdateState>? Changed;

#if __ANDROID__
    static bool _inited;
    static int _serial;
#endif

    /// <summary>Create Play's update manager. Call once at app start; idempotent.</summary>
    public static void Init()
    {
#if __ANDROID__
        if (_inited) return;
        _inited = true;
        SokolAppUpdate.Init();
#endif
    }

    /// <summary>Ask Play whether an update exists. Call at start and on every resume — a download that finished
    /// in the background shows up as <see cref="AppUpdateInstall.Downloaded"/>.</summary>
    public static void Check()
    {
#if __ANDROID__
        if (_inited) SokolAppUpdate.Check();
#endif
    }

    /// <summary>Show Play's FLEXIBLE update sheet for the update the last <see cref="Check"/> found. False when
    /// there is nothing to start; each check result starts at most one flow.</summary>
    public static bool StartFlexible()
    {
#if __ANDROID__
        return _inited && SokolAppUpdate.StartFlexible();
#else
        return false;
#endif
    }

    /// <summary>Install a downloaded FLEXIBLE update — Play shows its install screen and restarts the app.</summary>
    public static void Complete()
    {
#if __ANDROID__
        if (_inited) SokolAppUpdate.Complete();
#endif
    }

    /// <summary>Refresh <see cref="State"/>; call once per frame from the game loop.</summary>
    public static void Poll()
    {
#if __ANDROID__
        if (!_inited) return;
        SokolAppUpdate.GetState(out var s);
        if (s.serial == _serial) return;
        _serial = s.serial;
        State = new AppUpdateState
        {
            Availability = (AppUpdateAvailability)s.availability, FlexibleAllowed = s.flexible_allowed != 0,
            Install = (AppUpdateInstall)s.install_status, AvailableVersionCode = s.available_version_code,
            StalenessDays = s.staleness_days, Flow = (AppUpdateFlow)s.flow, ErrorCode = s.error_code,
            BytesDownloaded = s.bytes_downloaded, TotalBytes = s.total_bytes,
        };
        Changed?.Invoke(State);
#endif
    }

    /// <summary>Testing with Play's <c>FakeAppUpdateManager</c>, for builds not installed from Google Play.</summary>
    public static class Test
    {
        /// <summary>Swap in a fake manager that offers <paramref name="versionCode"/> as a FLEXIBLE update
        /// (0 = back to the real one). Follow with <see cref="AppUpdate.Check"/>.</summary>
        public static void UseFake(int versionCode)
        {
#if __ANDROID__
            if (_inited) SokolAppUpdate.TestUseFake(versionCode);
#endif
        }

        /// <summary>Drive the fake: "accept", "reject", "download-start", "download-complete", "download-fail",
        /// "cancel-download", "install-complete", "install-fail".</summary>
        public static void Fake(string action)
        {
#if __ANDROID__
            if (_inited) SokolAppUpdate.TestFake(action);
#endif
        }

        /// <summary>Bit 0 = the fake's confirmation sheet is "visible", bit 1 = its install splash is.</summary>
        public static int FakeFlags
        {
            get
            {
#if __ANDROID__
                return _inited ? SokolAppUpdate.TestFakeFlags() : 0;
#else
                return 0;
#endif
            }
        }
    }
}
