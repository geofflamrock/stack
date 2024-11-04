using System.Text;
using Octopus.Shellfish;
using Spectre.Console;

namespace Stack.Git;

internal record GitOperationSettings(bool DryRun, bool Verbose)
{
    public static GitOperationSettings Default => new GitOperationSettings(false, false);
}

internal static class GitOperations
{
    public static void CreateNewBranch(string branchName, string sourceBranch, GitOperationSettings? settings = null)
    {
        ExecuteGitCommand($"branch {branchName} {sourceBranch}");
    }

    public static void PushNewBranch(string branchName, GitOperationSettings? settings = null)
    {
        ExecuteGitCommand($"push -u origin {branchName}");
    }

    public static void PushBranch(string branchName, GitOperationSettings? settings = null)
    {
        ExecuteGitCommand($"push origin {branchName}", settings);
    }

    public static void ChangeBranch(string branchName, GitOperationSettings? settings = null)
    {
        ExecuteGitCommand($"checkout {branchName}", settings);
    }

    public static void FetchBranch(string branchName, GitOperationSettings? settings = null)
    {
        var currentBranch = GetCurrentBranch();

        if (currentBranch.Equals(branchName, StringComparison.OrdinalIgnoreCase))
        {
            ExecuteGitCommand($"fetch origin {branchName}", settings);
        }
        else
        {
            ExecuteGitCommand($"fetch origin {branchName}:{branchName}", settings);
        }
    }

    public static void FetchBranches(string[] branches, GitOperationSettings? settings = null)
    {
        ExecuteGitCommand($"fetch origin {string.Join(" ", branches)}", settings);
    }

    public static void PullBranch(string branchName, GitOperationSettings? settings = null)
    {
        ExecuteGitCommand($"pull origin {branchName}", settings);
    }

    public static void UpdateBranch(string branchName, GitOperationSettings? settings = null)
    {
        var currentBranch = GetCurrentBranch();

        if (!currentBranch.Equals(branchName, StringComparison.OrdinalIgnoreCase))
        {
            ChangeBranch(branchName, settings);
        }

        PullBranch(branchName, settings);
    }

    public static void MergeFromRemoteSourceBranch(string sourceBranchName, GitOperationSettings? settings = null)
    {
        ExecuteGitCommand($"merge origin/{sourceBranchName}", settings);
    }

    public static void MergeFromLocalSourceBranch(string sourceBranchName, GitOperationSettings? settings = null)
    {
        ExecuteGitCommand($"merge {sourceBranchName}", settings);
    }

    public static string GetCurrentBranch(GitOperationSettings? settings = null)
    {
        return ExecuteGitCommandAndReturnOutput("branch --show-current", settings).Trim();
    }

    public static string GetDefaultBranch(GitOperationSettings? settings = null)
    {
        return ExecuteGitCommandAndReturnOutput("symbolic-ref refs/remotes/origin/HEAD", settings).Trim().Replace("refs/remotes/origin/", "");
    }

    public static bool DoesRemoteBranchExist(string branchName, GitOperationSettings? settings = null)
    {
        return ExecuteGitCommandAndReturnOutput($"ls-remote --heads origin {branchName}", settings).Trim().Length > 0;
    }

    public static string[] GetBranchesThatExistInRemote(string[] branches, GitOperationSettings? settings = null)
    {
        var remoteBranches = ExecuteGitCommandAndReturnOutput($"ls-remote --heads origin {string.Join(" ", branches)}", settings).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        return branches.Where(b => remoteBranches.Any(rb => rb.EndsWith(b))).ToArray();
    }

    public static bool IsRemoteBranchFullyMerged(string branchName, string sourceBranchName, GitOperationSettings? settings = null)
    {
        var remoteBranchesThatHaveBeenMerged = ExecuteGitCommandAndReturnOutput($"branch --remote --merged {sourceBranchName}", settings).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        return remoteBranchesThatHaveBeenMerged.Any(b => b.EndsWith(branchName));
    }

    public static string[] GetBranchesThatHaveBeenMerged(string[] branches, string sourceBranchName, GitOperationSettings? settings = null)
    {
        var remoteBranchesThatHaveBeenMerged = ExecuteGitCommandAndReturnOutput($"branch --remote --merged {sourceBranchName}", settings).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        return branches.Where(b => remoteBranchesThatHaveBeenMerged.Any(rb => rb.EndsWith(b))).ToArray();
    }

    public static (int Ahead, int Behind) GetStatusOfBranch(string branchName, string sourceBranchName, GitOperationSettings? settings = null)
    {
        var status = ExecuteGitCommandAndReturnOutput($"rev-list --left-right --count {branchName}...{sourceBranchName}", settings).Trim();
        var parts = status.Split('\t');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    public static (int Ahead, int Behind) GetStatusOfRemoteBranch(string branchName, string sourceBranchName, GitOperationSettings? settings = null)
    {
        var status = ExecuteGitCommandAndReturnOutput($"rev-list --left-right --count origin/{branchName}...origin/{sourceBranchName}", settings).Trim();
        var parts = status.Split('\t');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    public static string GetRemoteUri(GitOperationSettings? settings = null)
    {
        return ExecuteGitCommandAndReturnOutput("remote get-url origin", settings).Trim();
    }

    public static string[] GetLocalBranchesOrderedByMostRecentCommitterDate(GitOperationSettings? settings = null)
    {
        return ExecuteGitCommandAndReturnOutput("branch --format=%(refname:short) --sort=-committerdate", settings).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    private static string ExecuteGitCommandAndReturnOutput(string command, GitOperationSettings? settings = null)
    {
        var settingsOrDefault = settings ?? GitOperationSettings.Default;

        if (settingsOrDefault.Verbose)
            AnsiConsole.MarkupLine($"[grey]git {command}[/]");

        var infoBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        var result = ShellExecutor.ExecuteCommand(
            "git",
            command,
            ".",
            (_) => { },
            (info) => infoBuilder.AppendLine(info),
            (error) => errorBuilder.AppendLine(error));

        if (result != 0)
        {
            AnsiConsole.MarkupLine($"[red]{errorBuilder}[/]");
            throw new Exception("Failed to execute git command.");
        }

        if (settingsOrDefault.Verbose && infoBuilder.Length > 0)
        {
            AnsiConsole.WriteLine(infoBuilder.ToString());
        }

        return infoBuilder.ToString();
    }

    private static void ExecuteGitCommand(string command, GitOperationSettings? settings = null)
    {
        var settingsOrDefault = settings ?? GitOperationSettings.Default;

        if (settingsOrDefault.Verbose)
            AnsiConsole.MarkupLine($"[grey]git {command}[/]");

        if (!settingsOrDefault.DryRun)
        {
            ExecuteGitCommandInternal(command);
        }
    }

    private static void ExecuteGitCommandInternal(string command)
    {
        var infoBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        var result = ShellExecutor.ExecuteCommand(
            "git",
            command,
            ".",
            (_) => { },
            (info) => infoBuilder.AppendLine(info),
            (error) => errorBuilder.AppendLine(error));

        if (result != 0)
        {
            AnsiConsole.MarkupLine($"[red]{errorBuilder}[/]");
            throw new Exception("Failed to execute git command.");
        }
        else
        {
            if (infoBuilder.Length > 0)
            {
                AnsiConsole.WriteLine(infoBuilder.ToString());
            }
        }
    }
}
