using System.Diagnostics;
using System.IO;

namespace ClaudeWatcher.Helpers;

public static class GitInfoHelper
{
    public static string GetDefaultTitle(string workingDirectory)
    {
        string folder = Path.GetFileName(workingDirectory.TrimEnd('/', '\\')) ?? "Unknown";

        try
        {
            string? branch = RunGit(workingDirectory, "branch --show-current");
            if (string.IsNullOrEmpty(branch)) return folder;

            // Detect worktree
            string? gitDir = RunGit(workingDirectory, "rev-parse --git-dir");
            string? gitCommon = RunGit(workingDirectory, "rev-parse --git-common-dir");

            if (!string.IsNullOrEmpty(gitDir) && !string.IsNullOrEmpty(gitCommon))
            {
                string absGitDir = Path.GetFullPath(gitDir, workingDirectory);
                string absGitCommon = Path.GetFullPath(gitCommon, workingDirectory);
                if (!string.Equals(absGitDir, absGitCommon, StringComparison.OrdinalIgnoreCase))
                {
                    // It's a worktree
                    string wtName = Path.GetFileName(workingDirectory.TrimEnd('/', '\\'));
                    return $"{folder} - {branch} (worktree)";
                }
            }

            return $"{folder} - {branch}";
        }
        catch
        {
            return folder;
        }
    }

    private static string? RunGit(string workingDirectory, string args)
    {
        try
        {
            var psi = new ProcessStartInfo("git", $"-C \"{workingDirectory}\" {args}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            string output = proc?.StandardOutput.ReadToEnd().Trim() ?? "";
            proc?.WaitForExit(2000);
            return string.IsNullOrEmpty(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }
}
