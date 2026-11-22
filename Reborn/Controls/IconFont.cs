using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Management.Deployment;

namespace Reborn.Controls;

/// <summary>
/// Groove draws its icons with "Segoe MVR MDL2 Assets" (SegMVR2.ttf), a Microsoft font shipped inside the Groove package.
/// Reborn does not ship that font. If Groove is installed on this PC, the user's own copy is copied next to the exe
/// (WinUI only loads font files from the app folder via ms-appx) and used; otherwise the system "Segoe MDL2 Assets" is used.
/// </summary>
public static class IconFont
{
    const string GrooveFamily = "Microsoft.ZuneMusic_8wekyb3d8bbwe";
    const string FontName = "Segoe MVR MDL2 Assets";
    const string LocalName = "SegMVR2.ttf";

    public static bool UsingGrooveFont { get; private set; }

    public static void Apply()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "Fonts", LocalName);
        try
        {
            if (!File.Exists(local) && FindInGroove() is { } source)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(local)!);
                File.Copy(source, local);
            }
        }
        catch { }

        if (!File.Exists(local)) return;
        UsingGrooveFont = true;
        // Glyphs the Groove font lacks (e.g. caption buttons) fall back to the system icon font.
        Family = new FontFamily($"ms-appx:///Fonts/{LocalName}#{FontName}, Segoe MDL2 Assets");
        Application.Current.Resources["SymbolThemeFontFamily"] = Family;
    }

    public static FontFamily? Family { get; private set; }

    /// <summary>Give every FontIcon under <paramref name="root"/> the Groove font (unless it pins its own family).</summary>
    public static void ApplyTo(DependencyObject root)
    {
        if (Family == null) return;
        if (root is Microsoft.UI.Xaml.Controls.FontIcon icon &&
            icon.ReadLocalValue(Microsoft.UI.Xaml.Controls.FontIcon.FontFamilyProperty) == DependencyProperty.UnsetValue)
            icon.FontFamily = Family;
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++) ApplyTo(VisualTreeHelper.GetChild(root, i));
    }

    static string? FindInGroove()
    {
        try
        {
            foreach (var p in new PackageManager().FindPackagesForUser("", GrooveFamily))
            {
                var f = Path.Combine(p.InstalledLocation.Path, "Resources", "Fonts", LocalName);
                if (File.Exists(f)) return f;
            }
        }
        catch { }
        return null;
    }
}
