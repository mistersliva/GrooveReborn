using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Reborn.Controls;
using Reborn.Models;
using Reborn.Pages;
using Reborn.Services;
using Windows.Graphics;

namespace Reborn;

public sealed partial class MainWindow : Window
{
    const double PaneWide = 320, PaneCompact = 48, CompactBelow = 1008;

    readonly DispatcherQueueTimer _timer;
    readonly GrooveBackdrop _backdrop = new();
    readonly Dictionary<Type, (Button Button, Rectangle Indicator)> _nav;
    double _volumeBeforeMute = 80;
    bool? _paneOpenByUser;

    PlayerService Player => App.Player;

    public MainWindow()
    {
        InitializeComponent();

        _nav = new()
        {
            [typeof(MyMusicPage)] = (NavMusic, IndMusic),
            [typeof(DetailPage)] = (NavMusic, IndMusic),
            [typeof(RecentPage)] = (NavRecent, IndRecent),
            [typeof(NowPlayingPage)] = (NavNow, IndNow),
            [typeof(PlaylistsPage)] = (NavPlaylists, IndPlaylists),
            [typeof(SettingsPage)] = (NavSettings, IndSettings),
        };
        foreach (var b in new[] { NavMusic, NavRecent, NavNow, NavPlaylists, NavSettings }) Reveal.Attach(b);
        if (!IconFont.UsingGrooveFont)
        {
            // No Groove on this PC: closest system MDL2 glyphs.
            MusicGlyph.Glyph = "\uE8D6";
            NowGlyph.Glyph = "\uE9D2";
            PlaylistGlyph.Glyph = "\uE90B";
        }

        // Own title bar with Windows 10 caption buttons: remove the system one entirely.
        if (AppWindow.Presenter is OverlappedPresenter op) op.SetBorderAndTitleBar(true, false);
        AppWindow.Resize(new SizeInt32(1280, 820));
        AppWindow.Changed += (_, e) => { if (e.DidPresenterChange || e.DidSizeChange) UpdateMaxIcon(); };
        Root.SizeChanged += (_, _) => { UpdatePane(); UpdateDragRegion(); };
        Root.Loaded += (_, _) => { UpdateDragRegion(); IconFont.ApplyTo(Root); };

        SystemBackdrop = _backdrop;
        Root.ActualThemeChanged += (_, _) => OnThemeChanged();
        ApplyTheme(App.Settings.Theme);

        Player.TrackChanged += UpdateTrack;
        Player.StateChanged += UpdateState;
        App.Library.Changed += () => DispatcherQueue.TryEnqueue(UpdateState);

        SeekSlider.DragCompleted += () => Player.Seek(SeekSlider.Value);
        SeekSlider.UserChanged += v => PosText.Text = Fmt.Time(v);
        VolumeSlider.Maximum = 100;
        VolumeSlider.UserChanged += SetVolume;
        SetVolume(Math.Round(App.Settings.Volume * 100));

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(250);
        _timer.Tick += (_, _) => UpdatePosition();
        _timer.Start();

        UpdateTrack();
        UpdateState();
        ContentFrame.Navigate(typeof(MyMusicPage));

        Closed += (_, _) =>
        {
            _timer.Stop();
            App.Settings.Save();
        };
    }

    // ---------- Theme ----------

    public void ApplyTheme(string theme)
    {
        Root.RequestedTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        OnThemeChanged();
    }

    void OnThemeChanged()
    {
        var dark = Root.ActualTheme != ElementTheme.Light;
        Accent.Apply(dark);
        _backdrop.SetDark(dark);
        UpdateState();
    }

    // ---------- Title bar ----------

    void UpdateDragRegion()
    {
        if (Root.XamlRoot == null) return;
        var scale = Root.XamlRoot.RasterizationScale;
        double left = BackButton.Visibility == Visibility.Visible ? 48 : 0;
        double right = 46 * 3;
        var width = Math.Max(0, Root.ActualWidth - left - right);
        var rect = new RectInt32((int)(left * scale), 0, (int)(width * scale), (int)(32 * scale));
        InputNonClientPointerSource.GetForWindowId(AppWindow.Id).SetRegionRects(NonClientRegionKind.Caption, new[] { rect });
    }

    void UpdateMaxIcon()
    {
        var maximized = AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
        MaxIcon.Glyph = maximized ? "" : "";
        AutomationProperties.SetName(MaxButton, maximized ? "Восстановить" : "Развернуть");
    }

    void Minimize_Click(object sender, RoutedEventArgs e) => (AppWindow.Presenter as OverlappedPresenter)?.Minimize();

    void Maximize_Click(object sender, RoutedEventArgs e)
    {
        if (AppWindow.Presenter is not OverlappedPresenter p) return;
        if (p.State == OverlappedPresenterState.Maximized) p.Restore(); else p.Maximize();
    }

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    void Close_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        CloseButton.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xE8, 0x11, 0x23));
        CloseButton.Foreground = new SolidColorBrush(Colors.White);
    }

    void Close_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        CloseButton.ClearValue(Control.BackgroundProperty);
        CloseButton.ClearValue(Control.ForegroundProperty);
    }

    // ---------- Pane ----------

    bool PaneOpen => _paneOpenByUser ?? Root.ActualWidth >= CompactBelow;

    void UpdatePane()
    {
        var open = PaneOpen;
        PaneColumn.Width = new GridLength(open ? PaneWide : PaneCompact);
        SearchField.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        SearchIconButton.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
    }

    void Hamburger_Click(object sender, RoutedEventArgs e)
    {
        _paneOpenByUser = !PaneOpen;
        UpdatePane();
    }

    void SearchIcon_Click(object sender, RoutedEventArgs e)
    {
        _paneOpenByUser = true;
        UpdatePane();
        SearchBox.Focus(FocusState.Programmatic);
    }

    void Search_GotFocus(object sender, RoutedEventArgs e) => SearchField.BorderBrush = Accent.Brush("GrooveAccentText");
    void Search_LostFocus(object sender, RoutedEventArgs e) => SearchField.BorderBrush = new SolidColorBrush(Colors.Transparent);

    void Search_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) RunSearch();
    }

    void SearchButton_Click(object sender, RoutedEventArgs e) => RunSearch();

    void RunSearch()
    {
        var q = SearchBox.Text?.Trim();
        if (!string.IsNullOrEmpty(q)) Navigate(typeof(MyMusicPage), q);
    }

    // ---------- Navigation ----------

    public void Navigate(Type page, object? parameter = null)
    {
        if (ContentFrame.CurrentSourcePageType == page && parameter == null && page != typeof(MyMusicPage))
            return;
        ContentFrame.Navigate(page, parameter, new EntranceNavigationTransitionInfo());
    }

    void Nav_Click(object sender, RoutedEventArgs e)
    {
        switch ((sender as FrameworkElement)?.Tag as string)
        {
            case "music": Navigate(typeof(MyMusicPage)); break;
            case "recent": Navigate(typeof(RecentPage)); break;
            case "now": Navigate(typeof(NowPlayingPage)); break;
            case "playlists": Navigate(typeof(PlaylistsPage)); break;
            case "settings": Navigate(typeof(SettingsPage)); break;
        }
        if (_paneOpenByUser == true && Root.ActualWidth < CompactBelow)
        {
            _paneOpenByUser = null;
            UpdatePane();
        }
    }

    void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        BackButton.Visibility = ContentFrame.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
        TitleText.Margin = new Thickness(ContentFrame.CanGoBack ? 4 : 12, 0, 0, 1);
        foreach (var (_, ind) in _nav.Values) ind.Visibility = Visibility.Collapsed;
        if (_nav.TryGetValue(e.SourcePageType, out var cur)) cur.Indicator.Visibility = Visibility.Visible;
        UpdateDragRegion();
        if (e.Content is FrameworkElement page)
        {
            page.Loaded += (_, _) => IconFont.ApplyTo(page);
        }
    }

    void Back_Click(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.CanGoBack) ContentFrame.GoBack();
    }

    void NowInfo_Tapped(object sender, TappedRoutedEventArgs e) => Navigate(typeof(NowPlayingPage));
    void NowPlaying_Click(object sender, RoutedEventArgs e) => Navigate(typeof(NowPlayingPage));
    void SettingsMenu_Click(object sender, RoutedEventArgs e) => Navigate(typeof(SettingsPage));

    // ---------- Player bar ----------

    void UpdateTrack()
    {
        var t = Player.Current;
        NowInfo.Visibility = t == null ? Visibility.Collapsed : Visibility.Visible;
        NowTitle.Text = t?.Title ?? "";
        NowArtist.Text = t?.Artist ?? "";
        NowCover.Source = Fmt.Image(t?.CoverPath, 180);
        UpdatePosition();
        UpdateState();
    }

    void UpdateState()
    {
        var hasTrack = Player.Current != null;
        PlayIcon.Glyph = Player.IsPlaying ? "" : "";
        PlayIcon.Margin = new Thickness(Player.IsPlaying ? 0 : 3, 0, 0, 0);
        ToolTipService.SetToolTip(PlayButton, Player.IsPlaying ? "Пауза" : "Воспроизвести");
        PlayButton.IsEnabled = hasTrack || App.Library.Tracks.Count > 0;
        PrevButton.IsEnabled = NextButton.IsEnabled = hasTrack;

        SetActive(ShuffleIcon, Player.Shuffle);
        SetActive(RepeatIcon, Player.Repeat != RepeatMode.Off);
        RepeatIcon.Glyph = Player.Repeat == RepeatMode.One ? "" : "";
        ToolTipService.SetToolTip(ShuffleButton, Player.Shuffle ? "Перемешать: вкл." : "Перемешать: выкл.");
        ToolTipService.SetToolTip(RepeatButton, Player.Repeat switch
        {
            RepeatMode.All => "Повтор: все",
            RepeatMode.One => "Повтор: одна",
            _ => "Повтор: выкл.",
        });
    }

    static void SetActive(FontIcon icon, bool active)
    {
        if (active) icon.Foreground = Accent.Brush("GrooveAccentText");
        else icon.ClearValue(IconElement.ForegroundProperty);
    }

    void UpdatePosition()
    {
        var has = Player.Current != null;
        var dur = Player.DurationSec;
        SeekSlider.IsHitTestVisible = has;
        SeekSlider.Maximum = Math.Max(1, dur);
        if (!SeekSlider.IsDragging)
        {
            SeekSlider.Value = has ? Math.Min(Player.PositionSec, SeekSlider.Maximum) : 0;
            PosText.Text = has ? Fmt.Time(Player.PositionSec) : "";
        }
        DurText.Text = has ? Fmt.Time(dur) : "";
    }

    void Play_Click(object sender, RoutedEventArgs e)
    {
        if (Player.Current == null)
        {
            var all = App.Library.Tracks;
            if (all.Count > 0) Player.Play(all.ToList(), Random.Shared.Next(all.Count), shuffle: true);
            return;
        }
        Player.TogglePlay();
    }

    void Prev_Click(object sender, RoutedEventArgs e) => Player.Previous();
    void Next_Click(object sender, RoutedEventArgs e) => Player.Next();
    void Shuffle_Click(object sender, RoutedEventArgs e) => Player.SetShuffle(!Player.Shuffle);
    void Repeat_Click(object sender, RoutedEventArgs e) => Player.CycleRepeat();

    void SetVolume(double v)
    {
        v = Math.Clamp(v, 0, 100);
        VolumeSlider.Value = v;
        Player.Volume = v / 100;
        App.Settings.Volume = v / 100;
        VolumeIcon.Glyph = v <= 0 ? "" : v < 34 ? "" : v < 67 ? "" : "";
        ToolTipService.SetToolTip(VolumeButton, v <= 0 ? "Включить звук" : $"Громкость: {(int)v}");
    }

    void Mute_Click(object sender, RoutedEventArgs e)
    {
        if (VolumeSlider.Value > 0)
        {
            _volumeBeforeMute = VolumeSlider.Value;
            SetVolume(0);
        }
        else
        {
            SetVolume(_volumeBeforeMute > 0 ? _volumeBeforeMute : 80);
        }
    }
}
