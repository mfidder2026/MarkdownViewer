using System;
using System.IO;
using System.Windows;

namespace MarkdownViewer;

/// <summary>
/// Application entry point. Inspects command-line arguments: if a Markdown
/// file path is provided, it is opened immediately on startup (no welcome screen).
/// </summary>
public partial class App : Application
{
    public static string? StartupFilePath { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
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

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        if (StartupFilePath is not null)
        {
            window.OpenFilePath(StartupFilePath);
        }
    }

    private static bool IsMarkdownExtension(string ext)
    {
        return string.Equals(ext, ".md", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(ext, ".markdown", StringComparison.OrdinalIgnoreCase);
    }
}