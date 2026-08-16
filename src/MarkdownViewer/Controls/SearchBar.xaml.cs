using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MarkdownViewer.Controls;

/// <summary>
/// In-document search bar. The host window wires the actual search logic by
/// subscribing to <see cref="QueryChanged"/> / <see cref="NavigateRequested"/>.
/// </summary>
public partial class SearchBar : UserControl
{
    public event EventHandler<string>? QueryChanged;
    public event EventHandler<SearchDirection>? NavigateRequested;

    public SearchBar()
    {
        InitializeComponent();
        QueryBox.TextChanged += (_, _) =>
        {
            QueryChanged?.Invoke(this, QueryBox.Text);
        };
    }

    public void Open()
    {
        Visibility = Visibility.Visible;
        QueryBox.Focus();
        QueryBox.SelectAll();
    }

    public void Close() => Visibility = Visibility.Collapsed;

    public void SetMatchCount(int count) =>
        MatchCount.Text = count > 0 ? $"{count} matches" : string.Empty;

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnPrev(object sender, RoutedEventArgs e) =>
        NavigateRequested?.Invoke(this, SearchDirection.Previous);

    private void OnNext(object sender, RoutedEventArgs e) =>
        NavigateRequested?.Invoke(this, SearchDirection.Next);

    private void OnQueryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateRequested?.Invoke(this,
                Keyboard.Modifiers == ModifierKeys.Shift
                    ? SearchDirection.Previous
                    : SearchDirection.Next);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}

public enum SearchDirection { Next, Previous }