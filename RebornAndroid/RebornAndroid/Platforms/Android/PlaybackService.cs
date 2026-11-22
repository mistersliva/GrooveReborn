using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Media;
using Android.Media.Session;
using Android.OS;

namespace Reborn.Droid;

/// <summary>
/// Foreground media service: keeps playback alive in the background, owns the MediaSession
/// (lock screen, headset buttons, Bluetooth) and shows the media notification.
/// Playback itself lives in PlayerService; this class only mirrors its state.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
public sealed class PlaybackService : Service
{
    const string ChannelId = "playback";
    const int NotificationId = 1;
    const string ActionPlayPause = "com.reborn.music.PLAYPAUSE";
    const string ActionNext = "com.reborn.music.NEXT";
    const string ActionPrev = "com.reborn.music.PREV";
    const string ActionStop = "com.reborn.music.STOP";

    static PlaybackService? _instance;

    MediaSession? _session;
    bool _foreground;
    string? _artPath;
    Bitmap? _art;

    static Services.PlayerService? Player => App.PlayerOrNull;

    /// <summary>Called by PlayerService (UI thread) whenever track or state changes.</summary>
    public static void Sync()
    {
        var p = Player;
        if (p == null) return;
        if (_instance != null)
        {
            _instance.Refresh();
            return;
        }
        if (p.Current != null && p.IsPlaying)
        {
            var ctx = Android.App.Application.Context;
            try { ctx.StartForegroundService(new Intent(ctx, typeof(PlaybackService))); }
            catch (Exception ex) { App.Log("service", ex); }
        }
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnCreate()
    {
        base.OnCreate();
        _instance = this;

        var nm = (NotificationManager)GetSystemService(NotificationService)!;
        var channel = new NotificationChannel(ChannelId, "Воспроизведение", NotificationImportance.Low)
        {
            Description = "Управление воспроизведением",
        };
        channel.SetShowBadge(false);
        nm.CreateNotificationChannel(channel);

        _session = new MediaSession(this, "RebornMusic");
        _session.SetCallback(new SessionCallback());
        _session.SetSessionActivity(LaunchIntent());
        _session.Active = true;
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // Must enter the foreground quickly after startForegroundService.
        if (!_foreground) GoForeground(Build());

        var p = Player;
        switch (intent?.Action)
        {
            case ActionPlayPause: App.RunOnUi(() => p?.TogglePlay()); break;
            case ActionNext: App.RunOnUi(() => p?.Next()); break;
            case ActionPrev: App.RunOnUi(() => p?.Previous()); break;
            case ActionStop: App.RunOnUi(() => p?.Stop()); break;
        }
        Refresh();
        return StartCommandResult.NotSticky;
    }

    public override void OnTaskRemoved(Intent? rootIntent)
    {
        if (Player?.IsPlaying != true) StopSelf();
        base.OnTaskRemoved(rootIntent);
    }

    public override void OnDestroy()
    {
        _instance = null;
        _session?.Release();
        _session = null;
        _art?.Dispose();
        base.OnDestroy();
    }

    void GoForeground(Notification n)
    {
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
                StartForeground(NotificationId, n, ForegroundService.TypeMediaPlayback);
            else
                StartForeground(NotificationId, n);
            _foreground = true;
        }
        catch (Exception ex)
        {
            App.Log("foreground", ex);
        }
    }

    void Refresh()
    {
        var p = Player;
        if (p?.Current == null)
        {
            StopForeground(StopForegroundFlags.Remove);
            _foreground = false;
            StopSelf();
            return;
        }

        UpdateSession(p);
        var n = Build();
        if (p.IsPlaying)
        {
            GoForeground(n);
        }
        else
        {
            // Paused: keep the notification but let the system (and the user) dismiss it.
            if (_foreground)
            {
                StopForeground(StopForegroundFlags.Detach);
                _foreground = false;
            }
            ((NotificationManager)GetSystemService(NotificationService)!).Notify(NotificationId, n);
        }
    }

    void UpdateSession(Services.PlayerService p)
    {
        if (_session == null || p.Current is not { } t) return;
        if (_artPath != t.CoverPath)
        {
            _art?.Dispose();
            _art = null;
            _artPath = t.CoverPath;
            try { if (t.CoverPath != null) _art = BitmapFactory.DecodeFile(t.CoverPath); } catch { }
        }

        var meta = new MediaMetadata.Builder()
            .PutString(MediaMetadata.MetadataKeyTitle, t.Title)!
            .PutString(MediaMetadata.MetadataKeyArtist, t.Artist)!
            .PutString(MediaMetadata.MetadataKeyAlbum, t.Album)!
            .PutLong(MediaMetadata.MetadataKeyDuration, (long)(p.DurationSec * 1000))!;
        if (_art != null) meta.PutBitmap(MediaMetadata.MetadataKeyAlbumArt, _art);
        _session.SetMetadata(meta.Build());

        var state = new PlaybackState.Builder()
            .SetActions(PlaybackState.ActionPlay | PlaybackState.ActionPause | PlaybackState.ActionPlayPause |
                        PlaybackState.ActionSkipToNext | PlaybackState.ActionSkipToPrevious |
                        PlaybackState.ActionSeekTo | PlaybackState.ActionStop)!
            .SetState(p.IsPlaying ? PlaybackStateCode.Playing : PlaybackStateCode.Paused,
                      (long)(p.PositionSec * 1000), p.IsPlaying ? 1f : 0f)!
            .Build();
        _session.SetPlaybackState(state);
    }

    Notification Build()
    {
        var p = Player;
        var t = p?.Current;
        bool playing = p?.IsPlaying == true;

        var b = new Notification.Builder(this, ChannelId)
            .SetSmallIcon(Resource.Drawable.ic_stat_music)!
            .SetContentTitle(t?.Title ?? "Reborn Music")!
            .SetContentText(t?.Artist ?? "")!
            .SetSubText(t?.Album)!
            .SetContentIntent(LaunchIntent())!
            .SetDeleteIntent(Action(ActionStop))!
            .SetVisibility(NotificationVisibility.Public)!
            .SetOngoing(playing)!
            .SetShowWhen(false)!
            .AddAction(NotifAction(Android.Resource.Drawable.IcMediaPrevious, "Назад", ActionPrev))!
            .AddAction(playing
                ? NotifAction(Android.Resource.Drawable.IcMediaPause, "Пауза", ActionPlayPause)
                : NotifAction(Android.Resource.Drawable.IcMediaPlay, "Воспроизвести", ActionPlayPause))!
            .AddAction(NotifAction(Android.Resource.Drawable.IcMediaNext, "Далее", ActionNext))!;
        if (_art != null) b.SetLargeIcon(_art);

        var style = new Notification.MediaStyle().SetShowActionsInCompactView(0, 1, 2);
        if (_session != null) style.SetMediaSession(_session.SessionToken);
        b.SetStyle(style);
        return b.Build()!;
    }

    Notification.Action NotifAction(int icon, string title, string action) =>
        new Notification.Action.Builder(Icon.CreateWithResource(this, icon), title, Action(action)).Build()!;

    PendingIntent Action(string action)
    {
        var i = new Intent(this, typeof(PlaybackService)).SetAction(action);
        return PendingIntent.GetService(this, action.GetHashCode(), i, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
    }

    PendingIntent LaunchIntent()
    {
        var i = new Intent(this, typeof(MainActivity)).AddFlags(ActivityFlags.SingleTop | ActivityFlags.ReorderToFront);
        return PendingIntent.GetActivity(this, 0, i, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
    }

    sealed class SessionCallback : MediaSession.Callback
    {
        public override void OnPlay() => App.RunOnUi(() => Player?.Resume());
        public override void OnPause() => App.RunOnUi(() => Player?.Pause());
        public override void OnSkipToNext() => App.RunOnUi(() => Player?.Next());
        public override void OnSkipToPrevious() => App.RunOnUi(() => Player?.Previous());
        public override void OnStop() => App.RunOnUi(() => Player?.Stop());
        public override void OnSeekTo(long pos) => App.RunOnUi(() => Player?.Seek(pos / 1000.0));
    }
}
