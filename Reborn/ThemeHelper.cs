using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Reborn;

public static class ThemeHelper
{
    /// <summary>Look up a theme brush for a specific element theme (Application.Resources only knows the app theme).</summary>
    public static Brush? ThemeDictionaryBrush(this ResourceDictionary _, string key, ElementTheme theme)
    {
        var themeKey = theme == ElementTheme.Light ? "Light" : "Dark";
        var found = Find(Application.Current.Resources, key, themeKey);
        return found as Brush;
    }

    static object? Find(ResourceDictionary dict, string key, string themeKey)
    {
        // Later merged dictionaries win, so search them in reverse.
        for (int i = dict.MergedDictionaries.Count - 1; i >= 0; i--)
        {
            var r = Find(dict.MergedDictionaries[i], key, themeKey);
            if (r != null) return r;
        }
        if (dict.ThemeDictionaries.TryGetValue(themeKey, out var td) && td is ResourceDictionary t &&
            t.TryGetValue(key, out var v))
            return v;
        return dict.TryGetValue(key, out var plain) ? plain : null;
    }
}
