using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MarkdownViewer.Services;

/// <summary>
/// Minimal plugin host. Plugins are .NET assemblies placed in
/// <c>%APPDATA%\MarkdownViewer\plugins\</c>. Each assembly may export one or
/// more types implementing <see cref="IMarkdownViewerPlugin"/>; they are
/// instantiated and their <see cref="IMarkdownViewerPlugin.Initialize"/> is
/// called once at startup. No remote loading, no untrusted code execution
/// beyond the user's own plugin folder — keep it conservative.
/// </summary>
public interface IMarkdownViewerPlugin
{
    string Name { get; }
    string Version { get; }
    void Initialize(PluginContext context);
}

/// <summary>Minimal context handed to plugins — read-only app reference.</summary>
public sealed class PluginContext
{
    public string AppVersion { get; init; } = "0.2.0";
    public string DataDirectory { get; init; } = string.Empty;
    public AppSettings Settings => SettingsService.Current;
}

public static class PluginHost
{
    private static readonly string PluginDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MarkdownViewer", "plugins");

    public static IReadOnlyList<IMarkdownViewerPlugin> Loaded { get; } = new List<IMarkdownViewerPlugin>();

    public static IReadOnlyList<IMarkdownViewerPlugin> LoadAll()
    {
        var list = (List<IMarkdownViewerPlugin>)Loaded;
        try
        {
            if (!Directory.Exists(PluginDirectory)) return list;
            foreach (var dll in Directory.EnumerateFiles(PluginDirectory, "*.dll"))
            {
                TryLoadAssembly(dll, list);
            }
        }
        catch
        {
            // Plugin loading must never crash the host.
        }
        return list;
    }

    private static void TryLoadAssembly(string path, List<IMarkdownViewerPlugin> list)
    {
        try
        {
            var ctx = new PluginContext
            {
                AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.2.0",
                DataDirectory = PluginDirectory,
            };
            var asm = Assembly.LoadFrom(path);
            foreach (var t in asm.GetExportedTypes())
            {
                if (t.IsClass && !t.IsAbstract && typeof(IMarkdownViewerPlugin).IsAssignableFrom(t))
                {
                    try
                    {
                        var plugin = (IMarkdownViewerPlugin)Activator.CreateInstance(t)!;
                        plugin.Initialize(ctx);
                        list.Add(plugin);
                    }
                    catch
                    {
                        // A single failing plugin constructor must not stop others.
                    }
                }
            }
        }
        catch
        {
            // Bad IL / missing deps — skip silently.
        }
    }
}