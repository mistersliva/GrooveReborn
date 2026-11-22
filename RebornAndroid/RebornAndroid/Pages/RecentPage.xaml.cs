using Microsoft.UI.Xaml.Navigation;
using Reborn.Models;

namespace Reborn.Pages;

/// <summary>Albums of recently played tracks, newest first.</summary>
public sealed partial class RecentPage : Page
{
    public RecentPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) => { if (Tiles.Fit(e.NewSize.Width - 12)) Load(); };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => Load();

    void Load()
    {
        var byPath = new Dictionary<string, Track>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in App.Library.Tracks) byPath[t.Path] = t;
        var albumOf = new Dictionary<Track, Album>();
        foreach (var a in App.Library.Albums)
            foreach (var t in a.Tracks) albumOf[t] = a;

        var albums = new List<Album>();
        foreach (var path in App.Settings.Recent)
            if (byPath.TryGetValue(path, out var t) && albumOf.TryGetValue(t, out var a) && !albums.Contains(a))
                albums.Add(a);

        AlbumsGrid.ItemsSource = Tiles.Rows(albums);
        AlbumsGrid.Visibility = albums.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = albums.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    void ShuffleAll_Click(object sender, RoutedEventArgs e)
    {
        var all = App.Library.Tracks;
        if (all.Count > 0) App.Player.Play(all.ToList(), Random.Shared.Next(all.Count), shuffle: true);
    }

    void ShowMyMusic_Click(object sender, RoutedEventArgs e) => App.Shell.Navigate(typeof(MyMusicPage));
    void Tiles_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (Tiles.ItemAt(e.OriginalSource) is { } item) App.Shell.Navigate(typeof(DetailPage), item);
    }
}
