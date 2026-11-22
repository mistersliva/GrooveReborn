using System.Text.Json;

namespace Reborn.Services;

public sealed class SettingsService
{
    public string Theme { get; set; } = "Dark";
    public double Volume { get; set; } = 1.0;
    public bool HideShortFiles { get; set; } = true;
    public List<string> Recent { get; set; } = new();

    public void AddRecent(string path)
    {
        Recent.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        Recent.Insert(0, path);
        if (Recent.Count > 200) Recent.RemoveRange(200, Recent.Count - 200);
    }

    static string FilePath => System.IO.Path.Combine(LibraryService.DataDir, "settings.json");

    public static SettingsService Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<SettingsService>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { }
        return new SettingsService();
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
