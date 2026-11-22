using Microsoft.UI.Dispatching;
using Reborn.Models;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Reborn.Services;

public enum RepeatMode { Off, All, One }

/// <summary>Playback queue on top of Windows MediaPlayer. All events fire on the UI thread.</summary>
public sealed class PlayerService
{
    readonly MediaPlayer _mp = new() { AutoPlay = false, AudioCategory = MediaPlayerAudioCategory.Media };
    readonly SystemMediaTransportControls _smtc;
    readonly DispatcherQueue _ui;
    readonly Random _rng = new();

    List<Track> _queue = new();
    List<int> _order = new();
    int _pos = -1;
    int _loadToken;
    int _failStreak;

    public event Action? TrackChanged;
    public event Action? StateChanged;
    public event Action? QueueChanged;

    public bool Shuffle { get; private set; }
    public RepeatMode Repeat { get; private set; }

    public Track? Current => _pos >= 0 && _pos < _order.Count ? _queue[_order[_pos]] : null;
    public int Position => _pos;
    public IReadOnlyList<QueueEntry> UpNext =>
        _order.Select((qi, i) => new QueueEntry { Index = i, Track = _queue[qi] }).ToList();

    public bool IsPlaying => _mp.PlaybackSession.PlaybackState is MediaPlaybackState.Playing
                                                              or MediaPlaybackState.Opening
                                                              or MediaPlaybackState.Buffering;
    public double PositionSec => _mp.PlaybackSession.Position.TotalSeconds;
    public double DurationSec
    {
        get
        {
            var d = _mp.PlaybackSession.NaturalDuration.TotalSeconds;
            return d > 0 ? d : Current?.DurationSec ?? 0;
        }
    }

    public double Volume
    {
        get => _mp.Volume;
        set => _mp.Volume = Math.Clamp(value, 0, 1);
    }

    public PlayerService(DispatcherQueue ui)
    {
        _ui = ui;
        _mp.CommandManager.IsEnabled = false;
        _smtc = _mp.SystemMediaTransportControls;
        _smtc.IsEnabled = true;
        _smtc.IsPlayEnabled = _smtc.IsPauseEnabled = _smtc.IsNextEnabled = _smtc.IsPreviousEnabled = true;
        _smtc.ButtonPressed += (_, e) => _ui.TryEnqueue(() =>
        {
            switch (e.Button)
            {
                case SystemMediaTransportControlsButton.Play: Resume(); break;
                case SystemMediaTransportControlsButton.Pause: _mp.Pause(); break;
                case SystemMediaTransportControlsButton.Next: Next(); break;
                case SystemMediaTransportControlsButton.Previous: Previous(); break;
            }
        });

        _mp.MediaEnded += (_, _) => _ui.TryEnqueue(() => Advance(auto: true));
        _mp.MediaOpened += (_, _) => _ui.TryEnqueue(() => _failStreak = 0);
        _mp.MediaFailed += (_, _) => _ui.TryEnqueue(OnFailed);
        _mp.PlaybackSession.PlaybackStateChanged += (s, _) => _ui.TryEnqueue(() =>
        {
            _smtc.PlaybackStatus = s.PlaybackState switch
            {
                MediaPlaybackState.Playing => MediaPlaybackStatus.Playing,
                MediaPlaybackState.Paused => MediaPlaybackStatus.Paused,
                _ => _smtc.PlaybackStatus,
            };
            StateChanged?.Invoke();
        });
    }

    /// <summary>Replace the queue and start playing.</summary>
    public void Play(IList<Track> tracks, int start = 0, bool? shuffle = null)
    {
        if (tracks.Count == 0) return;
        _queue = tracks.ToList();
        if (shuffle.HasValue) Shuffle = shuffle.Value;
        start = Math.Clamp(start, 0, _queue.Count - 1);
        BuildOrder(start);
        _failStreak = 0;
        Load(autoplay: true);
        QueueChanged?.Invoke();
        StateChanged?.Invoke();
    }

    public void PlayAt(int orderIndex)
    {
        if (orderIndex < 0 || orderIndex >= _order.Count) return;
        _pos = orderIndex;
        Load(autoplay: true);
    }

    public void TogglePlay()
    {
        if (Current == null) return;
        if (_mp.PlaybackSession.PlaybackState == MediaPlaybackState.Playing) _mp.Pause();
        else Resume();
    }

    void Resume()
    {
        if (Current == null) return;
        if (_mp.Source == null) Load(autoplay: true);
        else _mp.Play();
    }

    public void Next() => Advance(auto: false);

    public void Previous()
    {
        if (Current == null) return;
        if (PositionSec > 3 || _pos == 0)
        {
            _mp.PlaybackSession.Position = TimeSpan.Zero;
            return;
        }
        _pos--;
        Load(autoplay: true);
    }

    public void Seek(double seconds)
    {
        if (Current == null) return;
        _mp.PlaybackSession.Position = TimeSpan.FromSeconds(Math.Max(0, seconds));
    }

    public void SetShuffle(bool value)
    {
        Shuffle = value;
        if (Current != null) BuildOrder(_order[_pos]);
        QueueChanged?.Invoke();
        StateChanged?.Invoke();
    }

    public void CycleRepeat()
    {
        Repeat = Repeat switch { RepeatMode.Off => RepeatMode.All, RepeatMode.All => RepeatMode.One, _ => RepeatMode.Off };
        StateChanged?.Invoke();
    }

    void BuildOrder(int firstQueueIndex)
    {
        _order = Enumerable.Range(0, _queue.Count).ToList();
        if (Shuffle)
        {
            _order.RemoveAt(firstQueueIndex);
            for (int i = _order.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }
            _order.Insert(0, firstQueueIndex);
            _pos = 0;
        }
        else
        {
            _pos = firstQueueIndex;
        }
    }

    void Advance(bool auto)
    {
        if (Current == null) return;
        if (auto && Repeat == RepeatMode.One)
        {
            _mp.PlaybackSession.Position = TimeSpan.Zero;
            _mp.Play();
            return;
        }

        if (_pos + 1 < _order.Count)
        {
            _pos++;
            Load(autoplay: true);
        }
        else if (Repeat != RepeatMode.Off)
        {
            _pos = 0;
            Load(autoplay: true);
        }
        else if (auto)
        {
            // End of the queue: stop on the first track, like a fresh start.
            _pos = 0;
            Load(autoplay: false);
        }
        else
        {
            _pos = 0;
            Load(autoplay: true);
        }
    }

    void OnFailed()
    {
        if (++_failStreak >= Math.Min(_order.Count, 10))
        {
            _mp.Pause();
            StateChanged?.Invoke();
            return;
        }
        Advance(auto: false);
    }

    async void Load(bool autoplay)
    {
        var track = Current;
        if (track == null) return;
        int token = ++_loadToken;
        TrackChanged?.Invoke();

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(track.Path);
            if (token != _loadToken) return;
            _mp.Source = MediaSource.CreateFromStorageFile(file);
            if (autoplay) _mp.Play();
        }
        catch
        {
            if (token == _loadToken) OnFailed();
            return;
        }

        StateChanged?.Invoke();
        await UpdateSmtcAsync(track, token);
    }

    async Task UpdateSmtcAsync(Track track, int token)
    {
        try
        {
            var u = _smtc.DisplayUpdater;
            u.ClearAll();
            u.Type = MediaPlaybackType.Music;
            u.MusicProperties.Title = track.Title;
            u.MusicProperties.Artist = track.Artist;
            u.MusicProperties.AlbumTitle = track.Album;
            if (track.CoverPath != null)
            {
                var cover = await StorageFile.GetFileFromPathAsync(track.CoverPath);
                if (token != _loadToken) return;
                u.Thumbnail = RandomAccessStreamReference.CreateFromFile(cover);
            }
            u.Update();
        }
        catch { }
    }
}
