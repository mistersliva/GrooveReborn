using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Reborn.Controls;

/// <summary>
/// Groove's flat slider: 2px track, accent fill, round thumb (white ring, bar-coloured centre).
/// The seek bar shows its thumb only while hovered or dragged.
/// </summary>
public sealed class GrooveSlider : Grid
{
    const double ThumbSize = 16;

    readonly Rectangle _track = new() { Height = 2, VerticalAlignment = VerticalAlignment.Center };
    readonly Rectangle _fill = new() { Height = 2, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
    readonly Ellipse _thumb = new()
    {
        Width = ThumbSize, Height = ThumbSize, StrokeThickness = 2.5,
        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
    };

    double _value, _max = 1;
    bool _hover;

    public bool ThumbOnHoverOnly { get; set; }
    public bool IsDragging { get; private set; }

    /// <summary>Raised while the user clicks or drags, with the new value.</summary>
    public event Action<double>? UserChanged;
    public event Action? DragCompleted;

    public double Maximum
    {
        get => _max;
        set { _max = Math.Max(value, 0.0001); Layout(); }
    }

    public double Value
    {
        get => _value;
        set { _value = Math.Clamp(value, 0, _max); Layout(); }
    }

    public GrooveSlider()
    {
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        _track.Fill = Accent.Brush("GroovePlayerTrack");
        _fill.Fill = Accent.Brush("GrooveAccent");
        _thumb.Stroke = Accent.Brush("GroovePlayerForeground");
        _thumb.Fill = Accent.Brush("GroovePlayerBar");
        Children.Add(_track);
        Children.Add(_fill);
        Children.Add(_thumb);

        SizeChanged += (_, _) => Layout();
        PointerEntered += (_, _) => { _hover = true; Layout(); };
        PointerExited += (_, _) => { _hover = false; Layout(); };
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        PointerCaptureLost += (_, _) => EndDrag();
    }

    void Layout()
    {
        var w = ActualWidth;
        var x = w * (_value / _max);
        _fill.Width = Math.Max(0, x);
        _thumb.Margin = new Thickness(Math.Clamp(x - ThumbSize / 2, -ThumbSize / 2, Math.Max(0, w - ThumbSize / 2)), 0, 0, 0);
        _thumb.Visibility = IsHitTestVisible && (!ThumbOnHoverOnly || _hover || IsDragging) ? Visibility.Visible : Visibility.Collapsed;
    }

    void SetFromPointer(PointerRoutedEventArgs e)
    {
        if (ActualWidth <= 0) return;
        Value = e.GetCurrentPoint(this).Position.X / ActualWidth * _max;
        UserChanged?.Invoke(_value);
    }

    void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!IsHitTestVisible) return;
        IsDragging = true;
        CapturePointer(e.Pointer);
        SetFromPointer(e);
        e.Handled = true;
    }

    void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (IsDragging) SetFromPointer(e);
    }

    void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        ReleasePointerCapture(e.Pointer);
        EndDrag();
    }

    void EndDrag()
    {
        if (!IsDragging) return;
        IsDragging = false;
        Layout();
        DragCompleted?.Invoke();
    }
}
