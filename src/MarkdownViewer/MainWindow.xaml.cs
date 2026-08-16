using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MarkdownViewer.Controls;
using MarkdownViewer.Services;
using MarkdownViewer.Windows;
using Microsoft.Win32;

namespace MarkdownViewer;

/// <summary>
/// Main application window for v0.2. Hosts tabs, editor+preview split, file
/// browser, lint panel, search bar, status bar, and the menu wiring for all
/// features. No MVVM — direct event handlers and routed commands.
/// </summary>
public partial class MainWindow : Window
{
    // --- Routed commands ---------------------------------------------------
    public static readonly RoutedCommand NewTabCommand = new();
    public static readonly RoutedCommand OpenCommand = new();
    public static readonly RoutedCommand SaveCommand = new();
    public static readonly RoutedCommand SaveAsCommand = new();
    public static readonly RoutedCommand CloseTabCommand = new();
    public static readonly RoutedCommand PrintCommand = new();
    public static readonly RoutedCommand ExportHtmlCommand = new();
    public static readonly RoutedCommand ExportPdfCommand = new();
    public static readonly RoutedCommand FindCommand = new();
    public static readonly RoutedCommand ToggleFavouriteCommand = new();
    public static readonly RoutedCommand ToggleBrowserCommand = new();
    public static readonly RoutedCommand ToggleEditorCommand = new();
    public static readonly RoutedCommand ToggleLintCommand = new();
    public static readonly RoutedCommand LightThemeCommand = new();
    public static readonly RoutedCommand DarkThemeCommand = new();
    public static readonly RoutedCommand PreferencesCommand = new();
    public static readonly RoutedCommand CheckUpdateCommand = new();
    public static readonly RoutedCommand ManagePluginsCommand = new();
    public static readonly RoutedCommand AboutCommand = new();

    // --- Services ----------------------------------------------------------
    // Single shared Markdig pipeline (advanced/GitHub-flavoured). Built once
    // in MarkdownService and reused by all documents and the export service.
    private readonly Markdig.MarkdownPipeline _pipeline = MarkdownService.Pipeline;
    private readonly DocumentManager _docs;
    private readonly UpdateService _updateService = new();

    // --- Search state ------------------------------------------------------
    private List<int> _searchMatches = new();
    private int _searchIndex = -1;

    public MainWindow()
    {
        var settings = SettingsService.Load();
        _docs = new DocumentManager(_pipeline, settings);

        InitializeComponent();

        // Command bindings.
        CommandBindings.Add(new CommandBinding(NewTabCommand, OnNewTab));
        CommandBindings.Add(new CommandBinding(OpenCommand, OnOpen));
        CommandBindings.Add(new CommandBinding(SaveCommand, OnSave, CanSave));
        CommandBindings.Add(new CommandBinding(SaveAsCommand, OnSaveAs, CanSave));
        CommandBindings.Add(new CommandBinding(CloseTabCommand, OnCloseTab, CanCloseTab));
        CommandBindings.Add(new CommandBinding(PrintCommand, OnPrint, CanSave));
        CommandBindings.Add(new CommandBinding(ExportHtmlCommand, OnExportHtml, CanSave));
        CommandBindings.Add(new CommandBinding(ExportPdfCommand, OnExportPdf, CanSave));
        CommandBindings.Add(new CommandBinding(FindCommand, OnFind, CanSave));
        CommandBindings.Add(new CommandBinding(ToggleFavouriteCommand, OnToggleFavourite, CanSave));
        CommandBindings.Add(new CommandBinding(ToggleBrowserCommand, OnToggleBrowser));
        CommandBindings.Add(new CommandBinding(ToggleEditorCommand, OnToggleEditor));
        CommandBindings.Add(new CommandBinding(ToggleLintCommand, OnToggleLint));
        CommandBindings.Add(new CommandBinding(LightThemeCommand, (_, _) => ApplyTheme("Light")));
        CommandBindings.Add(new CommandBinding(DarkThemeCommand, (_, _) => ApplyTheme("Dark")));
        CommandBindings.Add(new CommandBinding(PreferencesCommand, OnPreferences));
        CommandBindings.Add(new CommandBinding(CheckUpdateCommand, OnCheckUpdate));
        CommandBindings.Add(new CommandBinding(ManagePluginsCommand, OnManagePlugins));
        CommandBindings.Add(new CommandBinding(AboutCommand, OnAbout));

        // Wire document manager events.
        _docs.DocumentsChanged += (_, _) => RebuildTabs();
        _docs.ActiveChanged += (_, _) => RefreshActive();

        // Wire search bar.
        SearchBarCtrl.QueryChanged += OnSearchQueryChanged;
        SearchBarCtrl.NavigateRequested += OnSearchNavigate;

        // Apply persisted theme and panel visibility.
        ThemeService.Apply(ThemeService.CurrentThemeName);
        ApplyPanelVisibility();

        // Populate recents/favourites menus.
        RebuildRecentsMenu();
        RebuildFavouritesMenu();

        // Load plugins (best-effort).
        try { PluginHost.LoadAll(); }
        catch { /* ignore */ }

        TelemetryService.Log("app_start", ("version", Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.2.0"));

        // Optional update check on startup.
        if (SettingsService.Current.UpdateCheckEnabled)
            _ = DoUpdateCheckAsync(silent: true);
    }

    // --- Opening files -----------------------------------------------------

    /// <summary>Opens and renders a Markdown file by path (used by App for CLI args).</summary>
    public void OpenFilePath(string path)
    {
        try
        {
            _docs.OpenFile(path);
            SettingsService.AddRecent(path, SettingsService.Current.RecentDocumentLimit);
            SettingsService.Current.LastOpenDirectory = Path.GetDirectoryName(path) ?? string.Empty;
            SettingsService.Save();
            RebuildRecentsMenu();
            RefreshFileBrowser();
            TelemetryService.Log("file_open", ("path", path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Could not open the file:\n{path}\n\n{ex.Message}",
                "Open File", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnNewTab(object sender, ExecutedRoutedEventArgs e)
    {
        _docs.OpenNew();
        Tabs.SelectedIndex = _docs.Documents.Count - 1;
    }

    private void OnOpen(object sender, ExecutedRoutedEventArgs e) => DoOpen();

    private void DoOpen()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open Markdown File",
            Filter = "Markdown files (*.md;*.markdown)|*.md;*.markdown|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = string.IsNullOrEmpty(SettingsService.Current.LastOpenDirectory)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : SettingsService.Current.LastOpenDirectory,
        };
        if (dlg.ShowDialog(this) == true) OpenFilePath(dlg.FileName);
    }

    // --- Save / Save As ----------------------------------------------------

    private void CanSave(object sender, CanExecuteRoutedEventArgs e) =>
        e.CanExecute = _docs.Active is not null;

    private void OnSave(object sender, ExecutedRoutedEventArgs e) => DoSave(saveAs: false);

    private void OnSaveAs(object sender, ExecutedRoutedEventArgs e) => DoSave(saveAs: true);

    private void DoSave(bool saveAs)
    {
        var doc = _docs.Active;
        if (doc is null) return;

        string? path = doc.FilePath;
        if (saveAs || string.IsNullOrEmpty(path))
        {
            var dlg = new SaveFileDialog
            {
                Title = "Save Markdown File",
                Filter = "Markdown (*.md)|*.md|Markdown (*.markdown)|*.markdown|All files (*.*)|*.*",
                FileName = string.IsNullOrEmpty(path) ? "Untitled.md" : Path.GetFileName(path),
                InitialDirectory = string.IsNullOrEmpty(path)
                    ? (string.IsNullOrEmpty(SettingsService.Current.LastOpenDirectory)
                        ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                        : SettingsService.Current.LastOpenDirectory)
                    : Path.GetDirectoryName(path),
            };
            if (dlg.ShowDialog(this) != true) return;
            path = dlg.FileName;
        }

        try
        {
            doc.SaveTo(path);
            SettingsService.AddRecent(path, SettingsService.Current.RecentDocumentLimit);
            SettingsService.Current.LastOpenDirectory = Path.GetDirectoryName(path) ?? string.Empty;
            SettingsService.Save();
            RebuildRecentsMenu();
            RebuildTabs();
            UpdateTitle();
            SetStatus($"Saved {Path.GetFileName(path)}");
            TelemetryService.Log("file_save", ("path", path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Could not save the file:\n{path}\n\n{ex.Message}",
                "Save", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // --- Close tab ---------------------------------------------------------

    private void CanCloseTab(object sender, CanExecuteRoutedEventArgs e) =>
        e.CanExecute = _docs.Active is not null;

    private void OnCloseTab(object sender, ExecutedRoutedEventArgs e)
    {
        var doc = _docs.Active;
        if (doc is not null) DoCloseTab(doc);
    }

    private void DoCloseTab(DocumentTab doc)
    {
        if (doc.IsDirty)
        {
            var r = MessageBox.Show(this,
                $"\"{doc.Title}\" has unsaved changes. Save before closing?",
                "Close Tab", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (r == MessageBoxResult.Cancel) return;
            if (r == MessageBoxResult.Yes) { DoSave(saveAs: false); if (doc.IsDirty) return; }
        }
        _docs.Close(doc);
    }

    // --- Editor / preview --------------------------------------------------

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        var doc = _docs.Active;
        if (doc is null) return;
        // Avoid feedback loop: only propagate if content actually differs.
        if (doc.Content != Editor.Text)
        {
            doc.SetContent(Editor.Text);
            UpdateTabHeader(doc);
            RunLint(doc.Content);
            SetStatus(doc.IsDirty ? "Modified" : "Saved");
        }
    }

    // --- Tabs --------------------------------------------------------------

    private void RebuildTabs()
    {
        Tabs.Items.Clear();
        foreach (var doc in _docs.Documents)
        {
            var tab = new TabItem();
            UpdateTabHeader(doc, tab);
            Tabs.Items.Add(tab);
        }
        if (Tabs.Items.Count > 0) Tabs.SelectedIndex = Math.Max(0, IndexOfActive());
    }

    private int IndexOfActive()
    {
        var active = _docs.Active;
        for (int i = 0; i < _docs.Documents.Count; i++)
            if (_docs.Documents[i] == active) return i;
        return -1;
    }

    private void UpdateTabHeader(DocumentTab doc, TabItem? tab = null)
    {
        tab ??= Tabs.Items.OfType<TabItem>().ElementAtOrDefault(IndexOf(doc));
        if (tab is null) return;
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TextBlock
        {
            Text = doc.IsDirty ? doc.Title + " •" : doc.Title,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        });
        var closeBtn = new Button
        {
            Content = "✕",
            Width = 20,
            Height = 20,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Tag = doc,
        };
        closeBtn.Click += OnTabCloseClick;
        header.Children.Add(closeBtn);
        tab.Header = header;
    }

    private int IndexOf(DocumentTab doc) => _docs.Documents.IndexOf(doc);

    private void OnTabCloseClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is DocumentTab doc)
        {
            Tabs.SelectedIndex = IndexOf(doc);
            DoCloseTab(doc);
        }
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Tabs.SelectedIndex < 0 || Tabs.SelectedIndex >= _docs.Documents.Count) return;
        _docs.Activate(Tabs.SelectedIndex);
    }

    private void RefreshActive()
    {
        var doc = _docs.Active;
        if (doc is null)
        {
            Editor.Visibility = Visibility.Collapsed;
            Splitter.Visibility = Visibility.Collapsed;
            DocViewer.Document = null;
            EmptyState.Visibility = Visibility.Visible;
            UpdateTitle();
            SetStatus(string.Empty);
            LintList.Items.Clear();
            LintSummary.Text = string.Empty;
            return;
        }

        // Bind editor.
        doc.Editor = Editor;
        if (Editor.Text != doc.Content) Editor.Text = doc.Content;
        ApplyEditorOptions();

        // Bind preview.
        DocViewer.Document = doc.Preview;

        // Empty state hides once any tab is active.
        EmptyState.Visibility = Visibility.Collapsed;

        // Editor pane visibility per preferences.
        ApplyEditorPaneVisibility();

        UpdateTitle();
        SetStatus(doc.IsDirty ? "Modified" : (doc.IsNew ? "New" : "Saved"));
        RunLint(doc.Content);
    }

    private void ApplyEditorOptions()
    {
        var s = SettingsService.Current;
        Editor.TextWrapping = s.WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        Editor.SpellCheck.IsEnabled = s.SpellCheck;
        Editor.FontFamily = new System.Windows.Media.FontFamily(s.FontFamily);
        Editor.FontSize = s.FontSize;
        // Line numbers are not natively supported by TextBox in WPF without a
        // custom control; left as a preference stub for now.
    }

    private void ApplyEditorPaneVisibility()
    {
        var s = SettingsService.Current;
        if (s.ShowEditorPane)
        {
            Editor.Visibility = Visibility.Visible;
            Splitter.Visibility = Visibility.Visible;
            EditorColumn.Width = new GridLength(1, GridUnitType.Star);
            SplitterColumn.Width = new GridLength(5);
            PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            Editor.Visibility = Visibility.Collapsed;
            Splitter.Visibility = Visibility.Collapsed;
            EditorColumn.Width = new GridLength(0);
            SplitterColumn.Width = new GridLength(0);
            PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
        }
    }

    private void ApplyPanelVisibility()
    {
        BrowserHost.Visibility = SettingsService.Current.ShowFileBrowser
            ? Visibility.Visible : Visibility.Collapsed;
        LintHost.Visibility = SettingsService.Current.ShowLintPanel
            ? Visibility.Visible : Visibility.Collapsed;
    }

    // --- Lint --------------------------------------------------------------

    private void RunLint(string content)
    {
        LintList.Items.Clear();
        var issues = MarkdownLinter.Lint(content);
        int errors = 0, warnings = 0;
        foreach (var issue in issues)
        {
            if (issue.Level == LintIssue.SeverityKind.Error) errors++;
            else if (issue.Level == LintIssue.SeverityKind.Warning) warnings++;
            LintList.Items.Add($"L{issue.Line}:C{issue.Column} [{issue.RuleId}] {issue.Message}");
        }
        LintSummary.Text = issues.Count == 0
            ? "Lint: clean"
            : $"Lint: {errors}E {warnings}W {issues.Count} total";
    }

    private void OnLintDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LintList.SelectedIndex < 0) return;
        // Jump to line in the editor (rough — based on the "L<n>" prefix).
        var text = LintList.SelectedItem?.ToString() ?? string.Empty;
        int colon = text.IndexOf(':');
        if (colon > 1 && int.TryParse(text.AsSpan(1, colon - 1), out int line) && line > 0)
        {
            int pos = 0;
            for (int i = 1; i < line && pos < Editor.Text.Length; i++)
            {
                int next = Editor.Text.IndexOf('\n', pos) + 1;
                if (next == 0) break;
                pos = next;
            }
            Editor.Focus();
            Editor.CaretIndex = pos;
            Editor.SelectionStart = pos;
            Editor.SelectionLength = 0;
            // TextBox has no built-in ScrollToLine; scroll by lines approx.
            int lineIndex = Editor.GetLineIndexFromCharacterIndex(Math.Min(pos, Editor.Text.Length - 1));
            if (lineIndex >= 0) Editor.ScrollToLine(lineIndex);
        }
    }

    // --- Search ------------------------------------------------------------

    private void OnFind(object sender, ExecutedRoutedEventArgs e) => SearchBarCtrl.Open();

    private void OnSearchQueryChanged(object? sender, string query)
    {
        _searchMatches.Clear();
        _searchIndex = -1;
        if (string.IsNullOrEmpty(query)) { SearchBarCtrl.SetMatchCount(0); return; }

        var doc = _docs.Active;
        if (doc is null) { SearchBarCtrl.SetMatchCount(0); return; }

        int idx = 0;
        while ((idx = doc.Content.IndexOf(query, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            _searchMatches.Add(idx);
            idx += query.Length;
        }
        SearchBarCtrl.SetMatchCount(_searchMatches.Count);
        if (_searchMatches.Count > 0)
        {
            _searchIndex = 0;
            HighlightMatch(_searchMatches[0], query.Length);
        }
    }

    private void OnSearchNavigate(object? sender, SearchDirection dir)
    {
        if (_searchMatches.Count == 0) return;
        if (dir == SearchDirection.Next)
            _searchIndex = (_searchIndex + 1) % _searchMatches.Count;
        else
            _searchIndex = (_searchIndex - 1 + _searchMatches.Count) % _searchMatches.Count;
        HighlightMatch(_searchMatches[_searchIndex], SearchBarCtrl.QueryBox.Text.Length);
    }

    private void HighlightMatch(int start, int length)
    {
        Editor.Focus();
        Editor.SelectionStart = start;
        Editor.SelectionLength = length;
        Editor.ScrollToLine(Editor.GetLineIndexFromCharacterIndex(start));
    }

    // --- Favourites / Recents ----------------------------------------------

    private void OnToggleFavourite(object sender, ExecutedRoutedEventArgs e)
    {
        var doc = _docs.Active;
        if (doc?.FilePath is null) return;
        SettingsService.ToggleFavourite(doc.FilePath);
        RebuildFavouritesMenu();
    }

    private void RebuildRecentsMenu()
    {
        RecentMenu.Items.Clear();
        var recents = SettingsService.Current.RecentDocuments
            .Where(File.Exists).ToList();
        if (recents.Count == 0)
        {
            RecentMenu.Items.Add(new MenuItem { Header = "(none)", IsEnabled = false });
            return;
        }
        foreach (var path in recents)
        {
            var item = new MenuItem { Header = path };
            item.Click += (_, _) => OpenFilePath(path);
            RecentMenu.Items.Add(item);
        }
    }

    private void RebuildFavouritesMenu()
    {
        FavouritesMenu.Items.Clear();
        var favs = SettingsService.Current.Favourites.Where(File.Exists).ToList();
        if (favs.Count == 0)
        {
            FavouritesMenu.Items.Add(new MenuItem { Header = "(none)", IsEnabled = false });
            return;
        }
        foreach (var path in favs)
        {
            var item = new MenuItem { Header = path };
            item.Click += (_, _) => OpenFilePath(path);
            FavouritesMenu.Items.Add(item);
        }
    }

    // --- File browser sidebar ---------------------------------------------

    private void RefreshFileBrowser()
    {
        FileTree.Items.Clear();
        var doc = _docs.Active;
        string? dir = doc?.FilePath is null
            ? (string.IsNullOrEmpty(SettingsService.Current.LastOpenDirectory) ? null : SettingsService.Current.LastOpenDirectory)
            : Path.GetDirectoryName(doc.FilePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
        AddDirectoryNode(FileTree.Items, dir, depth: 0);
    }

    private void AddDirectoryNode(ItemCollection parent, string dir, int depth)
    {
        if (depth > 2) return; // keep the tree shallow for performance
        var node = new TreeViewItem { Header = Path.GetFileName(dir), Tag = dir, IsExpanded = depth == 0 };
        try
        {
            foreach (var sub in Directory.EnumerateDirectories(dir).OrderBy(d => Path.GetFileName(d)))
                AddDirectoryNode(node.Items, sub, depth + 1);
            foreach (var file in Directory.EnumerateFiles(dir)
                         .Where(f => IsMarkdownExt(Path.GetExtension(f)))
                         .OrderBy(f => Path.GetFileName(f)))
            {
                var fileNode = new TreeViewItem
                {
                    Header = Path.GetFileName(file),
                    Tag = file,
                    FontWeight = depth == 0 ? FontWeights.Normal : FontWeights.Normal,
                };
                node.Items.Add(fileNode);
            }
        }
        catch { /* unreadable dir — skip */ }
        parent.Add(node);
    }

    private static bool IsMarkdownExt(string ext) =>
        ext.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
        ext.Equals(".markdown", StringComparison.OrdinalIgnoreCase);

    private void OnFileTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileTree.SelectedItem is TreeViewItem node && node.Tag is string path && File.Exists(path))
            OpenFilePath(path);
    }

    // --- Export / Print ----------------------------------------------------

    private void OnExportHtml(object sender, ExecutedRoutedEventArgs e)
    {
        var doc = _docs.Active;
        if (doc is null) return;
        var dlg = new SaveFileDialog
        {
            Title = "Export HTML",
            Filter = "HTML (*.html)|*.html|All files (*.*)|*.*",
            FileName = Path.GetFileNameWithoutExtension(doc.Title) + ".html",
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            ExportService.ExportHtml(doc.Content, dlg.FileName, doc.Title);
            SetStatus($"Exported {dlg.FileName}");
            TelemetryService.Log("export_html", ("path", dlg.FileName));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"HTML export failed:\n{ex.Message}",
                "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnExportPdf(object sender, ExecutedRoutedEventArgs e)
    {
        var doc = _docs.Active;
        if (doc?.Preview is null) return;
        MessageBox.Show(this,
            "PDF export uses the Windows print dialog.\nChoose \"Microsoft Print to PDF\" as the printer.",
            "PDF Export", MessageBoxButton.OK, MessageBoxImage.Information);
        PrintService.ExportPdf(doc.Preview, doc.Title);
        TelemetryService.Log("export_pdf");
    }

    private void OnPrint(object sender, ExecutedRoutedEventArgs e)
    {
        var doc = _docs.Active;
        if (doc?.Preview is null) return;
        PrintService.Print(doc.Preview, doc.Title);
        TelemetryService.Log("print");
    }

    // --- Theme / panels ----------------------------------------------------

    private void ApplyTheme(string name)
    {
        ThemeService.Apply(name);
        TelemetryService.Log("theme_change", ("theme", name));
    }

    private void OnToggleBrowser(object sender, ExecutedRoutedEventArgs e)
    {
        var s = SettingsService.Current;
        s.ShowFileBrowser = !s.ShowFileBrowser;
        SettingsService.Save();
        ApplyPanelVisibility();
        if (s.ShowFileBrowser) RefreshFileBrowser();
    }

    private void OnToggleEditor(object sender, ExecutedRoutedEventArgs e)
    {
        var s = SettingsService.Current;
        s.ShowEditorPane = !s.ShowEditorPane;
        SettingsService.Save();
        ApplyEditorPaneVisibility();
    }

    private void OnToggleLint(object sender, ExecutedRoutedEventArgs e)
    {
        var s = SettingsService.Current;
        s.ShowLintPanel = !s.ShowLintPanel;
        SettingsService.Save();
        ApplyPanelVisibility();
    }

    // --- Preferences / Updates / Plugins / About ---------------------------

    private void OnPreferences(object sender, ExecutedRoutedEventArgs e)
    {
        var w = new PreferencesWindow { Owner = this };
        if (w.ShowDialog() == true && w.SettingsChanged)
        {
            ApplyEditorOptions();
            ApplyEditorPaneVisibility();
            ApplyPanelVisibility();
            RefreshFileBrowser();
        }
    }

    private async void OnCheckUpdate(object sender, ExecutedRoutedEventArgs e) =>
        await DoUpdateCheckAsync(silent: false);

    private async Task DoUpdateCheckAsync(bool silent)
    {
        try
        {
            bool available = await _updateService.CheckAsync();
            if (!available)
            {
                if (!silent) MessageBox.Show(this, "You are running the latest version.",
                    "Update", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var msg = $"A new version is available: {_updateService.LatestVersion}";
            if (!string.IsNullOrEmpty(_updateService.ReleaseUrl))
                msg += $"\n\nOpen the release page?\n{_updateService.ReleaseUrl}";
            var r = MessageBox.Show(this, msg, "Update Available",
                MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (r == MessageBoxResult.Yes && !string.IsNullOrEmpty(_updateService.ReleaseUrl))
                Process.Start(new ProcessStartInfo(_updateService.ReleaseUrl!) { UseShellExecute = true });
        }
        catch
        {
            if (!silent) MessageBox.Show(this, "Update check failed.",
                "Update", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnManagePlugins(object sender, ExecutedRoutedEventArgs e)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MarkdownViewer", "plugins");
        Directory.CreateDirectory(dir);
        var sb = new StringBuilder();
        sb.AppendLine("Plugins are loaded from:");
        sb.AppendLine(dir);
        sb.AppendLine();
        sb.AppendLine("Loaded plugins:");
        if (PluginHost.Loaded.Count == 0) sb.AppendLine("(none)");
        foreach (var p in PluginHost.Loaded)
            sb.AppendLine($"- {p.Name} {p.Version}");
        sb.AppendLine();
        sb.AppendLine("Drop a plugin DLL into the folder above and restart the app.");
        MessageBox.Show(this, sb.ToString(), "Plugins", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnAbout(object sender, ExecutedRoutedEventArgs e)
    {
        var w = new AboutWindow { Owner = this };
        w.ShowDialog();
    }

    // --- Window lifecycle --------------------------------------------------

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Prompt for any dirty documents.
        foreach (var doc in _docs.Documents.ToList())
        {
            if (!doc.IsDirty) continue;
            _docs.Activate(_docs.Documents.IndexOf(doc));
            var r = MessageBox.Show(this,
                $"\"{doc.Title}\" has unsaved changes. Save before closing?",
                "Close", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (r == MessageBoxResult.Cancel) { e.Cancel = true; return; }
            if (r == MessageBoxResult.Yes) { DoSave(saveAs: false); if (doc.IsDirty) { e.Cancel = true; return; } }
        }
        _docs.Dispose();
        _updateService.Dispose();
        TelemetryService.Log("app_exit");
    }

    // --- Helpers -----------------------------------------------------------

    private void UpdateTitle()
    {
        var doc = _docs.Active;
        Title = doc is null ? "Markdown Viewer" : $"{doc.Title}{(doc.IsDirty ? " •" : string.Empty)} — Markdown Viewer";
    }

    private void SetStatus(string text) => StatusText.Text = text;
}