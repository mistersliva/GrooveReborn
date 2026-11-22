using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Reborn.Services;

namespace Reborn;

public partial class App : Application
{
    public static SettingsService Settings { get; } = SettingsService.Load();
    public static LibraryService Library { get; } = new();
    public static PlayerService Player { get; private set; } = null!;
    public static MainWindow Window { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            try
            {
                Directory.CreateDirectory(LibraryService.DataDir);
                File.AppendAllText(Path.Combine(LibraryService.DataDir, "errors.log"),
                    $"[{DateTime.Now:s}] {e.Exception}\n\n");
            }
            catch { }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Player = new PlayerService(DispatcherQueue.GetForCurrentThread());
        Player.Volume = Settings.Volume;
        Player.TrackChanged += () => { if (Player.Current is { } t) Settings.AddRecent(t.Path); };
        Reborn.Controls.Accent.Init();
        Reborn.Controls.IconFont.Apply();
        Library.LoadCache();

        Window = new MainWindow();
        Window.Activate();

        _ = Library.ScanAsync(Settings.Folders);
    }
}
