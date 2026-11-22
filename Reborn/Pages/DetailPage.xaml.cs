using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Reborn.Models;

namespace Reborn.Pages;

/// <summary>Album or artist page: header art, play buttons, track list.</summary>
public sealed partial class DetailPage : Page
{
    List<Track> _tracks = new();

    public DetailPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        string? cover = null;
        switch (e.Parameter)
        {
            case Album a:
                _tracks = a.Tracks;
                cover = a.CoverPath;
                TitleText.Text = a.Title;
                SubText.Text = a.Artist;
                InfoText.Text = Info(a.Year, a.Tracks);
                SquareArt.Visibility = Visibility.Visible;
                RoundArt.Visibility = Visibility.Collapsed;
                ArtImage.Source = Fmt.Image(cover, 440);
                break;

            case Artist ar:
                _tracks = ar.Tracks;
                cover = ar.CoverPath;
                TitleText.Text = ar.Name;
                SubText.Text = ar.AlbumCountText;
                InfoText.Text = Info(0, ar.Tracks);
                SquareArt.Visibility = Visibility.Collapsed;
                RoundArt.Visibility = Visibility.Visible;
                InitialText.Text = ar.Initial;
                RoundBrush.ImageSource = Fmt.Image(cover, 440);
                break;
        }

        Backdrop.Source = Fmt.Image(cover, 200);
        TrackList.ItemsSource = _tracks;
    }

    static string Info(uint year, List<Track> tracks)
    {
        var parts = new List<string>();
        if (year > 0) parts.Add(year.ToString());
        var genre = tracks.Select(t => t.Genre).FirstOrDefault(g => !string.IsNullOrEmpty(g));
        if (genre != null) parts.Add(genre);
        parts.Add(Fmt.Count(tracks.Count, "композиция", "композиции", "композиций"));
        parts.Add(Fmt.Time(tracks.Sum(t => t.DurationSec)));
        return string.Join(" • ", parts);
    }

    void PlayAll_Click(object sender, RoutedEventArgs e) => App.Player.Play(_tracks, 0, shuffle: false);

    void Shuffle_Click(object sender, RoutedEventArgs e)
    {
        if (_tracks.Count > 0) App.Player.Play(_tracks, Random.Shared.Next(_tracks.Count), shuffle: true);
    }

    void Play(Track? t)
    {
        if (t != null) App.Player.Play(_tracks, _tracks.IndexOf(t), shuffle: false);
    }

    void Track_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) =>
        Play((e.OriginalSource as FrameworkElement)?.DataContext as Track);

    void Track_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) Play(TrackList.SelectedItem as Track);
    }

    void Stripe_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args) =>
        Stripes.Apply(args);
}
