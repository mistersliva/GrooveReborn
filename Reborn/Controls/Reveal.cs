using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Reborn.Controls;

/// <summary>Windows 10 "Reveal" hover: a soft light spot that follows the pointer.</summary>
public static class Reveal
{
    public static void Attach(Control c)
    {
        c.PointerMoved += OnMoved;
        c.PointerExited += OnExited;
        c.PointerCanceled += OnExited;
    }

    static void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        var c = (Control)sender;
        var p = e.GetCurrentPoint(c).Position;
        if (c.Background is not RadialGradientBrush b)
        {
            var light = c.ActualTheme == ElementTheme.Light ? Colors.Black : Colors.White;
            b = new RadialGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                RadiusX = 110,
                RadiusY = 110,
            };
            b.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0x26, light.R, light.G, light.B), Offset = 0 });
            b.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0x00, light.R, light.G, light.B), Offset = 1 });
            c.Background = b;
        }
        b.Center = p;
        b.GradientOrigin = p;
    }

    static void OnExited(object sender, PointerRoutedEventArgs e) =>
        ((Control)sender).Background = new SolidColorBrush(Colors.Transparent);
}
