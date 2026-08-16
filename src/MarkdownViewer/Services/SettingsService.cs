using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarkdownViewer.Services;

/// <summary>
/// Strongly typed application settings persisted as JSON in
/// <c>%APPDATA%\MarkdownViewer\settings.json</c>. Kept deliberately flat —
/// no abstraction layers, no DI.
/// </summary>
public sealed class AppSettings
{
    public string Theme { get; set; } = "Light";
    public bool ShowFileBrowser { get; set; } = true;
    public bool ShowLintPanel { get; set; } = true;
    public bool ShowEditorPane { get; set; } = true;
    public bool WordWrap { get; set; } = true;
    public bool ShowLineNumbers { get; set; } = false;
    public bool SpellCheck { get; set; } = true;
    public bool AutosaveEnabled { get; set; } = true;
    public int AutosaveIntervalSeconds { get; set; } = 30;
    public int RecentDocumentLimit { get; set; } = 15;
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 15;
    public bool TelemetryEnabled { get; set; } = false;
    public bool UpdateCheckEnabled { get; set; } = true;
    public string LastOpenDirectory { get; set; } = string.Empty;
    public List<string> RecentDocuments { get; set; } = new();
    public List<string> Favourites { get; set; } = new();
    public List<string> PluginPaths { get; set; } = new();
}

/// <summary>
/// Loads and saves <see cref="AppSettings"/>. Failures are swallowed and a
/// fresh default instance is returned — settings must never block startup.
/// </summary>
public static class SettingsService
{
    private static readonly string SettingsDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MarkdownViewer");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static AppSettings Current { get; private set; } = new();

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null) Current = loaded;
            }
        }
        catch
        {
            // Corrupt settings — fall back to defaults. Do not crash startup.
            Current = new AppSettings();
        }
        return Current;
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            string json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(SettingsPath, json, Encoding.UTF8);
        }
        catch
        {
            // Read-only %APPDATA% or disk full — settings are best-effort.
        }
    }

    public static void AddRecent(string path, int limit)
    {
        if (string.IsNullOrEmpty(path)) return;
        Current.RecentDocuments.RemoveAll(p =>
            string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        Current.RecentDocuments.Insert(0, path);
        if (Current.RecentDocuments.Count > limit)
            Current.RecentDocuments.RemoveRange(limit, Current.RecentDocuments.Count - limit);
        Save();
    }

    public static void ToggleFavourite(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        int idx = Current.Favourites.FindIndex(p =>
            string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) Current.Favourites.RemoveAt(idx);
        else Current.Favourites.Insert(0, path);
        Save();
    }

    public static bool IsFavourite(string path) =>
        Current.Favourites.Exists(p =>
            string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
}