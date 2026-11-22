using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Reborn.Controls;
using Reborn.Droid;
using Reborn.Models;
using Reborn.Pages;
using Reborn.Services;
using Windows.UI.Core;
using Windows.UI.ViewManagement;

namespace Reborn;

public sealed partial class Shell : UserControl
{
    const double DockPaneAbove = 1008, WideBarAbove = 720;

    readonly DispatcherQueueTimer _timer;
    readonly Dictionary<Type, Rectangle> _nav;
    double _volumeBeforeMute = 80;
    bool _wide;

    PlayerService Player => App.Player;

    public Shell()
    {
        InitializeComponent();

        _nav = new()
        {
            [typeof(MyMusicPage)] = IndMusic,
            [typeof(DetailPage)] = IndMusic,
            [typeof(RecentPage)] = IndRecent,
            [typeof(NowPlayingPage)] = IndNow,
            [typeof(SettingsPage)] = IndSettings,
        };

        Root.SizeChanged += (_, _) => UpdateLayoutMode();
        Loaded += OnLoaded;

        ApplyTheme(App.Settings.Theme);

        Player.TrackChanged += UpdateTrack;
        Player.StateChanged += UpdateState;
        App.Library.Changed += () => DispatcherQueue.TryEnqueue(UpdateState);

        foreach (var s in new[] { WSeek, NSeek })
        {
            var slider = s;
            slider.DragCompleted += () => Player.Seek(slider.Value);
            slider.UserChanged += v => { WPos.Text = NPos.Text = Fmt.Time(v); };
        }
        foreach (var s in new[] { WVolume, NVolume })
        {
            s.Maximum = 100;
            s.UserChanged += SetVolume;
            s.DragCompleted += App.Settings.Save;
        }
        SetVolume(Math.Round(App.Settings.Volume * 100));

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(250);
        _timer.Tick += (_, _) => UpdatePosition();
        _timer.Start();

        UpdateTrack();
        UpdateState();
        ContentFrame.Navigate(typeof(MyMusicPage));
    }

    async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
        }
        catch (Exception ex) { App.Log("back", ex); }

        try
        {
            var view = ApplicationView.GetForCurrentView();
            view.VisibleBoundsChanged += (_, _) => UpdateInsets();
        }
        catch { }
        UpdateInsets();
        UpdateLayoutMode();
        SetSystemBars(Accent.IsDark);

        await RequestAccessAndScanAsync();
    }

    public async Task RequestAccessAndScanAsync(bool openSettingsIfBlocked = false)
    {
        if (await MainActivity.RequestPermissionsAsync(openSettingsIfBlocked))
            _ = App.Library.ScanAsync();
        else
            App.Library.NotifyNoAccess();
    }

    // ---------- System bars (edge-to-edge on Android 15+) ----------

    void UpdateInsets()
    {
        try
        {
            var view = ApplicationView.GetForCurrentView();
            var vb = view.VisibleBounds;
            var bounds = XamlRoot?.Size ?? new Windows.Foundation.Size(Root.ActualWidth, Root.ActualHeight);
            double top = Math.Max(0, vb.Top);
            double left = Math.Max(0, vb.Left);
            double right = Math.Max(0, bounds.Width - vb.Right);
            double bottom = Math.Max(0, bounds.Height - vb.Bottom);
            if (bounds.Width <= 0 || vb.Width <= 0) return;
            Root.Padding = new Thickness(left, top, right, bottom);
        }
        catch { }
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
        var dark = Root.ActualTheme != ElementTheme.Light;
        if (theme == "Dark") dark = true;
        else if (theme == "Light") dark = false;
        Accent.Apply(dark);
        RequestedTheme = Root.RequestedTheme;
        SetSystemBars(dark);
        UpdateState();
    }

    void SetSystemBars(bool dark)
    {
        // Uno drives the Android status bar through the WinUI StatusBar API; set it there and natively.
        try
        {
            StatusBar.GetForCurrentView().ForegroundColor = dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
        }
        catch (Exception ex) { App.Log("statusbar", ex); }
        MainActivity.SetLightSystemBars(!dark);
    }

    // ---------- Pane ----------

    void UpdateLayoutMode()
    {
        var w = Root.ActualWidth - Root.Padding.Left - Root.Padding.Right;
        if (w <= 0) return;

        bool dock = w >= DockPaneAbove;
        Split.DisplayMode = dock ? SplitViewDisplayMode.Inline : SplitViewDisplayMode.Overlay;
        Split.IsPaneOpen = dock;
        Split.OpenPaneLength = dock ? 320 : Math.Min(320, w - 56);
        TopHamburger.Visibility = dock ? Visibility.Collapsed : Visibility.Visible;
        PaneHamburger.Visibility = dock ? Visibility.Collapsed : Visibility.Visible;
        TitleText.Margin = new Thickness(dock && BackButton.Visibility != Visibility.Visible ? 16 : 4, 0, 0, 0);

        _wide = w >= WideBarAbove;
        WideBar.Visibility = _wide ? Visibility.Visible : Visibility.Collapsed;
        NarrowBar.Visibility = _wide ? Visibility.Collapsed : Visibility.Visible;
    }

    bool PaneIsOverlay => Split.DisplayMode == SplitViewDisplayMode.Overlay;

    void Hamburger_Click(object sender, RoutedEventArgs e)
    {
        if (PaneIsOverlay) Split.IsPaneOpen = !Split.IsPaneOpen;
    }

    void ClosePaneIfOverlay()
    {
        if (PaneIsOverlay) Split.IsPaneOpen = false;
    }

    void Search_GotFocus(object sender, RoutedEventArgs e) => SearchField.BorderBrush = Accent.Brush("GrooveAccentText");
    void Search_LostFocus(object sender, RoutedEventArgs e) => SearchField.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    void Search_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) RunSearch();
    }

    void SearchButton_Click(object sender, RoutedEventArgs e) => RunSearch();

    void RunSearch()
    {
        var q = SearchBox.Text?.Trim();
        if (string.IsNullOrEmpty(q)) return;
        Navigate(typeof(MyMusicPage), q);
        ClosePaneIfOverlay();
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
            case "settings": Navigate(typeof(SettingsPage)); break;
        }
        ClosePaneIfOverlay();
    }

    void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        BackButton.Visibility = ContentFrame.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
        foreach (var ind in _nav.Values) ind.Visibility = Visibility.Collapsed;
        if (_nav.TryGetValue(e.SourcePageType, out var cur)) cur.Visibility = Visibility.Visible;
        UpdateLayoutMode();
    }

    void Back_Click(object sender, RoutedEventArgs e) => GoBack();

    bool GoBack()
    {
        if (PaneIsOverlay && Split.IsPaneOpen)
        {
            Split.IsPaneOpen = false;
            return true;
        }
        if (ContentFrame.CanGoBack)
        {
            ContentFrame.GoBack();
            return true;
        }
        return false;
    }

    void OnBackRequested(object? sender, BackRequestedEventArgs e)
    {
        if (GoBack())
        {
            e.Handled = true;
            return;
        }
        // Keep the process (and the music) alive: behave like the Home button instead of finishing the activity.
        e.Handled = true;
        MainActivity.Instance?.MoveTaskToBack(true);
    }

    void NowInfo_Tapped(object sender, TappedRoutedEventArgs e) => Navigate(typeof(NowPlayingPage));

    // ---------- Player bar ----------

    void UpdateTrack()
    {
        var t = Player.Current;
        WNowInfo.Visibility = NNowInfo.Visibility = t == null ? Visibility.Collapsed : Visibility.Visible;
        WTitle.Text = NTitle.Text = t?.Title ?? "";
        WArtist.Text = NArtist.Text = t?.Artist ?? "";
        WCover.Source = Fmt.Image(t?.CoverPath, 180);
        NCover.Source = Fmt.Image(t?.CoverPath, 120);
        UpdatePosition();
        UpdateState();
    }

    void UpdateState()
    {
        var hasTrack = Player.Current != null;
        var playing = Player.IsPlaying;
        foreach (var icon in new[] { WPlayIcon, NPlayIcon })
        {
            icon.Glyph = playing ? "" : "";
            icon.Margin = new Thickness(playing ? 0 : 3, 0, 0, 0);
        }
        AutomationProperties.SetName(WPlay, playing ? "Пауза" : "Воспроизвести");
        AutomationProperties.SetName(NPlay, playing ? "Пауза" : "Воспроизвести");
        WPlay.IsEnabled = NPlay.IsEnabled = hasTrack || App.Library.Tracks.Count > 0;
        WPrev.IsEnabled = WNext.IsEnabled = NPrev.IsEnabled = NNext.IsEnabled = hasTrack;

        var repeatGlyph = Player.Repeat == RepeatMode.One ? "" : "";
        foreach (var i in new[] { WShuffleIcon, NShuffleIcon }) SetActive(i, Player.Shuffle);
        foreach (var i in new[] { WRepeatIcon, NRepeatIcon })
        {
            SetActive(i, Player.Repeat != RepeatMode.Off);
            i.Glyph = repeatGlyph;
        }
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
        var pos = Player.PositionSec;
        foreach (var s in new[] { WSeek, NSeek })
        {
            s.Interactive = has;
            s.Maximum = Math.Max(1, dur);
            if (!s.IsDragging) s.Value = has ? Math.Min(pos, s.Maximum) : 0;
        }
        if (!WSeek.IsDragging && !NSeek.IsDragging) WPos.Text = NPos.Text = has ? Fmt.Time(pos) : "";
        WDur.Text = NDur.Text = has ? Fmt.Time(dur) : "";
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
        v = Math.Clamp(Math.Round(v), 0, 100);
        WVolume.Value = NVolume.Value = v;
        Player.Volume = v / 100;
        App.Settings.Volume = v / 100;
        var glyph = v <= 0 ? "" : v < 34 ? "" : v < 67 ? "" : "";
        WVolumeIcon.Glyph = NVolumeIcon.Glyph = NMuteIcon.Glyph = glyph;
        NVolumeText.Text = ((int)v).ToString();
    }

    void Mute_Click(object sender, RoutedEventArgs e)
    {
        if (WVolume.Value > 0)
        {
            _volumeBeforeMute = WVolume.Value;
            SetVolume(0);
        }
        else
        {
            SetVolume(_volumeBeforeMute > 0 ? _volumeBeforeMute : 80);
        }
    }
}
