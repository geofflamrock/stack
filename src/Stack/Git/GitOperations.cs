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
    public static void CreateNewBranch(string branchName, string sourceBranch, GitOperationSettings settings)
    {
        ExecuteGitCommand($"branch {branchName} {sourceBranch}", settings);
    }

    public static void PushNewBranch(string branchName, GitOperationSettings settings)
    {
        ExecuteGitCommand($"push -u origin {branchName}", settings);
    }

    public static void PushBranch(string branchName, GitOperationSettings settings)
    {
        ExecuteGitCommand($"push origin {branchName}", settings);
    }

    public static void ChangeBranch(string branchName, GitOperationSettings settings)
    {
        ExecuteGitCommand($"checkout {branchName}", settings);
    }

    public static void FetchBranches(string[] branches, GitOperationSettings settings)
    {
        ExecuteGitCommand($"fetch origin {string.Join(" ", branches)}", settings);
    }

    public static void PullBranch(string branchName, GitOperationSettings settings)
    {
        ExecuteGitCommand($"pull origin {branchName}", settings);
    }

    public static void UpdateBranch(string branchName, GitOperationSettings settings)
    {
        var currentBranch = GetCurrentBranch(settings);

        if (!currentBranch.Equals(branchName, StringComparison.OrdinalIgnoreCase))
        {
            ChangeBranch(branchName, settings);
        }

        PullBranch(branchName, settings);
    }

    public static void MergeFromLocalSourceBranch(string sourceBranchName, GitOperationSettings settings)
    {
        ExecuteGitCommand($"merge {sourceBranchName}", settings);
    }

    public static string GetCurrentBranch(GitOperationSettings settings)
    {
        return ExecuteGitCommandAndReturnOutput("branch --show-current", settings).Trim();
    }

    public static string GetDefaultBranch(GitOperationSettings settings)
    {
        return ExecuteGitCommandAndReturnOutput("symbolic-ref refs/remotes/origin/HEAD", settings).Trim().Replace("refs/remotes/origin/", "");
    }

    public static bool DoesRemoteBranchExist(string branchName, GitOperationSettings settings)
    {
        return ExecuteGitCommandAndReturnOutput($"ls-remote --heads origin {branchName}", settings).Trim().Length > 0;
    }

    public static string[] GetBranchesThatExistInRemote(string[] branches, GitOperationSettings settings)
    {
        var remoteBranches = ExecuteGitCommandAndReturnOutput($"ls-remote --heads origin {string.Join(" ", branches)}", settings).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        return branches.Where(b => remoteBranches.Any(rb => rb.EndsWith(b))).ToArray();
    }

    public static bool IsRemoteBranchFullyMerged(string branchName, string sourceBranchName, GitOperationSettings settings)
    {
        var remoteBranchesThatHaveBeenMerged = ExecuteGitCommandAndReturnOutput($"branch --remote --merged {sourceBranchName}", settings).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        return remoteBranchesThatHaveBeenMerged.Any(b => b.EndsWith(branchName));
    }

    public static string[] GetBranchesThatHaveBeenMerged(string[] branches, string sourceBranchName, GitOperationSettings settings)
    {
        var remoteBranchesThatHaveBeenMerged = ExecuteGitCommandAndReturnOutput($"branch --remote --merged {sourceBranchName}", settings).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        return branches.Where(b => remoteBranchesThatHaveBeenMerged.Any(rb => rb.EndsWith(b))).ToArray();
    }

    public static (int Ahead, int Behind) GetStatusOfRemoteBranch(string branchName, string sourceBranchName, GitOperationSettings settings)
    {
        var status = ExecuteGitCommandAndReturnOutput($"rev-list --left-right --count origin/{branchName}...origin/{sourceBranchName}", settings).Trim();
        var parts = status.Split('\t');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    public static string GetRemoteUri(GitOperationSettings settings)
    {
        return ExecuteGitCommandAndReturnOutput("remote get-url origin", settings).Trim();
    }

    public static string[] GetLocalBranchesOrderedByMostRecentCommitterDate(GitOperationSettings settings)
    {
        return ExecuteGitCommandAndReturnOutput("branch --format=%(refname:short) --sort=-committerdate", settings).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    private static string ExecuteGitCommandAndReturnOutput(string command, GitOperationSettings settings)
    {
        if (settings.Verbose)
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

        if (settings.Verbose && infoBuilder.Length > 0)
        {
            AnsiConsole.WriteLine(infoBuilder.ToString());
        }

        return infoBuilder.ToString();
    }

    private static void ExecuteGitCommand(string command, GitOperationSettings settings)
    {
        if (settings.Verbose)
            AnsiConsole.MarkupLine($"[grey]git {command}[/]");

        if (!settings.DryRun)
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
