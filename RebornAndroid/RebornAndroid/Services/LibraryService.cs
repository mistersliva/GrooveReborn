using System.Text.Json;
using Android.Content;
using Android.Graphics;
using Android.Provider;
using Reborn.Models;
using AUri = Android.Net.Uri;
using Path = System.IO.Path;

namespace Reborn.Services;

/// <summary>
/// Music library from the Android MediaStore (no folder walking, no tag parsing: the system indexer
/// already has the tags). Covers come from MediaStore thumbnails and are cached as JPEG files.
/// </summary>
public sealed class LibraryService
{
    public static readonly string DataDir = Android.App.Application.Context.FilesDir!.AbsolutePath;
    static readonly string CacheFile = Path.Combine(DataDir, "library.json");
    static readonly string CoverDir = Path.Combine(DataDir, "covers");

    public const int ShortFileSec = 30;

    public IReadOnlyList<Track> Tracks { get; private set; } = Array.Empty<Track>();
    public IReadOnlyList<Album> Albums { get; private set; } = Array.Empty<Album>();
    public IReadOnlyList<Artist> Artists { get; private set; } = Array.Empty<Artist>();
    public bool IsScanning { get; private set; }
    /// <summary>The user declined access to music.</summary>
    public bool NoAccess { get; private set; }

    public void NotifyNoAccess()
    {
        NoAccess = true;
        Changed?.Invoke();
    }

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

    public Task ScanAsync() => Task.Run(async () =>
    {
        await _scanLock.WaitAsync();
        try
        {
            IsScanning = true;
            NoAccess = false;
            Changed?.Invoke();
            Directory.CreateDirectory(CoverDir);

            var cr = Android.App.Application.Context.ContentResolver!;
            var list = Query(cr);
            // Game assets, ringtones and voice notes are often flagged as music: hide very short files.
            if (App.Settings.HideShortFiles)
                list = list.Where(t => t.DurationSec <= 0 || t.DurationSec >= ShortFileSec).ToList();
            bool firstRun = Tracks.Count == 0;
            if (firstRun)
            {
                // Show the music right away; covers fill in below.
                Build(list);
                Changed?.Invoke();
            }

            var covers = new Dictionary<long, string?>();
            int done = 0;
            foreach (var t in list)
            {
                if (!covers.TryGetValue(t.AlbumId, out var cover))
                {
                    cover = Cover(cr, t);
                    covers[t.AlbumId] = cover;
                    if (firstRun && ++done % 40 == 0)
                    {
                        Build(list);
                        Changed?.Invoke();
                    }
                }
                t.CoverPath = cover;
            }

            Build(list);
            Save(list);
            Android.Util.Log.Info("RebornMusic",
                $"scan: {list.Count} tracks, {covers.Count} albums, {covers.Values.Count(c => c != null)} covers");
        }
        catch (Exception ex)
        {
            App.Log("scan", ex);
        }
        finally
        {
            IsScanning = false;
            _scanLock.Release();
            Changed?.Invoke();
        }
    });

    static string? Clean(string? s) =>
        string.IsNullOrWhiteSpace(s) || s == "<unknown>" ? null : s.Trim();

    static List<Track> Query(ContentResolver cr)
    {
        var uri = MediaStore.Audio.Media.ExternalContentUri!;
        var cols = new List<string>
        {
            "_id", "title", "artist", "album", "album_id", "album_artist",
            "duration", "track", "year", "date_added", "_display_name",
        };
        bool hasGenre = OperatingSystem.IsAndroidVersionAtLeast(30);
        if (hasGenre) cols.Add("genre");

        var result = new List<Track>();
        using var c = cr.Query(uri, cols.ToArray(), "is_music != 0", null, null);
        if (c == null) return result;

        int iId = c.GetColumnIndex("_id"), iTitle = c.GetColumnIndex("title"), iArtist = c.GetColumnIndex("artist"),
            iAlbum = c.GetColumnIndex("album"), iAlbumId = c.GetColumnIndex("album_id"),
            iAlbumArtist = c.GetColumnIndex("album_artist"), iDur = c.GetColumnIndex("duration"),
            iTrack = c.GetColumnIndex("track"), iYear = c.GetColumnIndex("year"),
            iAdded = c.GetColumnIndex("date_added"), iName = c.GetColumnIndex("_display_name"),
            iGenre = hasGenre ? c.GetColumnIndex("genre") : -1;

        string? Str(int i) => i >= 0 && !c.IsNull(i) ? c.GetString(i) : null;
        long Long(int i) => i >= 0 && !c.IsNull(i) ? c.GetLong(i) : 0;

        while (c.MoveToNext())
        {
            long id = Long(iId);
            var name = Str(iName) ?? "";
            var artist = Clean(Str(iArtist));
            var albumArtist = Clean(Str(iAlbumArtist));
            long trackNo = Long(iTrack);
            result.Add(new Track
            {
                MediaId = id,
                Path = ContentUris.WithAppendedId(uri, id).ToString()!,
                AlbumId = Long(iAlbumId),
                Title = Clean(Str(iTitle)) ?? Path.GetFileNameWithoutExtension(name),
                Artist = artist ?? albumArtist ?? Fmt.UnknownArtist,
                AlbumArtist = albumArtist ?? artist ?? Fmt.UnknownArtist,
                Album = Clean(Str(iAlbum)) ?? Fmt.UnknownAlbum,
                Genre = Clean(Str(iGenre)) ?? "",
                // MediaStore packs the disc into the thousands: 2005 = disc 2, track 5.
                TrackNumber = (uint)(trackNo % 1000),
                Disc = (uint)(trackNo / 1000),
                Year = (uint)Math.Max(0, Long(iYear)),
                DurationSec = Long(iDur) / 1000.0,
                Modified = Long(iAdded),
            });
        }
        return result;
    }

    static string? Cover(ContentResolver cr, Track t)
    {
        var file = Path.Combine(CoverDir, $"{t.AlbumId}.jpg");
        var none = Path.Combine(CoverDir, $"{t.AlbumId}.none");
        if (File.Exists(file)) return file;
        if (File.Exists(none)) return null;

        Bitmap? bmp = null;
        var size = new Android.Util.Size(512, 512);
        try
        {
            var albumUri = ContentUris.WithAppendedId(MediaStore.Audio.Albums.ExternalContentUri!, t.AlbumId);
            bmp = cr.LoadThumbnail(albumUri, size, null);
        }
        catch (Exception ex) { Android.Util.Log.Info("RebornMusic", $"cover album {t.AlbumId}: {ex.GetType().Name} {ex.Message}"); }
        if (bmp == null)
        {
            try { bmp = cr.LoadThumbnail(AUri.Parse(t.Path)!, size, null); }
            catch (Exception ex) { Android.Util.Log.Info("RebornMusic", $"cover track {t.MediaId}: {ex.GetType().Name} {ex.Message}"); }
        }

        if (bmp == null)
        {
            // The system thumbnailer can't decode some embedded pictures: take the raw bytes from the tag.
            try
            {
                using var mmr = new Android.Media.MediaMetadataRetriever();
                mmr.SetDataSource(Android.App.Application.Context, AUri.Parse(t.Path)!);
                var pic = mmr.GetEmbeddedPicture();
                if (pic is { Length: > 0 })
                {
                    bmp = BitmapFactory.DecodeByteArray(pic, 0, pic.Length);
                    if (bmp == null)
                    {
                        File.WriteAllBytes(file, pic);
                        return file;
                    }
                }
            }
            catch (Exception ex) { Android.Util.Log.Info("RebornMusic", $"cover tag {t.MediaId}: {ex.GetType().Name} {ex.Message}"); }
        }

        try
        {
            if (bmp == null)
            {
                File.WriteAllText(none, "");
                return null;
            }
            if (bmp.Width > 512 || bmp.Height > 512)
            {
                var scaled = Bitmap.CreateScaledBitmap(bmp, 512, Math.Max(1, bmp.Height * 512 / Math.Max(1, bmp.Width)), true);
                bmp.Recycle();
                bmp = scaled;
            }
            using (var fs = File.Create(file))
                bmp.Compress(Bitmap.CompressFormat.Jpeg!, 88, fs);
            return file;
        }
        catch
        {
            return null;
        }
        finally
        {
            bmp?.Recycle();
            bmp?.Dispose();
        }
    }

    static string AlbumKey(Track t) => (t.AlbumArtist + "\u0001" + t.Album).ToLowerInvariant();

    void Build(List<Track> list)
    {
        var cmp = StringComparer.CurrentCultureIgnoreCase;

        var albums = list.GroupBy(AlbumKey).Select(g =>
        {
            var tracks = g.OrderBy(t => t.Disc).ThenBy(t => t.TrackNumber).ThenBy(t => t.Title, cmp).ToList();
            var cover = tracks.Select(t => t.CoverPath).FirstOrDefault(c => c != null);
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
