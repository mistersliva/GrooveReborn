using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Reborn.Models;

namespace Reborn.Pages;

/// <summary>Albums of recently played tracks, newest first.</summary>
public sealed partial class RecentPage : Page
{
    public RecentPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var byPath = App.Library.Tracks.ToDictionary(t => t.Path, StringComparer.OrdinalIgnoreCase);
        var albumOf = new Dictionary<Track, Album>();
        foreach (var a in App.Library.Albums)
            foreach (var t in a.Tracks) albumOf[t] = a;

        var albums = new List<Album>();
        foreach (var path in App.Settings.Recent)
            if (byPath.TryGetValue(path, out var t) && albumOf.TryGetValue(t, out var a) && !albums.Contains(a))
                albums.Add(a);

        AlbumsGrid.ItemsSource = albums;
        AlbumsGrid.Visibility = albums.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = albums.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    void ShuffleAll_Click(object sender, RoutedEventArgs e)
    {
        var all = App.Library.Tracks;
        if (all.Count > 0) App.Player.Play(all.ToList(), Random.Shared.Next(all.Count), shuffle: true);
    }

    void ShowMyMusic_Click(object sender, RoutedEventArgs e) => App.Window.Navigate(typeof(MyMusicPage));
    void Album_Click(object sender, ItemClickEventArgs e) => App.Window.Navigate(typeof(DetailPage), e.ClickedItem);

    void PlayAlbum_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Album a) App.Player.Play(a.Tracks, 0, shuffle: false);
    }

    void Tile_PointerEntered(object sender, PointerRoutedEventArgs e) => TileHover.Set(sender, true);
    void Tile_PointerExited(object sender, PointerRoutedEventArgs e) => TileHover.Set(sender, false);
}
