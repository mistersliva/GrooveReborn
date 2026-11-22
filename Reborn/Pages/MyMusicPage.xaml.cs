using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Reborn.Models;

namespace Reborn.Pages;

public sealed partial class MyMusicPage : Page
{
    enum Sort { Added, Title, Artist, Album, Year }

    static int _lastTab = 2; // Albums
    static Sort _songSort = Sort.Added, _albumSort = Sort.Added;
    static string? _genre;
    static bool _bannerClosed;

    string? _query;
    List<Track> _songs = new();

    public MyMusicPage()
    {
        InitializeComponent();
        App.Library.Changed += () => DispatcherQueue.TryEnqueue(Refresh);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _query = e.Parameter as string;
        if (string.IsNullOrWhiteSpace(_query)) _query = null;
        Refresh();
    }

    static bool Match(string s, string q) => s.Contains(q, StringComparison.CurrentCultureIgnoreCase);

    void Refresh()
    {
        var lib = App.Library;
        var q = _query;
        var cmp = StringComparer.CurrentCultureIgnoreCase;

        IEnumerable<Track> songs = lib.Tracks;
        IEnumerable<Album> albums = lib.Albums;
        IEnumerable<Artist> artists = lib.Artists;
        if (q != null)
        {
            songs = songs.Where(t => Match(t.Title, q) || Match(t.Artist, q) || Match(t.Album, q));
            albums = albums.Where(a => Match(a.Title, q) || Match(a.Artist, q));
            artists = artists.Where(a => Match(a.Name, q));
        }
        if (_genre != null)
        {
            songs = songs.Where(t => string.Equals(t.Genre, _genre, StringComparison.CurrentCultureIgnoreCase));
            albums = albums.Where(a => a.Tracks.Any(t => string.Equals(t.Genre, _genre, StringComparison.CurrentCultureIgnoreCase)));
        }

        songs = _songSort switch
        {
            Sort.Title => songs.OrderBy(t => t.Title, cmp),
            Sort.Artist => songs.OrderBy(t => t.Artist, cmp).ThenBy(t => t.Album, cmp).ThenBy(t => t.TrackNumber),
            Sort.Album => songs.OrderBy(t => t.Album, cmp).ThenBy(t => t.Disc).ThenBy(t => t.TrackNumber),
            _ => songs.OrderByDescending(t => t.Modified),
        };
        albums = _albumSort switch
        {
            Sort.Title => albums.OrderBy(a => a.Title, cmp),
            Sort.Artist => albums.OrderBy(a => a.Artist, cmp).ThenBy(a => a.Year),
            Sort.Year => albums.OrderByDescending(a => a.Year).ThenBy(a => a.Title, cmp),
            _ => albums.OrderByDescending(a => a.Tracks.Max(t => t.Modified)),
        };

        _songs = songs.ToList();
        var albumList = albums.ToList();
        var artistList = artists.ToList();

        Header.Text = q == null ? "Моя музыка" : $"Результаты для «{q}»";
        SongsList.ItemsSource = _songs;
        AlbumsGrid.ItemsSource = albumList;
        ArtistsGrid.ItemsSource = artistList;

        ShuffleText.Text = $"Перемешать все ({_songs.Count})";
        ShuffleAllButton.Visibility = _songs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ScanIndicator.Visibility = lib.IsScanning ? Visibility.Visible : Visibility.Collapsed;
        FoldersBanner.Visibility = q == null && !_bannerClosed && lib.Tracks.Count == 0 && !lib.IsScanning
            ? Visibility.Visible : Visibility.Collapsed;

        BuildGenreMenu();
        UpdateTabs();
        EmptyText.Text = q == null ? "Здесь пока ничего нет" : "Ничего не найдено";
    }

    // ---------- Tabs ----------

    void Tab_Click(object sender, RoutedEventArgs e)
    {
        _lastTab = Tabs.Children.IndexOf((UIElement)sender);
        UpdateTabs();
    }

    void UpdateTabs()
    {
        for (int i = 0; i < Tabs.Children.Count; i++)
            ((ToggleButton)Tabs.Children[i]).IsChecked = i == _lastTab;

        int count = _lastTab switch
        {
            0 => _songs.Count,
            1 => (ArtistsGrid.ItemsSource as System.Collections.ICollection)?.Count ?? 0,
            _ => (AlbumsGrid.ItemsSource as System.Collections.ICollection)?.Count ?? 0,
        };
        bool empty = count == 0 && !App.Library.IsScanning;
        SongsList.Visibility = !empty && _lastTab == 0 ? Visibility.Visible : Visibility.Collapsed;
        ArtistsGrid.Visibility = !empty && _lastTab == 1 ? Visibility.Visible : Visibility.Collapsed;
        AlbumsGrid.Visibility = !empty && _lastTab == 2 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;

        SortBlock.Visibility = _lastTab == 1 ? Visibility.Collapsed : Visibility.Visible;
        GenreBlock.Visibility = _lastTab == 1 ? Visibility.Collapsed : Visibility.Visible;
        BuildSortMenu();
    }

    // ---------- Sort & genre ----------

    static string SortName(Sort s) => s switch
    {
        Sort.Title => "А–Я",
        Sort.Artist => "Исполнитель",
        Sort.Album => "Альбом",
        Sort.Year => "Год выпуска",
        _ => "Дата добавления",
    };

    void BuildSortMenu()
    {
        var options = _lastTab == 0
            ? new[] { Sort.Added, Sort.Title, Sort.Artist, Sort.Album }
            : new[] { Sort.Added, Sort.Title, Sort.Artist, Sort.Year };
        var current = _lastTab == 0 ? _songSort : _albumSort;
        SortText.Text = SortName(current);
        SortMenu.Items.Clear();
        foreach (var o in options)
        {
            var item = new ToggleMenuFlyoutItem { Text = SortName(o), IsChecked = o == current };
            item.Click += (_, _) =>
            {
                if (_lastTab == 0) _songSort = o; else _albumSort = o;
                Refresh();
            };
            SortMenu.Items.Add(item);
        }
    }

    void BuildGenreMenu()
    {
        GenreText.Text = _genre ?? "Все жанры";
        GenreMenu.Items.Clear();
        var all = new ToggleMenuFlyoutItem { Text = "Все жанры", IsChecked = _genre == null };
        all.Click += (_, _) => { _genre = null; Refresh(); };
        GenreMenu.Items.Add(all);
        var genres = App.Library.Tracks.Select(t => t.Genre).Where(g => !string.IsNullOrEmpty(g))
            .Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(g => g, StringComparer.CurrentCultureIgnoreCase);
        foreach (var g in genres)
        {
            var item = new ToggleMenuFlyoutItem { Text = g, IsChecked = string.Equals(g, _genre, StringComparison.CurrentCultureIgnoreCase) };
            item.Click += (_, _) => { _genre = g; Refresh(); };
            GenreMenu.Items.Add(item);
        }
    }

    // ---------- Playback ----------

    void ShuffleAll_Click(object sender, RoutedEventArgs e)
    {
        if (_songs.Count > 0) App.Player.Play(_songs, Random.Shared.Next(_songs.Count), shuffle: true);
    }

    void PlaySong(Track? t)
    {
        if (t != null) App.Player.Play(_songs, _songs.IndexOf(t), shuffle: false);
    }

    void Songs_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) =>
        PlaySong((e.OriginalSource as FrameworkElement)?.DataContext as Track);

    void Songs_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) PlaySong(SongsList.SelectedItem as Track);
    }

    void Stripe_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args) =>
        Stripes.Apply(args);

    void Album_Click(object sender, ItemClickEventArgs e) => App.Window.Navigate(typeof(DetailPage), e.ClickedItem);
    void Artist_Click(object sender, ItemClickEventArgs e) => App.Window.Navigate(typeof(DetailPage), e.ClickedItem);

    void PlayAlbum_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Album a) App.Player.Play(a.Tracks, 0, shuffle: false);
    }

    void Tile_PointerEntered(object sender, PointerRoutedEventArgs e) => TileHover.Set(sender, true);
    void Tile_PointerExited(object sender, PointerRoutedEventArgs e) => TileHover.Set(sender, false);

    void ChooseFolders_Click(object sender, RoutedEventArgs e) => App.Window.Navigate(typeof(SettingsPage));

    void CloseBanner_Click(object sender, RoutedEventArgs e)
    {
        _bannerClosed = true;
        FoldersBanner.Visibility = Visibility.Collapsed;
    }
}

static class TileHover
{
    public static void Set(object sender, bool on)
    {
        if (sender is not FrameworkElement fe) return;
        var v = on ? Visibility.Visible : Visibility.Collapsed;
        if (fe.FindName("PlayOverlay") is UIElement b) b.Visibility = v;
        if (fe.FindName("HoverShade") is UIElement s) s.Visibility = v;
    }
}
