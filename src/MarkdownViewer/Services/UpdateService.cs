using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace MarkdownViewer.Services;

/// <summary>
/// Opt-in update check. Performs a single HTTP GET against a configurable
/// JSON endpoint and compares the reported version with the running version.
/// No automatic download or install — only a notification. Disabled by
/// preference and surfaced as a no-op if the user opts out.
/// </summary>
public sealed class UpdateService : IDisposable
{
    private readonly HttpClient _http;
    private bool _disposed;

    public string? LatestVersion { get; private set; }
    public string? ReleaseUrl { get; private set; }
    public string? Notes { get; private set; }

    public UpdateService() => _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

    public async Task<bool> CheckAsync()
    {
        if (!SettingsService.Current.UpdateCheckEnabled) return false;

        try
        {
            // Endpoint is intentionally a placeholder — real releases would
            // point at the project's GitHub releases JSON. Kept configurable.
            const string url = "https://api.github.com/repos/nicktakin/markdown-viewer/releases/latest";
            using var req = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("MarkdownViewer/0.2");
            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return false;

            string body = await resp.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("tag_name", out var tag))
                LatestVersion = tag.GetString()?.TrimStart('v');
            if (doc.RootElement.TryGetProperty("html_url", out var link))
                ReleaseUrl = link.GetString();
            if (doc.RootElement.TryGetProperty("body", out var bodyEl))
                Notes = bodyEl.GetString();
        }
        catch
        {
            // Network errors / parse errors are not fatal — return false.
            return false;
        }

        string? current = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
        return !string.IsNullOrEmpty(LatestVersion) && LatestVersion != current;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _http.Dispose();
        }
    }
}