using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Reborn;

/// <summary>Alternating row shading, as in the Windows 10 song lists.</summary>
public static class Stripes
{
    static readonly SolidColorBrush Clear = new(Microsoft.UI.Colors.Transparent);

    public static void Apply(ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is not ListViewItem item) return;
        Brush? brush = Clear;
        if (args.ItemIndex % 2 == 0)
        {
            var theme = (item.XamlRoot?.Content as FrameworkElement)?.ActualTheme ?? ElementTheme.Dark;
            brush = new ResourceDictionary().ThemeDictionaryBrush("RowStripe", theme) ?? Clear;
        }
        // The row template root is a Grid; shade it and stretch it over the item padding.
        if (item.ContentTemplateRoot is Grid root)
        {
            root.Background = brush;
            root.Margin = new Thickness(-item.Padding.Left, 0, -item.Padding.Right, 0);
            root.Padding = new Thickness(item.Padding.Left, 0, item.Padding.Right, 0);
        }
        else
        {
            item.Background = brush;
        }
    }
}
