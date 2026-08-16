using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarkdownViewer.Services;

/// <summary>
/// Local-only telemetry. The v0.1 developer prompt forbids cloud telemetry,
/// so v0.2 honors the spirit: when the user opts in via Preferences, events
/// are appended to a local JSONL log under <c>%APPDATA%\MarkdownViewer</c>.
/// Nothing is ever sent anywhere.
/// </summary>
public static class TelemetryService
{
    private static readonly string LogDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MarkdownViewer");
    private static readonly string LogPath = Path.Combine(LogDirectory, "telemetry.jsonl");

    private static readonly object Gate = new();

    public static void Log(string eventName, params (string Key, string Value)[] props)
    {
        if (!SettingsService.Current.TelemetryEnabled) return;
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var entry = new TelemetryEntry
            {
                TimestampUtc = DateTime.UtcNow.ToString("O"),
                Event = eventName,
                Properties = new System.Collections.Generic.Dictionary<string, string>(),
            };
            foreach (var (k, v) in props) entry.Properties[k] = v;

            string line = JsonSerializer.Serialize(entry,
                new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.Never });
            lock (Gate)
            {
                File.AppendAllText(LogPath, line + "\n", Encoding.UTF8);
            }
        }
        catch
        {
            // Telemetry must never affect the user.
        }
    }

    public static void Toggle(bool enabled)
    {
        SettingsService.Current.TelemetryEnabled = enabled;
        SettingsService.Save();
        if (enabled) Log("telemetry_enabled");
    }

    public sealed class TelemetryEntry
    {
        public string TimestampUtc { get; set; } = "";
        public string Event { get; set; } = "";
        public System.Collections.Generic.Dictionary<string, string> Properties { get; set; } = new();
    }
}