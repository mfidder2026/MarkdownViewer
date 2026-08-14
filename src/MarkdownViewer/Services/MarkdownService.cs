using System;
using System.IO;
using System.Text;
using Markdig;
using Markdig.Syntax;

namespace MarkdownViewer.Services;

/// <summary>
/// Reads Markdown files and parses them into Markdig's <see cref="MarkdownDocument"/>
/// AST. The parser pipeline is created once (thread-safe, reusable) to avoid
/// per-file setup cost.
/// </summary>
internal static class MarkdownService
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

    /// <summary>
    /// Reads a text file, detecting encoding by BOM and falling back to strict
    /// UTF-8 then default ANSI. Returns the decoded text and the detected encoding.
    /// </summary>
    public static (string Text, Encoding Encoding) ReadFile(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return (Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), Encoding.UTF8);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return (Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), Encoding.Unicode);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return (Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), Encoding.BigEndianUnicode);
        }

        try
        {
            var strictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
            string decoded = strictUtf8.GetString(bytes);
            return (decoded, Encoding.UTF8);
        }
        catch (DecoderFallbackException)
        {
            return (Encoding.Default.GetString(bytes), Encoding.Default);
        }
    }

    /// <summary>Parse Markdown text into a Markdig AST.</summary>
    public static MarkdownDocument Parse(string text) =>
        (MarkdownDocument)Markdig.Markdown.Parse(text, Pipeline);
}