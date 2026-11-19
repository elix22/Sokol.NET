/// <summary>What became of the last <see cref="Review.Request"/>. The stores never report whether their prompt was shown
/// or what the user chose, so <see cref="Done"/> means "the store's flow finished", shown or not.</summary>
public enum ReviewState { Idle = 0, Requesting = 1, Done = 2, Failed = 3 }

/// <summary>
/// The platform's own in-app rating prompt: Google Play In-App Review on Android, StoreKit's
/// <c>AppStore.requestReview(in:)</c> on iOS 16+ (none on iOS 15: its only API, SKStoreReviewController, is deprecated). The store decides by itself whether the prompt really appears (quota) and does
/// not say; elsewhere <see cref="Supported"/> is false and every call is a no-op, so call sites need no <c>#if</c>.
/// <para>Store guidance is the app's job: ask after a positive moment at a natural pause, never mid-task, never at first
/// launch, never from a button (a "Rate us" button opens the store page instead).</para>
/// </summary>
public static class Review
{
#if __ANDROID__
    public static bool Supported => true;
#elif __IOS__
    /// <summary>iOS 16+ only: AppStore.requestReview(in:) needs it, and the iOS 15 API is deprecated (no call is made there).</summary>
    public static bool Supported => System.OperatingSystem.IsIOSVersionAtLeast(16);
#else
    public static bool Supported => false;
#endif

    /// <summary>Ask the store to show its rating prompt (async). False when nothing could be started (unsupported, or a
    /// request is already running).</summary>
    public static bool Request()
    {
#if __ANDROID__ || __IOS__
        return Supported && SokolReview.Request();
#else
        return false;
#endif
    }

    public static ReviewState State
    {
        get
        {
#if __ANDROID__ || __IOS__
            return (ReviewState)SokolReview.GetState();
#else
            return ReviewState.Idle;
#endif
        }
    }

    /// <summary>Android: Play's ReviewErrorCode of the last failure (0 = none). iOS: always 0.</summary>
    public static int ErrorCode
    {
        get
        {
#if __ANDROID__ || __IOS__
            return SokolReview.GetError();
#else
            return 0;
#endif
        }
    }

    /// <summary>Testing.</summary>
    public static class Test
    {
        /// <summary>Android: true = Play's <c>FakeReviewManager</c> (the whole flow completes with no UI and no call to
        /// Play), false = the real one. iOS: no effect — a development-signed build always shows StoreKit's prompt and
        /// submits nothing; TestFlight never shows it.</summary>
        public static void UseFake(bool on)
        {
#if __ANDROID__
            SokolReview.TestUseFake(on);
#endif
        }

        /// <summary>How this copy was installed, so a test can refuse to ask the real store where a real review could be
        /// posted. Android: the installer package ("com.android.vending" = Google Play; "" = sideloaded / adb). iOS:
        /// "development" (development-signed: StoreKit submits nothing), "testflight", "appstore" or "adhoc". Elsewhere "".</summary>
        public static string InstallSource
        {
            get
            {
#if __ANDROID__ || __IOS__
                var buf = new byte[128];
                int n = SokolReview.TestInstallSource(buf, buf.Length);
                return n > 0 ? System.Text.Encoding.UTF8.GetString(buf, 0, n) : "";
#else
                return "";
#endif
            }
        }
    }
}
