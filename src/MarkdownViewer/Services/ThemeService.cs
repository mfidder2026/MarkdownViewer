using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;

namespace MarkdownViewer.Services;

/// <summary>
/// Applies a named ResourceDictionary theme to the application-wide merged
/// dictionaries. Light/Dark live under <c>Themes/</c>. Theme name is persisted
/// via <see cref="SettingsService"/>.
/// </summary>
public static class ThemeService
{
    public const string LightKey = "Light";
    public const string DarkKey = "Dark";

    public static IReadOnlyList<string> AvailableThemes { get; } = new[] { LightKey, DarkKey };

    public static void Apply(string themeName)
    {
        if (!AvailableThemes.Contains(themeName)) themeName = LightKey;
        var app = Application.Current;
        if (app is null) return;

        var dictionaries = app.Resources.MergedDictionaries;
        // Remove any previously applied theme dictionary.
        for (int i = dictionaries.Count - 1; i >= 0; i--)
        {
            var src = dictionaries[i].Source?.OriginalString ?? string.Empty;
            if (src.EndsWith("Light.xaml", StringComparison.OrdinalIgnoreCase) ||
                src.EndsWith("Dark.xaml", StringComparison.OrdinalIgnoreCase))
            {
                dictionaries.RemoveAt(i);
            }
        }

        var pack = new Uri($"pack://application:,,,/Themes/{themeName}.xaml", UriKind.Absolute);
        dictionaries.Add(new ResourceDictionary { Source = pack });

        SettingsService.Current.Theme = themeName;
        SettingsService.Save();
    }

    public static string CurrentThemeName
    {
        get
        {
            var name = SettingsService.Current.Theme;
            return AvailableThemes.Contains(name) ? name : LightKey;
        }
    }
}