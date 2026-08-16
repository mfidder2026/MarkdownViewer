using System;
using System.IO;
using System.Windows;
using MarkdownViewer.Services;

namespace MarkdownViewer;

/// <summary>
/// Application entry point. v0.2 loads settings, applies the persisted theme,
/// loads plugins, then opens the main window — optionally with a file passed
/// on the command line. No welcome screen.
/// </summary>
public partial class App : Application
{
    public static string? StartupFilePath { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Load settings first — theme/prefs drive everything that follows.
            SettingsService.Load();

            base.OnStartup(e);

            if (e.Args.Length > 0)
            {
                string candidate = e.Args[0].Trim('"');
                if (!string.IsNullOrWhiteSpace(candidate) &&
                    File.Exists(candidate) &&
                    IsMarkdownExtension(Path.GetExtension(candidate)))
                {
                    StartupFilePath = candidate;
                }
            }

            // Load plugins (best-effort — never block startup).
            try { PluginHost.LoadAll(); } catch { /* ignore */ }

            var window = new MainWindow();
            MainWindow = window;
            window.Show();

            if (StartupFilePath is not null)
            {
                window.OpenFilePath(StartupFilePath);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start application:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static bool IsMarkdownExtension(string ext)
    {
        return string.Equals(ext, ".md", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(ext, ".markdown", StringComparison.OrdinalIgnoreCase);
    }
}