using System.Text.Json;

namespace Reborn.Services;

public sealed class SettingsService
{
    public List<string> Folders { get; set; } = new();
    public string Theme { get; set; } = "System";
    public double Volume { get; set; } = 0.8;
    public List<string> Recent { get; set; } = new();

    public void AddRecent(string path)
    {
        Recent.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        Recent.Insert(0, path);
        if (Recent.Count > 200) Recent.RemoveRange(200, Recent.Count - 200);
    }

    static string FilePath => Path.Combine(LibraryService.DataDir, "settings.json");

    public static SettingsService Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<SettingsService>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { }

        var s = new SettingsService();
        var music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        if (!string.IsNullOrEmpty(music)) s.Folders.Add(music);
        return s;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(LibraryService.DataDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
