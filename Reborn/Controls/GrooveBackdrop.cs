using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Reborn.Controls;

/// <summary>Host-backdrop acrylic tinted like the Windows 10 dark/light acrylic (the stock one is Windows 11's).</summary>
public sealed class GrooveBackdrop : SystemBackdrop
{
    DesktopAcrylicController? _controller;
    bool _dark = true;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(target, xamlRoot);
        _controller = new DesktopAcrylicController();
        Apply();
        _controller.AddSystemBackdropTarget(target);
        _controller.SetSystemBackdropConfiguration(GetDefaultSystemBackdropConfiguration(target, xamlRoot));
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        if (_controller != null)
        {
            _controller.RemoveSystemBackdropTarget(target);
            _controller.Dispose();
            _controller = null;
        }
        base.OnTargetDisconnected(target);
    }

    public void SetDark(bool dark)
    {
        _dark = dark;
        Apply();
    }

    void Apply()
    {
        if (_controller == null) return;
        _controller.TintColor = _dark ? Color.FromArgb(255, 0x1F, 0x1F, 0x1F) : Color.FromArgb(255, 0xE6, 0xE6, 0xE6);
        _controller.TintOpacity = _dark ? 0.72f : 0.6f;
        _controller.LuminosityOpacity = 0.88f;
        _controller.FallbackColor = _dark ? Color.FromArgb(255, 0x2B, 0x2B, 0x2B) : Color.FromArgb(255, 0xE6, 0xE6, 0xE6);
    }
}
