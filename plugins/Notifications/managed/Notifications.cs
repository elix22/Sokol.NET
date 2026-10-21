#nullable enable
using System;
using System.Collections.Generic;

/// <summary>
/// Local (on-device) scheduled notifications for Sokol.NET apps — WorkManager on Android,
/// UNUserNotificationCenter on iOS. No push and no server: the app schedules its own, already
/// localized text and the OS shows it. Asynchronous answers arrive as
/// <see cref="NotificationEvent"/>s raised by <see cref="Notifications.Poll"/>, which the app calls
/// once per frame from its game loop.
///
/// Ids are app-chosen, 1..<see cref="Notifications.MaxId"/>; scheduling a pending id replaces it.
/// On platforms without a backend (desktop, web) <see cref="Notifications.Available"/> is false,
/// the permission reads as denied and every call completes through the same event path.
/// </summary>
public enum NotificationPermission
{
    /// <summary>Never asked: <see cref="Notifications.RequestPermission"/> may show the OS prompt.</summary>
    NotDetermined = 0,
    Granted       = 1,
    /// <summary>Refused, or switched off in the system settings (<see cref="Notifications.OpenSettings"/>).</summary>
    Denied        = 2,
}

public enum NotificationEventType
{
    /// <summary>The answer to <see cref="Notifications.RequestPermission"/>: <c>Value</c> is a <see cref="NotificationPermission"/>.</summary>
    Permission  = 1,
    /// <summary>One pending notification: <c>Value</c> = id, <c>Time</c> = fire time (unix seconds, UTC).</summary>
    Pending     = 2,
    /// <summary>End of the list started by <see cref="Notifications.QueryPending"/>.</summary>
    PendingDone = 3,
}

public readonly struct NotificationEvent
{
    public NotificationEventType Type { get; init; }
    public int Value { get; init; }
    public long Time { get; init; }
}

public static class Notifications
{
    public const int MaxId = 63;

    /// <summary>True when this platform has a backend (Android, iOS).</summary>
#if __ANDROID__ || __IOS__
    public const bool Available = true;
#else
    public const bool Available = false;
#endif

    /// <summary>Raised from within <see cref="Poll"/>, on the polling thread.</summary>
    public static event Action<NotificationEvent>? OnEvent;

    static readonly Queue<NotificationEvent> _stubQueue = new();

    public static void Init()
    {
#if __ANDROID__ || __IOS__
        SokolNotifications.Init();
#endif
    }

    /// <summary>Android: the channel name the user sees in the system settings, and an optional small
    /// icon — a path inside the APK's assets, a white silhouette on transparency (null = the app icon).
    /// No-op on iOS.</summary>
    public static void Configure(string channelName, string? smallIconAsset = null)
    {
#if __ANDROID__ || __IOS__
        SokolNotifications.Configure(channelName, smallIconAsset);
#endif
    }

    public static NotificationPermission Permission
    {
        get
        {
#if __ANDROID__ || __IOS__
            return (NotificationPermission)SokolNotifications.Status();
#else
            return NotificationPermission.Denied;
#endif
        }
    }

    /// <summary>Ask the user (Android 13+, iOS) → a <see cref="NotificationEventType.Permission"/> event.
    /// Answers at once when the outcome is already known.</summary>
    public static void RequestPermission()
    {
#if __ANDROID__ || __IOS__
        SokolNotifications.RequestPermission();
#else
        _stubQueue.Enqueue(new NotificationEvent {
            Type = NotificationEventType.Permission, Value = (int)NotificationPermission.Denied });
#endif
    }

    /// <summary>Show <paramref name="title"/> / <paramref name="body"/> at <paramref name="at"/>.
    /// Android may run late (Doze); past <paramref name="expires"/> the notification is dropped
    /// instead. iOS fires on time.</summary>
    public static void Schedule(int id, DateTimeOffset at, DateTimeOffset? expires, string title, string body)
    {
        if (id < 1 || id > MaxId) throw new ArgumentOutOfRangeException(nameof(id));
#if __ANDROID__ || __IOS__
        SokolNotifications.Schedule(id, at.ToUnixTimeSeconds(), expires?.ToUnixTimeSeconds() ?? 0, title, body);
#endif
    }

    /// <summary>Cancel the pending notification with this id and remove it if already shown.</summary>
    public static void Cancel(int id)
    {
#if __ANDROID__ || __IOS__
        SokolNotifications.Cancel(id);
#endif
    }

    /// <summary>Cancel every pending notification of this plugin and remove the shown ones.</summary>
    public static void CancelAll()
    {
#if __ANDROID__ || __IOS__
        SokolNotifications.CancelAll();
#endif
    }

    /// <summary>List what is pending → <see cref="NotificationEventType.Pending"/> events, then
    /// <see cref="NotificationEventType.PendingDone"/>.</summary>
    public static void QueryPending()
    {
#if __ANDROID__ || __IOS__
        SokolNotifications.QueryPending();
#else
        _stubQueue.Enqueue(new NotificationEvent { Type = NotificationEventType.PendingDone });
#endif
    }

    /// <summary>The id of the notification the user tapped to open the app, once; 0 when none.
    /// Check it at start and whenever the app returns to the foreground.</summary>
    public static int TakeOpened()
    {
#if __ANDROID__ || __IOS__
        return SokolNotifications.TakeOpened();
#else
        return 0;
#endif
    }

    /// <summary>Open this app's notification settings, to switch notifications back on.</summary>
    public static void OpenSettings()
    {
#if __ANDROID__ || __IOS__
        SokolNotifications.OpenSettings();
#endif
    }

    /// <summary>Drain pending events; call once per frame from the game loop.</summary>
    public static void Poll()
    {
        while (_stubQueue.Count > 0)
            OnEvent?.Invoke(_stubQueue.Dequeue());
#if __ANDROID__ || __IOS__
        while (SokolNotifications.PollEvent(out SokolNotifications.Event e))
            OnEvent?.Invoke(new NotificationEvent {
                Type = (NotificationEventType)e.type, Value = e.value, Time = e.time });
#endif
    }
}
