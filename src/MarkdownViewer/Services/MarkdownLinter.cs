using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MarkdownViewer.Services;

/// <summary>
/// Lightweight Markdown linter. No external dependency — a handful of cheap
/// regex-based rules that flag common mistakes without a full parser. Output
/// is shown in the lint panel and never blocks editing.
/// </summary>
public sealed record LintIssue(int Line, int Column, string RuleId, LintIssue.SeverityKind Level, string Message)
{
    public enum SeverityKind { Info, Warning, Error }
}

public static class MarkdownLinter
{
    public static IReadOnlyList<LintIssue> Lint(string text)
    {
        var issues = new List<LintIssue>();
        if (string.IsNullOrEmpty(text)) return issues;

        var lines = text.Replace("\r\n", "\n").Split('\n');
        bool inFencedCode = false;
        string fenceMarker = "";

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            int lineNo = i + 1;

            var fence = FenceRegex().Match(line);
            if (fence.Success)
            {
                if (!inFencedCode)
                {
                    inFencedCode = true;
                    fenceMarker = fence.Groups[1].Value;
                    if (fence.Groups[2].Length < 3)
                        issues.Add(new LintIssue(lineNo, 1, "MD098", LintIssue.SeverityKind.Warning,
                            "Fenced code block should use at least three backticks."));
                }
                else if (fence.Groups[1].Value == fenceMarker)
                {
                    inFencedCode = false;
                    fenceMarker = "";
                }
                continue;
            }

            if (inFencedCode) continue;

            if (line.Length > 0 && line[^1] is ' ' or '\t')
                issues.Add(new LintIssue(lineNo, line.Length, "MD009", LintIssue.SeverityKind.Warning,
                    "Trailing whitespace."));

            int tabIdx = line.IndexOf('\t');
            if (tabIdx >= 0)
                issues.Add(new LintIssue(lineNo, tabIdx + 1, "MD010", LintIssue.SeverityKind.Info,
                    "Hard tab — consider using spaces."));

            var heading = HeadingNoSpaceRegex().Match(line);
            if (heading.Success)
                issues.Add(new LintIssue(lineNo, heading.Length, "MD018", LintIssue.SeverityKind.Warning,
                    "Heading should have a space after the # characters."));

            if (line.Length == 0 && i > 0 && lines[i - 1].Length == 0
                && i > 1 && lines[i - 2].Length == 0)
                issues.Add(new LintIssue(lineNo, 1, "MD012", LintIssue.SeverityKind.Info,
                    "Multiple consecutive blank lines."));

            var bareUrl = BareUrlRegex().Match(line);
            if (bareUrl.Success)
                issues.Add(new LintIssue(lineNo, bareUrl.Index + 1, "MD034", LintIssue.SeverityKind.Info,
                    "Bare URL — wrap in angle brackets for an autolink."));

            var headingEnd = HeadingEndPunctRegex().Match(line);
            if (headingEnd.Success)
                issues.Add(new LintIssue(lineNo, line.Length, "MD026", LintIssue.SeverityKind.Info,
                    "Heading ends with punctuation."));
        }

        if (inFencedCode)
            issues.Add(new LintIssue(lines.Length, 1, "MD098", LintIssue.SeverityKind.Error,
                "Fenced code block is never closed."));

        return issues;
    }

    private static Regex FenceRegex() => _fence ??= new Regex(@"^(\s*)(`{3,}|~{3,})", RegexOptions.Compiled);
    private static Regex? _fence;
    private static Regex HeadingNoSpaceRegex() => _headingNoSpace ??= new Regex(@"^#{1,6}[^\s#]", RegexOptions.Compiled);
    private static Regex? _headingNoSpace;
    private static Regex BareUrlRegex() => _bareUrl ??= new Regex(@"(?<![<\w])(https?://\S+)", RegexOptions.Compiled);
    private static Regex? _bareUrl;
    private static Regex HeadingEndPunctRegex() => _headingEnd ??= new Regex(@"^#{1,6}\s+.*[.!?:;,]$");
    private static Regex? _headingEnd;
}