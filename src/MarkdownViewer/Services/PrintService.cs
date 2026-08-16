using System;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace MarkdownViewer.Services;

/// <summary>
/// Printing and PDF export. WPF has no built-in "save to PDF" API, so PDF is
/// achieved by routing the FlowDocument through the print dialog and letting
/// the user pick the "Microsoft Print to PDF" virtual printer. This avoids a
/// heavyweight PDF dependency while still producing a real PDF.
/// </summary>
public static class PrintService
{
    /// <summary>
    /// Shows the print dialog and prints the supplied FlowDocument.
    /// </summary>
    public static void Print(FlowDocument document, string description)
    {
        try
        {
            var dlg = new System.Windows.Controls.PrintDialog();
            if (dlg.ShowDialog() != true) return;

            // Clone so print-specific page padding doesn't mutate the on-screen doc.
            var clone = CloneDocument(document);
            clone.PagePadding = new Thickness(48);
            clone.ColumnWidth = double.PositiveInfinity;

            var paginator = ((IDocumentPaginatorSource)clone).DocumentPaginator;
            paginator.PageSize = new Size(dlg.PrintableAreaWidth, dlg.PrintableAreaHeight);
            dlg.PrintDocument(paginator, description);
        }
        catch
        {
            MessageBox.Show("Printing failed. Check that a printer is installed.",
                "Print", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Exports the document to PDF by invoking the print dialog — the user
    /// selects "Microsoft Print to PDF" as the target. A pure-code PDF writer
    /// would require a large dependency, which the prompt discourages.
    /// </summary>
    public static void ExportPdf(FlowDocument document, string description)
    {
        Print(document, description);
    }

    private static FlowDocument CloneDocument(FlowDocument src)
    {
        using var ms = new MemoryStream();
        var source = new TextRange(src.ContentStart, src.ContentEnd);
        source.Save(ms, DataFormats.Xaml);
        var clone = new FlowDocument();
        var range = new TextRange(clone.ContentStart, clone.ContentEnd);
        ms.Position = 0;
        range.Load(ms, DataFormats.Xaml);
        return clone;
    }
}