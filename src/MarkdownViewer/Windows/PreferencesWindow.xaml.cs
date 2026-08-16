using System;
using System.Windows;
using System.Windows.Controls;
using MarkdownViewer.Services;

namespace MarkdownViewer.Windows;

public partial class PreferencesWindow : Window
{
    public bool SettingsChanged { get; private set; }

    public PreferencesWindow()
    {
        InitializeComponent();

        foreach (var t in ThemeService.AvailableThemes) ThemeBox.Items.Add(t);
        ThemeBox.SelectedItem = ThemeService.CurrentThemeName;

        var s = SettingsService.Current;
        WordWrapBox.IsChecked = s.WordWrap;
        LineNumbersBox.IsChecked = s.ShowLineNumbers;
        SpellCheckBox.IsChecked = s.SpellCheck;
        EditorPaneBox.IsChecked = s.ShowEditorPane;
        AutosaveBox.IsChecked = s.AutosaveEnabled;
        AutosaveIntervalBox.Text = s.AutosaveIntervalSeconds.ToString();
        FileBrowserBox.IsChecked = s.ShowFileBrowser;
        LintPanelBox.IsChecked = s.ShowLintPanel;
        UpdateCheckBox.IsChecked = s.UpdateCheckEnabled;
        TelemetryBox.IsChecked = s.TelemetryEnabled;
        FontFamilyBox.Text = s.FontFamily;
        FontSizeBox.Text = s.FontSize.ToString();
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        // Live preview: apply immediately so the user sees it.
        if (ThemeBox.SelectedItem is string theme)
        {
            ThemeService.Apply(theme);
            SettingsChanged = true;
        }
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var s = SettingsService.Current;
        s.WordWrap = WordWrapBox.IsChecked == true;
        s.ShowLineNumbers = LineNumbersBox.IsChecked == true;
        s.SpellCheck = SpellCheckBox.IsChecked == true;
        s.ShowEditorPane = EditorPaneBox.IsChecked == true;
        s.AutosaveEnabled = AutosaveBox.IsChecked == true;
        if (int.TryParse(AutosaveIntervalBox.Text, out int interval) && interval > 0)
            s.AutosaveIntervalSeconds = interval;
        s.ShowFileBrowser = FileBrowserBox.IsChecked == true;
        s.ShowLintPanel = LintPanelBox.IsChecked == true;
        s.UpdateCheckEnabled = UpdateCheckBox.IsChecked == true;
        bool newTelemetry = TelemetryBox.IsChecked == true;
        if (newTelemetry != s.TelemetryEnabled) TelemetryService.Toggle(newTelemetry);
        s.FontFamily = FontFamilyBox.Text;
        if (double.TryParse(FontSizeBox.Text, out double fs) && fs > 0)
            s.FontSize = fs;
        SettingsService.Save();
        SettingsChanged = true;
        DialogResult = true;
    }
}