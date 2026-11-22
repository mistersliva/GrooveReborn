using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Reborn.Services;
using Windows.Storage.Pickers;

namespace Reborn.Pages;

public sealed partial class SettingsPage : Page
{
    static readonly string[] Themes = { "Light", "Dark", "System" };
    bool _loading = true;

    public SettingsPage()
    {
        InitializeComponent();
        RefreshFolders();
        ThemeRadios.SelectedIndex = Math.Max(0, Array.IndexOf(Themes, App.Settings.Theme) is var i && i >= 0 ? i : 2);
        var ver = typeof(App).Assembly.GetName().Version;
        AboutText.Text = $"Reborn Music {ver?.ToString(3)}\nДанные библиотеки: {LibraryService.DataDir}";
        _loading = false;
    }

    void RefreshFolders() => FolderList.ItemsSource = App.Settings.Folders.ToList();

    void Rescan() => _ = App.Library.ScanAsync(App.Settings.Folders);

    async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.MusicLibrary };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.Window));
        var folder = await picker.PickSingleFolderAsync();
        if (folder == null) return;
        if (App.Settings.Folders.Any(f => string.Equals(f, folder.Path, StringComparison.OrdinalIgnoreCase))) return;

        App.Settings.Folders.Add(folder.Path);
        App.Settings.Save();
        RefreshFolders();
        Rescan();
    }

    void RemoveFolder_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string path) return;
        App.Settings.Folders.RemoveAll(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase));
        App.Settings.Save();
        RefreshFolders();
        Rescan();
    }

    void Rescan_Click(object sender, RoutedEventArgs e) => Rescan();

    void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeRadios.SelectedIndex < 0) return;
        App.Settings.Theme = Themes[ThemeRadios.SelectedIndex];
        App.Settings.Save();
        App.Window.ApplyTheme(App.Settings.Theme);
    }
}
