using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Reborn.Models;

namespace Reborn.Services;

public sealed class LibraryService
{
    public static readonly string DataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RebornMusic");
    static readonly string CacheFile = Path.Combine(DataDir, "library.json");
    static readonly string CoverDir = Path.Combine(DataDir, "covers");

    static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".flac", ".m4a", ".aac", ".wma", ".wav", ".ogg", ".opus", ".aiff", ".aif" };

    static readonly string[] FolderImages =
        { "cover.jpg", "folder.jpg", "front.jpg", "cover.png", "folder.png", "front.png", "albumart.jpg" };

    public IReadOnlyList<Track> Tracks { get; private set; } = Array.Empty<Track>();
    public IReadOnlyList<Album> Albums { get; private set; } = Array.Empty<Album>();
    public IReadOnlyList<Artist> Artists { get; private set; } = Array.Empty<Artist>();
    public bool IsScanning { get; private set; }

    /// <summary>Raised on a background thread.</summary>
    public event Action? Changed;

    readonly SemaphoreSlim _scanLock = new(1, 1);

    public void LoadCache()
    {
        try
        {
            if (!File.Exists(CacheFile)) return;
            var list = JsonSerializer.Deserialize<List<Track>>(File.ReadAllText(CacheFile));
            if (list != null) Build(list);
        }
        catch { }
    }

    public Task ScanAsync(IEnumerable<string> folders)
    {
        var roots = folders.ToList();
        return Task.Run(async () =>
        {
            await _scanLock.WaitAsync();
            try
            {
                IsScanning = true;
                Changed?.Invoke();
                Directory.CreateDirectory(CoverDir);

                bool firstRun = Tracks.Count == 0;
                var known = new Dictionary<string, Track>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in Tracks) known[t.Path] = t;

                var result = new List<Track>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int sinceUpdate = 0;

                foreach (var root in roots)
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (var file in Enumerate(root))
                    {
                        if (!Extensions.Contains(Path.GetExtension(file)) || !seen.Add(file)) continue;

                        long modified;
                        try { modified = File.GetLastWriteTimeUtc(file).Ticks; }
                        catch { continue; }

                        if (known.TryGetValue(file, out var cached) && cached.Modified == modified &&
                            (cached.CoverPath == null || File.Exists(cached.CoverPath)))
                        {
                            result.Add(cached);
                            continue;
                        }

                        result.Add(ReadTrack(file, modified));

                        // On a first run show music as it is found; otherwise update once at the end.
                        if (firstRun && ++sinceUpdate >= 250)
                        {
                            sinceUpdate = 0;
                            Build(new List<Track>(result));
                            Changed?.Invoke();
                        }
                    }
                }

                Build(result);
                Save(result);
            }
            catch { }
            finally
            {
                IsScanning = false;
                _scanLock.Release();
                Changed?.Invoke();
            }
        });
    }

    static IEnumerable<string> Enumerate(string root)
    {
        try
        {
            return Directory.EnumerateFiles(root, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System,
            });
        }
        catch { return Array.Empty<string>(); }
    }

    static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    static string AlbumKey(Track t) => (t.AlbumArtist + "\u0001" + t.Album).ToLowerInvariant();

    static string Hash(string s) => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(s)))[..20];

    static Track ReadTrack(string path, long modified)
    {
        var t = new Track { Path = path, Modified = modified, Title = Path.GetFileNameWithoutExtension(path) };
        TagLib.File? f = null;
        try
        {
            f = TagLib.File.Create(path);
            var tag = f.Tag;
            t.Title = Clean(tag.Title) ?? t.Title;
            t.Artist = Clean(tag.JoinedPerformers) ?? Clean(tag.JoinedAlbumArtists) ?? Fmt.UnknownArtist;
            t.AlbumArtist = Clean(tag.JoinedAlbumArtists) ?? Clean(tag.FirstPerformer) ?? Fmt.UnknownArtist;
            t.Album = Clean(tag.Album) ?? Fmt.UnknownAlbum;
            t.Genre = Clean(tag.JoinedGenres) ?? "";
            t.TrackNumber = tag.Track;
            t.Disc = tag.Disc;
            t.Year = tag.Year;
            t.DurationSec = f.Properties?.Duration.TotalSeconds ?? 0;
        }
        catch { }

        try
        {
            var coverFile = Path.Combine(CoverDir, Hash(AlbumKey(t)) + ".img");
            if (File.Exists(coverFile))
            {
                t.CoverPath = coverFile;
            }
            else if (f?.Tag.Pictures is { Length: > 0 } pics)
            {
                var pic = pics.FirstOrDefault(p => p.Type == TagLib.PictureType.FrontCover) ?? pics[0];
                File.WriteAllBytes(coverFile, pic.Data.Data);
                t.CoverPath = coverFile;
            }
            else
            {
                var dir = Path.GetDirectoryName(path)!;
                t.CoverPath = FolderImages.Select(n => Path.Combine(dir, n)).FirstOrDefault(File.Exists);
            }
        }
        catch { }
        finally { f?.Dispose(); }

        return t;
    }

    void Build(List<Track> list)
    {
        var cmp = StringComparer.CurrentCultureIgnoreCase;

        var albums = list.GroupBy(AlbumKey).Select(g =>
        {
            var tracks = g.OrderBy(t => t.Disc).ThenBy(t => t.TrackNumber).ThenBy(t => t.Title, cmp).ToList();
            var cover = tracks.Select(t => t.CoverPath).FirstOrDefault(c => c != null);
            foreach (var t in tracks) t.CoverPath ??= cover;
            return new Album
            {
                Title = tracks[0].Album,
                Artist = tracks[0].AlbumArtist,
                Year = tracks.Max(t => t.Year),
                CoverPath = cover,
                Tracks = tracks,
            };
        }).OrderBy(a => a.Title, cmp).ToList();

        var artists = albums.GroupBy(a => a.Artist, StringComparer.CurrentCultureIgnoreCase).Select(g =>
        {
            var al = g.OrderByDescending(a => a.Year).ThenBy(a => a.Title, cmp).ToList();
            return new Artist
            {
                Name = al[0].Artist,
                Albums = al,
                Tracks = al.SelectMany(a => a.Tracks).ToList(),
                CoverPath = al.Select(a => a.CoverPath).FirstOrDefault(c => c != null),
            };
        }).OrderBy(a => a.Name, cmp).ToList();

        Tracks = list.OrderBy(t => t.Title, cmp).ToList();
        Albums = albums;
        Artists = artists;
    }

    static void Save(List<Track> list)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(CacheFile, JsonSerializer.Serialize(list));
        }
        catch { }
    }
}
