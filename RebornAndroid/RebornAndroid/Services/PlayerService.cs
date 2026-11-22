using Android.Content;
using Android.Media;
using Microsoft.UI.Dispatching;
using Reborn.Droid;
using Reborn.Models;
using AUri = Android.Net.Uri;

namespace Reborn.Services;

public enum RepeatMode { Off, All, One }

/// <summary>
/// Playback queue on top of Android MediaPlayer. All events fire on the UI thread.
/// The PlaybackService keeps the process alive in the background and shows the media notification.
/// </summary>
public sealed class PlayerService
{
    readonly Context _ctx = Android.App.Application.Context;
    readonly MediaPlayer _mp = new();
    readonly AudioManager _audio;
    readonly AudioAttributes _attrs;
    readonly AudioFocusRequestClass _focusRequest;
    readonly DispatcherQueue _ui;
    readonly Random _rng = new();
    readonly NoisyReceiver _noisy;

    List<Track> _queue = new();
    List<int> _order = new();
    int _pos = -1;
    int _loadToken;
    int _failStreak;
    bool _prepared, _wantPlay, _resumeOnFocus, _noisyRegistered;
    double _volume = 1;

    public event Action? TrackChanged;
    public event Action? StateChanged;
    public event Action? QueueChanged;

    public bool Shuffle { get; private set; }
    public RepeatMode Repeat { get; private set; }

    public Track? Current => _pos >= 0 && _pos < _order.Count ? _queue[_order[_pos]] : null;
    public int Position => _pos;
    public IReadOnlyList<TrackRow> UpNext => _order.Select((qi, i) => new TrackRow(_queue[qi], i, i == _pos)).ToList();

    public bool IsPlaying => _prepared ? SafeIsPlaying() : _wantPlay;
    public double PositionSec => _prepared ? SafeInt(() => _mp.CurrentPosition) / 1000.0 : 0;
    public double DurationSec
    {
        get
        {
            var d = _prepared ? SafeInt(() => _mp.Duration) / 1000.0 : 0;
            return d > 0 ? d : Current?.DurationSec ?? 0;
        }
    }

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 1);
            // Perceptual curve: the slider feels linear like the Windows one.
            var v = (float)(_volume * _volume);
            try { _mp.SetVolume(v, v); } catch { }
        }
    }

    public PlayerService(DispatcherQueue ui)
    {
        _ui = ui;
        _audio = (AudioManager)_ctx.GetSystemService(Context.AudioService)!;
        _attrs = new AudioAttributes.Builder()!
            .SetUsage(AudioUsageKind.Media)!
            .SetContentType(AudioContentType.Music)!
            .Build()!;
        _focusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain)
            .SetAudioAttributes(_attrs)
            .SetOnAudioFocusChangeListener(new FocusListener(this))
            .Build();
        _noisy = new NoisyReceiver(this);

        _mp.SetAudioAttributes(_attrs);
        _mp.SetWakeMode(_ctx, Android.OS.WakeLockFlags.Partial);
        _mp.Prepared += (_, _) => Post(OnPrepared);
        _mp.Completion += (_, _) => Post(() => { if (_prepared) Advance(auto: true); });
        _mp.Error += (_, e) =>
        {
            e.Handled = true;
            Post(OnFailed);
        };
    }

    void Post(Action a)
    {
        if (_ui.HasThreadAccess) a();
        else _ui.TryEnqueue(() => a());
    }

    bool SafeIsPlaying()
    {
        try { return _mp.IsPlaying; } catch { return false; }
    }

    static int SafeInt(Func<int> f)
    {
        try { return f(); } catch { return 0; }
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
        if (IsPlaying) Pause();
        else Resume();
    }

    public void Pause()
    {
        _wantPlay = false;
        _resumeOnFocus = false;
        if (_prepared && SafeIsPlaying()) _mp.Pause();
        Changed();
    }

    public void Resume()
    {
        if (Current == null) return;
        if (!_prepared)
        {
            if (_wantPlay) return;
            Load(autoplay: true);
            return;
        }
        StartPlayback();
    }

    void StartPlayback()
    {
        if (_audio.RequestAudioFocus(_focusRequest) != AudioFocusRequest.Granted) return;
        RegisterNoisy(true);
        _wantPlay = true;
        try { _mp.Start(); } catch { }
        Changed();
    }

    public void Stop()
    {
        _wantPlay = false;
        if (_prepared && SafeIsPlaying()) _mp.Pause();
        _audio.AbandonAudioFocusRequest(_focusRequest);
        RegisterNoisy(false);
        Changed();
    }

    public void Next() => Advance(auto: false);

    public void Previous()
    {
        if (Current == null) return;
        if (PositionSec > 3 || _pos == 0)
        {
            Seek(0);
            return;
        }
        _pos--;
        Load(autoplay: true);
    }

    public void Seek(double seconds)
    {
        if (Current == null || !_prepared) return;
        try { _mp.SeekTo((int)(Math.Max(0, seconds) * 1000)); } catch { }
        Changed();
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
            try
            {
                _mp.SeekTo(0);
                _mp.Start();
            }
            catch { }
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
        else
        {
            // End of the queue on its own: stop on the first track, like a fresh start.
            _pos = 0;
            Load(autoplay: !auto);
        }
    }

    void OnFailed()
    {
        _prepared = false;
        if (++_failStreak >= Math.Min(_order.Count, 10))
        {
            _wantPlay = false;
            Changed();
            return;
        }
        Advance(auto: false);
    }

    void Load(bool autoplay)
    {
        var track = Current;
        if (track == null) return;
        ++_loadToken;
        _prepared = false;
        _wantPlay = autoplay;
        TrackChanged?.Invoke();

        try
        {
            _mp.Reset();
            _mp.SetAudioAttributes(_attrs);
            _mp.SetDataSource(_ctx, AUri.Parse(track.Path)!);
            _mp.PrepareAsync();
        }
        catch (Exception ex)
        {
            App.Log("load", ex);
            OnFailed();
            return;
        }
        Changed();
    }

    void OnPrepared()
    {
        _prepared = true;
        _failStreak = 0;
        Volume = _volume;
        if (_wantPlay) StartPlayback();
        else Changed();
    }

    void Changed()
    {
        StateChanged?.Invoke();
        PlaybackService.Sync();
    }

    void OnFocusChange(AudioFocus focus)
    {
        switch (focus)
        {
            case AudioFocus.Loss:
                Pause();
                break;
            case AudioFocus.LossTransient:
            case AudioFocus.LossTransientCanDuck:
                if (IsPlaying)
                {
                    Pause();
                    _resumeOnFocus = true;
                }
                break;
            case AudioFocus.Gain:
                if (_resumeOnFocus)
                {
                    _resumeOnFocus = false;
                    Resume();
                }
                break;
        }
    }

    void RegisterNoisy(bool on)
    {
        try
        {
            if (on && !_noisyRegistered)
            {
                var filter = new IntentFilter(AudioManager.ActionAudioBecomingNoisy);
                if (OperatingSystem.IsAndroidVersionAtLeast(33))
                    _ctx.RegisterReceiver(_noisy, filter, ReceiverFlags.NotExported);
                else
                    _ctx.RegisterReceiver(_noisy, filter);
                _noisyRegistered = true;
            }
            else if (!on && _noisyRegistered)
            {
                _ctx.UnregisterReceiver(_noisy);
                _noisyRegistered = false;
            }
        }
        catch { }
    }

    sealed class FocusListener : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
    {
        readonly PlayerService _p;
        public FocusListener(PlayerService p) => _p = p;
        public void OnAudioFocusChange(AudioFocus focusChange) => _p.Post(() => _p.OnFocusChange(focusChange));
    }

    /// <summary>Headphones unplugged: pause, as every player does.</summary>
    sealed class NoisyReceiver : BroadcastReceiver
    {
        readonly PlayerService _p;
        public NoisyReceiver(PlayerService p) => _p = p;
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action == AudioManager.ActionAudioBecomingNoisy) _p.Post(_p.Pause);
        }
    }
}
