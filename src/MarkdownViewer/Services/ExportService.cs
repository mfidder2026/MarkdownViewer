using System;
using System.IO;
using System.Text;
using Markdig;

namespace MarkdownViewer.Services;

/// <summary>
/// Export helpers. HTML export reuses the same Markdig pipeline as the viewer
/// (GitHub-flavoured output). PDF and printing are routed through WPF's
/// built-in print dialog elsewhere.
/// </summary>
public static class ExportService
{
    private static readonly MarkdownPipeline HtmlPipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    /// <summary>Convert Markdown text to a standalone HTML document string.</summary>
    public static string ToHtml(string markdown, string? title = null)
    {
        string body = Markdig.Markdown.ToHtml(markdown ?? string.Empty, HtmlPipeline);
        string safeTitle = System.Net.WebUtility.HtmlEncode(title ?? "Markdown Export");
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\"/>");
        sb.AppendLine($"<title>{safeTitle}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:-apple-system,Segoe UI,Roboto,sans-serif;max-width:820px;margin:2rem auto;padding:0 1rem;line-height:1.55;color:#1f2328}");
        sb.AppendLine("code,pre{font-family:Consolas,monospace;background:#f6f8fa;border-radius:4px}");
        sb.AppendLine("pre{padding:.8rem;overflow:auto;border:1px solid #d0d7de}");
        sb.AppendLine("blockquote{border-left:3px solid #d0d7de;margin:0;padding:.4rem 0 .4rem 1rem;color:#656d76;background:#f6f8fa}");
        sb.AppendLine("table{border-collapse:collapse;width:100%}th,td{border:1px solid #d0d7de;padding:.4rem .6rem}th{background:#f0f3f6}");
        sb.AppendLine("a{color:#0969ff}img{max-width:100%}hr{border:none;border-top:1px solid #d0d7de}");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.Append(body);
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    public static void ExportHtml(string markdown, string destPath, string? title = null)
    {
        File.WriteAllText(destPath, ToHtml(markdown, title), new UTF8Encoding(false));
    }
}