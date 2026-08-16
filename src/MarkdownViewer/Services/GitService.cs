using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MarkdownViewer.Services;

/// <summary>
/// Read-only Git integration. Runs <c>git</c> in the directory of the open
/// document and surfaces a simple status map (file path → status code).
/// No writes, no history rewriting, no network operations beyond what git
/// itself does. Falls back to "not a git repo" when git is absent.
/// </summary>
public static class GitService
{
    public sealed record GitStatus(string Path, char Code, string Description, bool IsStaged);

    /// <summary>True if <c>git</c> is on PATH and the directory is a repository.</summary>
    public static bool IsRepository(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return false;
        try
        {
            string output = RunGit(directory, "rev-parse", "--is-inside-work-tree");
            return output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static IReadOnlyList<GitStatus> GetStatus(string directory)
    {
        var result = new List<GitStatus>();
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return result;
        try
        {
            // Porcelain v1: XY filename
            string output = RunGit(directory, "status", "--porcelain=v1", "-z");
            if (string.IsNullOrEmpty(output)) return result;

            // -z uses NUL separators: each entry is "XY path\0".
            var parts = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.Length < 3) continue;
                char x = part[0];
                char y = part[1];
                string path = part[3..].Trim('"');
                // Use the unstaged code as primary, fall back to staged.
                char code = y == ' ' ? x : y;
                result.Add(new GitStatus(path, code, Describe(code), x != ' ' && y != ' '));
            }
        }
        catch
        {
            // Ignore — never crash on git failures.
        }
        return result;
    }

    public static string CurrentBranch(string directory)
    {
        try { return RunGit(directory, "rev-parse", "--abbrev-ref", "HEAD").Trim(); }
        catch { return "(no branch)"; }
    }

    private static string RunGit(string workingDir, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("git not found");
        string stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit(5000);
        if (p.ExitCode != 0)
        {
            string err = p.StandardError.ReadToEnd();
            throw new InvalidOperationException(err);
        }
        return stdout;
    }

    private static string Describe(char code) => code switch
    {
        'M' => "Modified",
        'A' => "Added",
        'D' => "Deleted",
        'R' => "Renamed",
        'C' => "Copied",
        'U' => "Unmerged",
        '?' => "Untracked",
        '!' => "Ignored",
        _ => "Unknown",
    };
}