using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using MarkdownViewer.Services;
using Microsoft.Win32;

namespace MarkdownViewer;

/// <summary>
/// Main application window. Owns the menu, empty state, and FlowDocument viewer.
/// Commands are bound directly here (no MVVM framework) to keep things light.
/// </summary>
public partial class MainWindow : Window
{
    private string? _currentFilePath;

    public static readonly RoutedCommand OpenCommand = new();
    public static readonly RoutedCommand CloseCommand = new();
    public static readonly RoutedCommand AboutCommand = new();

    public MainWindow()
    {
        InitializeComponent();

        CommandBindings.Add(new CommandBinding(OpenCommand, OnOpen));
        CommandBindings.Add(new CommandBinding(CloseCommand, OnClose, CanClose));
        CommandBindings.Add(new CommandBinding(AboutCommand, OnAbout));

        DataContext = this;
    }

    /// <summary>Opens and renders a Markdown file by path (used by App for CLI args).</summary>
    public void OpenFilePath(string path)
    {
        try
        {
            var (text, _) = MarkdownService.ReadFile(path);
            var document = MarkdownService.Parse(text);
            var baseDir = Path.GetDirectoryName(path) ?? string.Empty;
            DocViewer.Document = FlowDocumentBuilder.Build(document, baseDir);
            _currentFilePath = path;
            UpdateTitle();
            EmptyState.Visibility = Visibility.Collapsed;
            DocViewer.Visibility = Visibility.Visible;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SystemException)
        {
            MessageBox.Show(this,
                $"Could not open the file:\n{path}\n\n{ex.Message}",
                "Open File",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
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
        };

        if (dlg.ShowDialog(this) == true)
        {
            OpenFilePath(dlg.FileName);
        }
    }

    private void OnClose(object sender, ExecutedRoutedEventArgs e) => DoClose();

    private void DoClose()
    {
        _currentFilePath = null;
        DocViewer.Document = null;
        DocViewer.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Visible;
        UpdateTitle();
    }

    private void CanClose(object sender, CanExecuteRoutedEventArgs e) =>
        e.CanExecute = _currentFilePath is not null;

    private void OnAbout(object sender, ExecutedRoutedEventArgs e)
    {
        var version = Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(3) ?? "0.1.0";
        MessageBox.Show(this,
            $"Markdown Viewer\nVersion {version}\n\nFast lightweight Markdown viewer for Windows.",
            "About",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void UpdateTitle()
    {
        if (_currentFilePath is not null)
        {
            Title = $"{Path.GetFileName(_currentFilePath)} — Markdown Viewer";
        }
        else
        {
            Title = "Markdown Viewer";
        }
    }
}