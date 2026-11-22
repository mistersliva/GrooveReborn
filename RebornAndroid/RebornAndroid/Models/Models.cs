using System.Text.Json.Serialization;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Reborn.Models;

public static class Fmt
{
    public const string UnknownArtist = "Неизвестный исполнитель";
    public const string UnknownAlbum = "Неизвестный альбом";

    public static string Time(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, double.IsFinite(seconds) ? seconds : 0));
        return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
    }

    public static string Count(int n, string one, string few, string many)
    {
        int m10 = n % 10, m100 = n % 100;
        var word = m10 == 1 && m100 != 11 ? one
                 : m10 is >= 2 and <= 4 && (m100 < 12 || m100 > 14) ? few
                 : many;
        return $"{n} {word}";
    }

    /// <summary>Image from a local file (cover cache). Loaded from a stream, which works on every Uno renderer.</summary>
    public static ImageSource? Image(string? path, int decodeWidth)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        var bmp = new BitmapImage { DecodePixelWidth = decodeWidth };
        _ = LoadAsync(bmp, path);
        return bmp;
    }

    static async Task LoadAsync(BitmapImage bmp, string path)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path);
            var ms = new MemoryStream(bytes);
            await bmp.SetSourceAsync(ms.AsRandomAccessStream());
        }
        catch (Exception ex)
        {
            Android.Util.Log.Info("RebornMusic", $"image fail {path}: {ex.GetType().Name} {ex.Message}");
        }
    }
}

public sealed class Track
{
    /// <summary>content:// URI of the MediaStore item.</summary>
    public string Path { get; set; } = "";
    public long MediaId { get; set; }
    public long AlbumId { get; set; }
    public string Title { get; set; } = "";
    public string Artist { get; set; } = Fmt.UnknownArtist;
    public string AlbumArtist { get; set; } = Fmt.UnknownArtist;
    public string Album { get; set; } = Fmt.UnknownAlbum;
    public string Genre { get; set; } = "";
    public uint TrackNumber { get; set; }
    public uint Disc { get; set; }
    public uint Year { get; set; }
    public double DurationSec { get; set; }
    public string? CoverPath { get; set; }
    /// <summary>Date added to the device (Unix seconds).</summary>
    public long Modified { get; set; }

    [JsonIgnore] public string DurationText => Fmt.Time(DurationSec);
    [JsonIgnore] public string YearText => Year > 0 ? Year.ToString() : "";
    [JsonIgnore] public string NumberText => TrackNumber > 0 ? TrackNumber.ToString() : "";
}

/// <summary>Album/artist tile size: Groove's 180px on wide screens, sized to fill two or more columns on a phone.</summary>
public static class Tiles
{
    public const double Gap = 12; // tile margin 4 + 8
    public static double Size { get; private set; } = 160;
    public static int Columns { get; private set; } = 2;

    public static List<TileRow> Rows<T>(IEnumerable<T> items) where T : class =>
        items.Chunk(Columns).Select(c => new TileRow(c.Cast<object>().ToList())).ToList();

    /// <summary>Recomputes the size for a grid of this width; true when it changed.</summary>
    public static bool Fit(double gridWidth)
    {
        if (gridWidth <= 0) return false;
        int cols = Math.Max(2, (int)(gridWidth / (180 + Gap)));
        // A few pixels of slack: an exact fit wraps the last column on rounding.
        double size = Math.Floor(Math.Min(200, (gridWidth - 6) / cols - Gap));
        if (Math.Abs(size - Size) < 1 && cols == Columns) return false;
        Size = size;
        Columns = cols;
        return true;
    }

    /// <summary>The Album or Artist under a tapped element.</summary>
    public static object? ItemAt(object? originalSource)
    {
        for (var fe = originalSource as FrameworkElement; fe != null; fe = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(fe) as FrameworkElement)
            if (fe.DataContext is Album or Artist) return fe.DataContext;
        return null;
    }
}

/// <summary>One row of album/artist tiles.</summary>
public sealed class TileRow
{
    public TileRow(List<object> items) => Items = items;
    public List<object> Items { get; }
}

public sealed class Album
{
    public double TileSize => Tiles.Size;
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public uint Year { get; init; }
    public string? CoverPath { get; init; }
    public List<Track> Tracks { get; init; } = new();

    ImageSource? _cover;
    public ImageSource? Cover => _cover ??= Fmt.Image(CoverPath, 320);
    public string Subtitle => Year > 0 ? $"{Artist} • {Year}" : Artist;
}

public sealed class Artist
{
    public string Name { get; init; } = "";
    public double TileSize => Tiles.Size;
    public List<Album> Albums { get; init; } = new();
    public List<Track> Tracks { get; init; } = new();
    public string? CoverPath { get; init; }

    ImageSource? _cover;
    public ImageSource? Cover => _cover ??= Fmt.Image(CoverPath, 320);
    public string Initial => string.IsNullOrEmpty(Name) ? "?" : char.ToUpper(Name[0]).ToString();
    public string AlbumCountText => Fmt.Count(Albums.Count, "альбом", "альбома", "альбомов");
}

/// <summary>A list row: the item plus its position, for the alternating Windows 10 row shading.</summary>
public sealed class TrackRow
{
    public TrackRow(Track track, int index, bool isCurrent = false)
    {
        Track = track;
        Index = index;
        IsCurrent = isCurrent;
    }

    public Track Track { get; }
    public int Index { get; }
    public bool IsCurrent { get; }
    public Visibility CurrentVisibility => IsCurrent ? Visibility.Visible : Visibility.Collapsed;
    public string Title => Track.Title;
    public string Artist => Track.Artist;
    public string Album => Track.Album;
    public string ArtistAlbum => Track.Album == Fmt.UnknownAlbum ? Track.Artist : $"{Track.Artist} • {Track.Album}";
    public string DurationText => Track.DurationText;
    public string NumberText => Track.NumberText;
    public Brush Stripe => Index % 2 == 0
        ? (Brush)Application.Current.Resources["RowStripeBrush"]
        : (Brush)Application.Current.Resources["TransparentBrush"];

    public static List<TrackRow> From(IEnumerable<Track> tracks) => tracks.Select((t, i) => new TrackRow(t, i)).ToList();
}
