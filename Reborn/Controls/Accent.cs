using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Reborn.Controls;

/// <summary>
/// Groove derives several colours from the Windows accent: the pane selection bar and links use AccentLight1,
/// the player bar is that shade at ~30% over black. These brushes live in App.xaml and are recoloured here.
/// </summary>
public static class Accent
{
    static readonly UISettings Ui = new();
    static bool _dark = true;

    public static void Init()
    {
        var dq = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Ui.ColorValuesChanged += (_, _) => dq.TryEnqueue(() => Apply(_dark));
    }

    public static void Apply(bool dark)
    {
        _dark = dark;
        var accent = Ui.GetColorValue(UIColorType.Accent);
        var light1 = Ui.GetColorValue(UIColorType.AccentLight1);
        var dark1 = Ui.GetColorValue(UIColorType.AccentDark1);

        Set("GrooveAccent", accent);
        Set("GrooveAccentText", dark ? light1 : dark1);
        Set("GroovePlayerBar", dark ? Scale(light1, 0.30) : Mix(Color.FromArgb(255, 255, 255, 255), light1, 0.40));
        Set("GroovePlayerTrack", dark ? Color.FromArgb(0x40, 255, 255, 255) : Color.FromArgb(0x40, 0, 0, 0));
        Set("GroovePlayerForeground", dark ? Color.FromArgb(255, 255, 255, 255) : Color.FromArgb(255, 0, 0, 0));
        Set("GroovePlayerSecondary", dark ? Color.FromArgb(0xB3, 255, 255, 255) : Color.FromArgb(0xB3, 0, 0, 0));
    }

    public static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    static void Set(string key, Color c)
    {
        if (Application.Current.Resources.TryGetValue(key, out var v) && v is SolidColorBrush b) b.Color = c;
    }

    static Color Scale(Color c, double f) =>
        Color.FromArgb(255, (byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f));

    static Color Mix(Color a, Color b, double t) =>
        Color.FromArgb(255, (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
}
