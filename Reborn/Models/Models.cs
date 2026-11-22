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

    public static ImageSource? Image(string? path, int decodeWidth)
    {
        if (string.IsNullOrEmpty(path)) return null;
        try { return new BitmapImage(new Uri(path)) { DecodePixelWidth = decodeWidth }; }
        catch { return null; }
    }
}

public sealed class Track
{
    public string Path { get; set; } = "";
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
    public long Modified { get; set; }

    [JsonIgnore] public string DurationText => Fmt.Time(DurationSec);
    [JsonIgnore] public string YearText => Year > 0 ? Year.ToString() : "";
    [JsonIgnore] public string NumberText => TrackNumber > 0 ? TrackNumber.ToString() : "";
}

public sealed class Album
{
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public uint Year { get; init; }
    public string? CoverPath { get; init; }
    public List<Track> Tracks { get; init; } = new();

    ImageSource? _cover;
    public ImageSource? Cover => _cover ??= Fmt.Image(CoverPath, 360);
    public string Subtitle => Year > 0 ? $"{Artist} • {Year}" : Artist;
}

public sealed class Artist
{
    public string Name { get; init; } = "";
    public List<Album> Albums { get; init; } = new();
    public List<Track> Tracks { get; init; } = new();
    public string? CoverPath { get; init; }

    ImageSource? _cover;
    public ImageSource? Cover => _cover ??= Fmt.Image(CoverPath, 320);
    public string Initial => string.IsNullOrEmpty(Name) ? "?" : char.ToUpper(Name[0]).ToString();
    public string AlbumCountText => Fmt.Count(Albums.Count, "альбом", "альбома", "альбомов");
}

public sealed class QueueEntry
{
    public int Index { get; init; }
    public Track Track { get; init; } = null!;
    public string Title => Track.Title;
    public string Artist => Track.Artist;
    public string DurationText => Track.DurationText;
}
