using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace MarkdownViewer.Windows;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var v = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.2.0";
        VersionText.Text = $"Version {v}";
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnWebsiteNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch
        {
            // If we can't open the browser, just let the link do nothing
        }
    }
}