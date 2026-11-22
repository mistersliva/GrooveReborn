using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Reborn.Controls;

/// <summary>
/// Groove derives several colours from the accent: the pane selection bar and links use AccentLight1,
/// the player bar is that shade at ~30% over black. Android has no Windows accent, so this uses the
/// default Windows 10 blue palette (#0078D7) that Groove shipped with.
/// </summary>
public static class Accent
{
    static readonly Color AccentColor = Color.FromArgb(255, 0x00, 0x78, 0xD7);
    static readonly Color Light1 = Color.FromArgb(255, 0x42, 0x9C, 0xE3);
    static readonly Color Dark1 = Color.FromArgb(255, 0x00, 0x5A, 0x9E);

    public static bool IsDark { get; private set; } = true;

    public static void Apply(bool dark)
    {
        IsDark = dark;
        Set("GrooveAccent", AccentColor);
        Set("GrooveAccentText", dark ? Light1 : Dark1);
        Set("GroovePlayerBar", dark ? Scale(Light1, 0.30) : Mix(Color.FromArgb(255, 255, 255, 255), Light1, 0.40));
        Set("GroovePlayerTrack", dark ? Color.FromArgb(0x40, 255, 255, 255) : Color.FromArgb(0x40, 0, 0, 0));
        Set("GroovePlayerForeground", dark ? Color.FromArgb(255, 255, 255, 255) : Color.FromArgb(255, 0, 0, 0));
        Set("GroovePlayerSecondary", dark ? Color.FromArgb(0xB3, 255, 255, 255) : Color.FromArgb(0xB3, 0, 0, 0));
        Set("RowStripeBrush", dark ? Color.FromArgb(255, 0x19, 0x19, 0x19) : Color.FromArgb(255, 0xF2, 0xF2, 0xF2));
        Set("PaneBrush", dark ? Color.FromArgb(255, 0x1F, 0x1F, 0x1F) : Color.FromArgb(255, 0xE6, 0xE6, 0xE6));
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
