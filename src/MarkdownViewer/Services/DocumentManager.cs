using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Markdig;
using Markdig.Syntax;

namespace MarkdownViewer.Services;

/// <summary>
/// Represents a single open Markdown document and its runtime state: file path,
/// editor text, dirty flag, autosave timer, preview <see cref="FlowDocument"/>,
/// and the editor control instance bound to the tab. Named <c>DocumentTab</c>
/// to avoid colliding with <c>Markdig.Syntax.MarkdownDocument</c> (which is
/// aliased in <c>FlowDocumentBuilder.cs</c> — a same-namespace type would
/// shadow that alias).
/// </summary>
public sealed class DocumentTab : IDisposable
{
    private readonly Timer? _autosaveTimer;
    private readonly MarkdownPipeline _pipeline;
    private bool _disposed;

    public string? FilePath { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string EncodingName { get; private set; } = "UTF-8";
    public bool IsDirty { get; private set; }
    public bool IsNew { get; private set; } = true;

    /// <summary>Tab title — filename, or "Untitled" if never saved.</summary>
    public string Title => FilePath is null ? "Untitled" : Path.GetFileName(FilePath);

    /// <summary>Editor control instance (TextBox) bound to this document's tab.</summary>
    public TextBox? Editor { get; set; }

    public FlowDocument? Preview { get; private set; }

    public event EventHandler? DirtyChanged;
    public event EventHandler? ContentChanged;
    public event EventHandler? Saved;

    public DocumentTab(MarkdownPipeline pipeline, AppSettings settings)
    {
        _pipeline = pipeline;
        if (settings.AutosaveEnabled && settings.AutosaveIntervalSeconds > 0)
        {
            _autosaveTimer = new Timer(_ => _ = TryAutosaveAsync(),
                null, Timeout.Infinite, Timeout.Infinite);
        }
    }

    public static DocumentTab FromFile(string path, MarkdownPipeline pipeline, AppSettings settings)
    {
        var doc = new DocumentTab(pipeline, settings);
        doc.LoadFrom(path);
        return doc;
    }

    public void LoadFrom(string path)
    {
        var (text, enc) = MarkdownService.ReadFile(path);
        Content = text;
        EncodingName = enc.EncodingName;
        FilePath = path;
        IsNew = false;
        IsDirty = false;
        if (Editor is not null) Editor.Text = text;
        RebuildPreview();
        StopAutosave();
    }

    public void SetContent(string text)
    {
        if (text == Content) return;
        Content = text;
        IsDirty = true;
        RebuildPreview();
        DirtyChanged?.Invoke(this, EventArgs.Empty);
        ContentChanged?.Invoke(this, EventArgs.Empty);
        ArmAutosave();
    }

    public void SaveTo(string path)
    {
        File.WriteAllText(path, Content, new UTF8Encoding(false));
        FilePath = path;
        IsNew = false;
        IsDirty = false;
        StopAutosave();
        DirtyChanged?.Invoke(this, EventArgs.Empty);
        Saved?.Invoke(this, EventArgs.Empty);
    }

    public void RebuildPreview()
    {
        try
        {
            var ast = (Markdig.Syntax.MarkdownDocument)Markdig.Markdown.Parse(Content, _pipeline);
            Preview = FlowDocumentBuilder.Build(ast, FilePath is null ? string.Empty : Path.GetDirectoryName(FilePath) ?? string.Empty);
        }
        catch
        {
            Preview = new FlowDocument();
        }
    }

    private void ArmAutosave()
    {
        if (_autosaveTimer is null || IsNew) return;
        _autosaveTimer.Change(TimeSpan.FromSeconds(SettingsService.Current.AutosaveIntervalSeconds), Timeout.InfiniteTimeSpan);
    }

    private void StopAutosave()
    {
        _autosaveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private async Task TryAutosaveAsync()
    {
        if (IsNew || !IsDirty || FilePath is null) return;
        try
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                File.WriteAllText(FilePath, Content, new UTF8Encoding(false));
                IsDirty = false;
                DirtyChanged?.Invoke(this, EventArgs.Empty);
            });
        }
        catch
        {
            // Autosave is best-effort.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _autosaveTimer?.Dispose();
    }
}

/// <summary>
/// Owns the list of open <see cref="DocumentTab"/> tabs and tracks the active
/// tab. No MVVM — the window subscribes to events and updates the UI.
/// </summary>
public sealed class DocumentManager : IDisposable
{
    private readonly MarkdownPipeline _pipeline;
    private readonly AppSettings _settings;
    private bool _disposed;

    public ObservableCollection<DocumentTab> Documents { get; } = new();

    public DocumentTab? Active => Documents.Count > 0 && _activeIndex >= 0 && _activeIndex < Documents.Count
        ? Documents[_activeIndex]
        : null;

    private int _activeIndex = -1;

    public event EventHandler? ActiveChanged;
    public event EventHandler? DocumentsChanged;

    public DocumentManager(MarkdownPipeline pipeline, AppSettings settings)
    {
        _pipeline = pipeline;
        _settings = settings;
    }

    public DocumentTab OpenNew()
    {
        var doc = new DocumentTab(_pipeline, _settings);
        doc.RebuildPreview();
        Documents.Add(doc);
        _activeIndex = Documents.Count - 1;
        ActiveChanged?.Invoke(this, EventArgs.Empty);
        DocumentsChanged?.Invoke(this, EventArgs.Empty);
        return doc;
    }

    public DocumentTab OpenFile(string path)
    {
        var existing = Documents.FirstOrDefault(d =>
            d.FilePath is not null &&
            string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _activeIndex = Documents.IndexOf(existing);
            ActiveChanged?.Invoke(this, EventArgs.Empty);
            return existing;
        }

        var doc = DocumentTab.FromFile(path, _pipeline, _settings);
        Documents.Add(doc);
        _activeIndex = Documents.Count - 1;
        ActiveChanged?.Invoke(this, EventArgs.Empty);
        DocumentsChanged?.Invoke(this, EventArgs.Empty);
        return doc;
    }

    public void Activate(int index)
    {
        if (index < 0 || index >= Documents.Count) return;
        _activeIndex = index;
        ActiveChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Close(DocumentTab doc)
    {
        int idx = Documents.IndexOf(doc);
        if (idx < 0) return;
        Documents.RemoveAt(idx);
        doc.Dispose();
        if (_activeIndex >= Documents.Count) _activeIndex = Documents.Count - 1;
        if (_activeIndex < 0 && Documents.Count > 0) _activeIndex = 0;
        ActiveChanged?.Invoke(this, EventArgs.Empty);
        DocumentsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var d in Documents) d.Dispose();
        Documents.Clear();
    }
}