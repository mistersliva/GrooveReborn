using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Reborn.Models;

namespace Reborn.Pages;

public sealed partial class NowPlayingPage : Page
{
    List<TrackRow> _rows = new();
    bool _wide;

    public NowPlayingPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) => ApplySize(e.NewSize.Width);
    }

    void ApplySize(double width)
    {
        _wide = width >= 720;
        if (_wide)
        {
            // Desktop: big art with text under it on the left, queue on the right.
            Grid.SetRow(QueuePanel, 0);
            Grid.SetRowSpan(QueuePanel, 2);
            Grid.SetColumn(QueuePanel, 1);
            ArtBox.Width = ArtBox.Height = 340;
            Grid.SetRow(TextPanel, 1);
            Grid.SetColumn(TextPanel, 0);
            TextPanel.Margin = new Thickness(0, 12, 0, 0);
            InfoPanel.Width = 340;
            TitleText.FontSize = 26;
            Layout.Padding = new Thickness(40, 16, 24, 0);
        }
        else
        {
            Grid.SetRow(QueuePanel, 1);
            Grid.SetRowSpan(QueuePanel, 1);
            Grid.SetColumn(QueuePanel, 0);
            Grid.SetColumnSpan(QueuePanel, 2);
            Grid.SetColumnSpan(InfoPanel, 2);
            ArtBox.Width = ArtBox.Height = 140;
            Grid.SetRow(TextPanel, 0);
            Grid.SetColumn(TextPanel, 1);
            TextPanel.Margin = new Thickness(0);
            InfoPanel.Width = double.NaN;
            TitleText.FontSize = 22;
            Layout.Padding = new Thickness(16, 8, 16, 0);
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        App.Player.TrackChanged += UpdateQueue;
        App.Player.QueueChanged += UpdateQueue;
        UpdateQueue();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        App.Player.TrackChanged -= UpdateQueue;
        App.Player.QueueChanged -= UpdateQueue;
    }

    void UpdateQueue()
    {
        _rows = App.Player.UpNext.ToList();
        QueueList.ItemsSource = _rows;
        UpdateTrack();
    }

    void UpdateTrack()
    {
        var t = App.Player.Current;
        TitleText.Text = t?.Title ?? "";
        ArtistText.Text = t?.Artist ?? "";
        AlbumText.Text = t?.Album ?? "";
        Art.Source = Fmt.Image(t?.CoverPath, 680);
        Backdrop.Source = Fmt.Image(t?.CoverPath, 200);
        EmptyText.Visibility = t == null ? Visibility.Visible : Visibility.Collapsed;
        InfoPanel.Visibility = t == null ? Visibility.Collapsed : Visibility.Visible;

        var pos = App.Player.Position;
        if (pos >= 0 && pos < _rows.Count) QueueList.ScrollIntoView(_rows[pos]);
    }

    void Queue_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TrackRow r) App.Player.PlayAt(r.Index);
    }
}
