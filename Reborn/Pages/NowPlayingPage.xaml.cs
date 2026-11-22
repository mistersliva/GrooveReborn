using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Reborn.Models;

namespace Reborn.Pages;

public sealed partial class NowPlayingPage : Page
{
    public NowPlayingPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        App.Player.TrackChanged += UpdateTrack;
        App.Player.QueueChanged += UpdateQueue;
        UpdateQueue();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        App.Player.TrackChanged -= UpdateTrack;
        App.Player.QueueChanged -= UpdateQueue;
    }

    void UpdateQueue()
    {
        QueueList.ItemsSource = App.Player.UpNext;
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

        var pos = App.Player.Position;
        if (pos >= 0 && QueueList.Items.Count > pos)
        {
            QueueList.SelectedIndex = pos;
            QueueList.ScrollIntoView(QueueList.Items[pos]);
        }
    }

    void Queue_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is QueueEntry q) App.Player.PlayAt(q.Index);
    }

    void Stripe_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args) =>
        Stripes.Apply(args);
}
