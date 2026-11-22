using Microsoft.UI.Xaml.Navigation;
using Reborn.Droid;
using Reborn.Models;

namespace Reborn.Pages;

public sealed partial class SettingsPage : Page
{
    bool _loading;

    public SettingsPage()
    {
        InitializeComponent();
        App.Library.Changed += () => DispatcherQueue.TryEnqueue(UpdateLibrary);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _loading = true;
        (App.Settings.Theme switch { "Light" => ThemeLight, "Dark" => ThemeDark, _ => ThemeSystem }).IsChecked = true;
        HideShortBox.IsChecked = App.Settings.HideShortFiles;
        _loading = false;

        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        AboutText.Text =
            $"Reborn Music для Android {version}\n" +
            "Локальный плеер в стиле Groove Music из Windows 10. Без аккаунтов и магазина.\n" +
            "Иконки: Uno Fluent UI Assets (Apache 2.0). Шрифт: Open Sans (SIL OFL). Сделано на Uno Platform.";
        UpdateLibrary();
    }

    void UpdateLibrary()
    {
        var lib = App.Library;
        AccessButton.Visibility = MainActivity.HasAudioPermission ? Visibility.Collapsed : Visibility.Visible;
        LibraryText.Text = !MainActivity.HasAudioPermission
            ? "Нет доступа к музыке. Разрешите доступ, чтобы приложение нашло ваши композиции."
            : lib.IsScanning
                ? "Идёт поиск музыки…"
                : $"{Fmt.Count(lib.Tracks.Count, "композиция", "композиции", "композиций")}, " +
                  $"{Fmt.Count(lib.Albums.Count, "альбом", "альбома", "альбомов")}, " +
                  $"{Fmt.Count(lib.Artists.Count, "исполнитель", "исполнителя", "исполнителей")}";
    }

    void Access_Click(object sender, RoutedEventArgs e) => _ = App.Shell.RequestAccessAndScanAsync(openSettingsIfBlocked: true);
    void Rescan_Click(object sender, RoutedEventArgs e) => _ = App.Shell.RequestAccessAndScanAsync(openSettingsIfBlocked: true);

    void HideShort_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.HideShortFiles = HideShortBox.IsChecked == true;
        App.Settings.Save();
        _ = App.Shell.RequestAccessAndScanAsync();
    }

    void Theme_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || (sender as FrameworkElement)?.Tag is not string theme) return;
        App.Settings.Theme = theme;
        App.Settings.Save();
        App.Shell.ApplyTheme(theme);
    }
}
