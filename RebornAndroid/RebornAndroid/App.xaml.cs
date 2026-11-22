using Microsoft.UI.Dispatching;
using Reborn.Services;

namespace Reborn;

public partial class App : Application
{
    public static SettingsService Settings { get; } = SettingsService.Load();
    public static LibraryService Library { get; } = new();
    public static PlayerService Player => PlayerOrNull!;
    public static PlayerService? PlayerOrNull { get; private set; }
    public static Shell Shell { get; private set; } = null!;
    static DispatcherQueue? _ui;

    Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            Log("unhandled", e.Exception);
            e.Handled = true;
        };
    }

    public static void Log(string where, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(LibraryService.DataDir);
            File.AppendAllText(System.IO.Path.Combine(LibraryService.DataDir, "errors.log"),
                $"[{DateTime.Now:s}] {where}: {ex}\n\n");
            Android.Util.Log.Error("RebornMusic", $"{where}: {ex}");
        }
        catch { }
    }

    public static void RunOnUi(Action a)
    {
        if (_ui == null || _ui.HasThreadAccess) a();
        else _ui.TryEnqueue(() => a());
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (_window != null)
        {
            _window.Activate();
            return;
        }

        _ui = DispatcherQueue.GetForCurrentThread();
        PlayerOrNull = new PlayerService(_ui);
        Player.Volume = Settings.Volume;
        Player.TrackChanged += () => { if (Player.Current is { } t) Settings.AddRecent(t.Path); };
        Library.LoadCache();

        _window = new Window();
        Shell = new Shell();
        _window.Content = Shell;
        _window.Activate();
    }
}
