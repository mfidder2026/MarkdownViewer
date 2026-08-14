using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MarkdownDocument = Markdig.Syntax.MarkdownDocument;
using HeadingBlock = Markdig.Syntax.HeadingBlock;
using ParagraphBlock = Markdig.Syntax.ParagraphBlock;
using ListBlock = Markdig.Syntax.ListBlock;
using ListItemBlock = Markdig.Syntax.ListItemBlock;
using QuoteBlock = Markdig.Syntax.QuoteBlock;
using FencedCodeBlock = Markdig.Syntax.FencedCodeBlock;
using CodeBlock = Markdig.Syntax.CodeBlock;
using ThematicBreakBlock = Markdig.Syntax.ThematicBreakBlock;
using ContainerBlock = Markdig.Syntax.ContainerBlock;
using HtmlBlock = Markdig.Syntax.HtmlBlock;
using LinkReferenceDefinition = Markdig.Syntax.LinkReferenceDefinition;
using ContainerInline = Markdig.Syntax.Inlines.ContainerInline;
using LiteralInline = Markdig.Syntax.Inlines.LiteralInline;
using EmphasisInline = Markdig.Syntax.Inlines.EmphasisInline;
using CodeInline = Markdig.Syntax.Inlines.CodeInline;
using LinkInline = Markdig.Syntax.Inlines.LinkInline;
using AutolinkInline = Markdig.Syntax.Inlines.AutolinkInline;
using LineBreakInline = Markdig.Syntax.Inlines.LineBreakInline;
using HtmlInline = Markdig.Syntax.Inlines.HtmlInline;
using MBlock = Markdig.Syntax.Block;
using MInline = Markdig.Syntax.Inlines.Inline;
using MTable = Markdig.Extensions.Tables.Table;
using MTableCell = Markdig.Extensions.Tables.TableCell;

namespace MarkdownViewer.Services;

/// <summary>
/// Walks a Markdig <see cref="MarkdownDocument"/> AST and produces a native WPF
/// <see cref="FlowDocument"/>. No HTML, no browser engine. Reflow on resize is
/// handled by WPF's FlowDocument layout — the AST is not re-parsed.
/// </summary>
internal static class FlowDocumentBuilder
{
    private const string BodyFontFamily = "Segoe UI";
    private const string CodeFontFamily = "Consolas";

    private static readonly Brush CodeBackground = Freeze(Color.FromRgb(0xF6, 0xF8, 0xFA));
    private static readonly Brush QuoteBarBrush = Freeze(Color.FromRgb(0xD0, 0xD7, 0xDE));
    private static readonly Brush RuleBrush = Freeze(Color.FromRgb(0xD0, 0xD7, 0xDE));
    private static readonly Brush LinkBrush = Freeze(Color.FromRgb(0x09, 0x69, 0xFF));
    private static readonly Brush TableHeaderBackground = Freeze(Color.FromRgb(0xF0, 0xF3, 0xF6));
    private static readonly Brush TableBorderBrush = Freeze(Color.FromRgb(0xD0, 0xD7, 0xDE));
    private static readonly Brush QuoteBackground = Freeze(Color.FromRgb(0xF6, 0xF8, 0xFA));

    private static Brush Freeze(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public static FlowDocument Build(MarkdownDocument document, string baseDir)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily(BodyFontFamily),
            FontSize = 15,
            PagePadding = new Thickness(0),
            ColumnWidth = double.PositiveInfinity,
            TextAlignment = TextAlignment.Left,
        };

        var ctx = new BuildContext { BaseDirectory = baseDir };

        foreach (MBlock block in document)
        {
            Block? built = BuildBlock(block, ctx);
            if (built is not null)
            {
                doc.Blocks.Add(built);
            }
        }

        return doc;
    }

    private sealed class BuildContext
    {
        public string BaseDirectory { get; init; } = string.Empty;
    }

    private static Block? BuildBlock(MBlock block, BuildContext ctx) => block switch
    {
        HeadingBlock h => BuildHeading(h),
        ParagraphBlock p => BuildParagraph(p, ctx),
        ListBlock l => BuildList(l, ctx),
        QuoteBlock q => BuildQuote(q, ctx),
        FencedCodeBlock fc => BuildCodeBlock(fc),
        CodeBlock c => BuildCodeBlock(c),
        ThematicBreakBlock => BuildThematicBreak(),
        MTable t => BuildTable(t, ctx),
        HtmlBlock => null,
        LinkReferenceDefinition => null,
        ContainerBlock cb => BuildContainer(cb, ctx),
        _ => BuildFallbackBlock(block),
    };

    private static Block BuildFallbackBlock(MBlock block)
    {
        var p = new Paragraph();
        p.Inlines.Add(new Run(block.ToString() ?? string.Empty) { Foreground = Brushes.Gray });
        return p;
    }

    private static Block? BuildContainer(ContainerBlock container, BuildContext ctx)
    {
        if (container.Count == 0) return null;
        var section = new Section();
        foreach (MBlock child in container)
        {
            Block? built = BuildBlock(child, ctx);
            if (built is not null) section.Blocks.Add(built);
        }
        return section;
    }

    private static Block BuildHeading(HeadingBlock h)
    {
        double size = h.Level switch
        {
            1 => 28, 2 => 24, 3 => 20, 4 => 17, 5 => 15, _ => 14,
        };
        bool bold = h.Level <= 4;

        var p = new Paragraph { Margin = new Thickness(0, h.Level <= 2 ? 18 : 12, 0, 6) };

        if (h.Inline is not null)
        {
            foreach (Inline inline in BuildInlines(h.Inline, null))
            {
                if (inline is Run r)
                {
                    r.FontSize = size;
                    if (bold) r.FontWeight = FontWeights.SemiBold;
                }
                p.Inlines.Add(inline);
            }
        }

        if (h.Level <= 2)
        {
            p.BorderBrush = RuleBrush;
            p.BorderThickness = new Thickness(0, 0, 0, 1);
            p.Padding = new Thickness(0, 0, 0, 4);
        }

        return p;
    }

    private static Block BuildParagraph(ParagraphBlock p, BuildContext ctx)
    {
        var para = new Paragraph { Margin = new Thickness(0, 0, 0, 10) };
        if (p.Inline is not null)
        {
            foreach (Inline inline in BuildInlines(p.Inline, ctx))
            {
                para.Inlines.Add(inline);
            }
        }
        return para;
    }

    private static Block BuildQuote(QuoteBlock q, BuildContext ctx)
    {
        var section = new Section
        {
            BorderBrush = QuoteBarBrush,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 4, 0, 4),
            Margin = new Thickness(0, 0, 0, 10),
            Background = QuoteBackground,
        };

        foreach (MBlock child in q)
        {
            Block? built = BuildBlock(child, ctx);
            if (built is not null) section.Blocks.Add(built);
        }

        return section;
    }

    private static Block BuildCodeBlock(CodeBlock c)
    {
        string text = c.Lines.ToString();
        if (text.EndsWith("\r\n", StringComparison.Ordinal)) text = text[..^2];
        else if (text.EndsWith('\n') || text.EndsWith('\r')) text = text[..^1];

        var lines = text.Replace("\r\n", "\n").Split('\n');

        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily(CodeFontFamily),
            FontSize = 13.5,
            Foreground = Brushes.Black,
        };

        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new Run(lines[i]));
        }

        var border = new Border
        {
            Background = CodeBackground,
            BorderBrush = TableBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 12),
            Child = textBlock,
        };

        return new BlockUIContainer { Child = border };
    }

    private static Block BuildThematicBreak()
    {
        return new Paragraph
        {
            Margin = new Thickness(0, 8, 0, 12),
            BorderBrush = RuleBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
        };
    }

    private static Block BuildList(ListBlock list, BuildContext ctx)
    {
        bool ordered = list.IsOrdered;
        int index = 0;
        var result = new Section { Margin = new Thickness(0, 0, 0, 10) };

        foreach (ListItemBlock item in list)
        {
            index++;
            string marker = ordered ? $"{index}." : "•";
            var itemPara = new Paragraph { Margin = new Thickness(20, 0, 0, 2) };
            itemPara.Inlines.Add(new Run(marker + "  ")
            {
                Foreground = Brushes.Gray,
                FontFamily = new FontFamily(CodeFontFamily),
            });

            bool itemParaEmitted = false;

            foreach (MBlock child in item)
            {
                if (child is ParagraphBlock pb && pb.Inline is not null)
                {
                    foreach (Inline inline in BuildInlines(pb.Inline, ctx))
                    {
                        itemPara.Inlines.Add(inline);
                    }
                }
                else if (child is ListBlock nestedList)
                {
                    if (itemPara.Inlines.Count > 0)
                    {
                        result.Blocks.Add(itemPara);
                        itemParaEmitted = true;
                    }
                    result.Blocks.Add(BuildList(nestedList, ctx));
                    itemPara = new Paragraph { Margin = new Thickness(20, 0, 0, 2) };
                }
                else
                {
                    Block? built = BuildBlock(child, ctx);
                    if (built is not null)
                    {
                        if (itemPara.Inlines.Count > 0)
                        {
                            result.Blocks.Add(itemPara);
                            itemParaEmitted = true;
                        }
                        result.Blocks.Add(built);
                        itemPara = new Paragraph { Margin = new Thickness(20, 0, 0, 2) };
                    }
                }
            }

            if (!itemParaEmitted && itemPara.Inlines.Count > 0)
            {
                result.Blocks.Add(itemPara);
            }
        }

        return result;
    }

    private static Block BuildTable(MTable table, BuildContext ctx)
    {
        var flowTable = new Table
        {
            Margin = new Thickness(0, 0, 0, 12),
            BorderBrush = TableBorderBrush,
            BorderThickness = new Thickness(1),
            CellSpacing = 0,
        };

        int colCount = table.Count > 0 ? ((ContainerBlock)table[0]).Count : 0;
        for (int i = 0; i < colCount; i++)
        {
            flowTable.Columns.Add(new TableColumn());
        }

        var rg = new TableRowGroup();
        flowTable.RowGroups.Add(rg);

        for (int r = 0; r < table.Count; r++)
        {
            var row = (ContainerBlock)table[r];
            var tr = new TableRow();
            if (r == 0)
            {
                tr.Background = TableHeaderBackground;
                tr.FontWeight = FontWeights.SemiBold;
            }

            foreach (MTableCell cell in row)
            {
                var tc = new TableCell
                {
                    BorderBrush = TableBorderBrush,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 4, 8, 4),
                };

                var p = new Paragraph { Margin = new Thickness(0) };
                foreach (MBlock cellBlock in cell)
                {
                    if (cellBlock is ParagraphBlock pb && pb.Inline is not null)
                    {
                        foreach (Inline inline in BuildInlines(pb.Inline, ctx))
                        {
                            p.Inlines.Add(inline);
                        }
                    }
                }
                tc.Blocks.Add(p);
                tr.Cells.Add(tc);
            }

            rg.Rows.Add(tr);
        }

        return flowTable;
    }

    private static IEnumerable<Inline> BuildInlines(ContainerInline? container, BuildContext? ctx)
    {
        if (container is null) yield break;
        foreach (Inline inline in WalkInlines(container, ctx))
        {
            yield return inline;
        }
    }

    private static IEnumerable<Inline> WalkInlines(ContainerInline container, BuildContext? ctx)
    {
        foreach (MInline inline in container)
        {
            foreach (Inline built in BuildSingleInline(inline, ctx))
            {
                yield return built;
            }
        }
    }

    private static IEnumerable<Inline> BuildSingleInline(MInline inline, BuildContext? ctx)
    {
        switch (inline)
        {
            case LiteralInline literal:
                yield return new Run(literal.Content.ToString());
                break;

            case EmphasisInline emphasis:
            {
                var span = new Span();
                char d = emphasis.DelimiterChar;
                int count = emphasis.DelimiterCount;
                if (count == 2 && (d == '*' || d == '_'))
                {
                    span.FontWeight = FontWeights.Bold;
                }
                else if (d == '~')
                {
                    span.TextDecorations = TextDecorations.Strikethrough;
                }
                else
                {
                    span.FontStyle = FontStyles.Italic;
                }

                foreach (Inline child in WalkInlines(emphasis, ctx))
                {
                    span.Inlines.Add(child);
                }
                yield return span;
                break;
            }

            case CodeInline code:
            {
                var span = new Span
                {
                    Background = CodeBackground,
                    FontFamily = new FontFamily(CodeFontFamily),
                    FontSize = 13.5,
                };
                span.Inlines.Add(new Run(code.Content));
                yield return span;
                break;
            }

            case LinkInline link:
            {
                if (link.IsImage)
                {
                    Inline? imgInline = BuildImage(link, ctx);
                    if (imgInline is not null) yield return imgInline;
                }
                else
                {
                    var hyper = new Hyperlink
                    {
                        Foreground = LinkBrush,
                        TextDecorations = TextDecorations.Underline,
                        NavigateUri = TryUri(link.Url),
                    };
                    hyper.RequestNavigate += OnNavigate;

                    if (!string.IsNullOrEmpty(link.Url))
                    {
                        hyper.ToolTip = link.Url;
                    }

                    foreach (Inline child in WalkInlines(link, ctx))
                    {
                        hyper.Inlines.Add(child);
                    }

                    if (hyper.Inlines.Count == 0)
                    {
                        hyper.Inlines.Add(new Run(link.Url ?? string.Empty));
                    }

                    yield return hyper;
                }
                break;
            }

            case AutolinkInline autolink:
            {
                var hyper = new Hyperlink
                {
                    Foreground = LinkBrush,
                    NavigateUri = TryUri(autolink.Url),
                };
                hyper.RequestNavigate += OnNavigate;
                hyper.Inlines.Add(new Run(autolink.Url));
                yield return hyper;
                break;
            }

            case LineBreakInline:
                yield return new LineBreak();
                break;

            case HtmlInline html:
                yield return new Run(html.Tag) { Foreground = Brushes.Gray };
                break;

            case ContainerInline childContainer:
            {
                foreach (Inline child in WalkInlines(childContainer, ctx))
                {
                    yield return child;
                }
                break;
            }

            default:
                yield return new Run(inline.ToString() ?? string.Empty);
                break;
        }
    }

    private static Inline? BuildImage(LinkInline link, BuildContext? ctx)
    {
        string? url = link.Url;
        if (string.IsNullOrEmpty(url))
        {
            return new Run("[image]") { Foreground = Brushes.Gray };
        }

        string resolved = ResolveImagePath(url, ctx?.BaseDirectory ?? string.Empty);
        if (resolved.Length == 0)
        {
            return new Run($"[image not found: {url}]") { Foreground = Brushes.Gray };
        }

        ImageSource? source = TryLoadImage(resolved);
        if (source is null)
        {
            return new Run($"[image not found: {url}]") { Foreground = Brushes.Gray };
        }

        var image = new Image { MaxWidth = 800, Stretch = Stretch.Uniform, Source = source };

        string alt = ExtractAltText(link);
        if (!string.IsNullOrEmpty(alt))
        {
            image.ToolTip = alt;
        }

        return new InlineUIContainer { Child = image };
    }

    private static ImageSource? TryLoadImage(string resolved)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(resolved, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractAltText(LinkInline link)
    {
        var sb = new StringBuilder();
        foreach (MInline c in link)
        {
            if (c is LiteralInline lit)
            {
                sb.Append(lit.Content.ToString());
            }
        }
        return sb.ToString();
    }

    private static string ResolveImagePath(string url, string baseDir)
    {
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        try
        {
            string path = Path.IsPathRooted(url) ? url : Path.Combine(baseDir, url);
            return File.Exists(path) ? path : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static Uri? TryUri(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        return Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out Uri? u) ? u : null;
    }

    private static void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true,
            };
            Process.Start(psi);
        }
        catch
        {
            // Ignore navigation failures silently — do not crash.
        }
        e.Handled = true;
    }
}