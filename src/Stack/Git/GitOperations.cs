using System.Text;
using Octopus.Shellfish;
using Spectre.Console;

namespace Stack.Git;

public record GitOperationSettings(bool DryRun, bool Verbose, string? WorkingDirectory)
{
    public static GitOperationSettings Default => new(false, false, null);
}



public interface IGitOperations
{
    void CreateNewBranch(string branchName, string sourceBranch, GitOperationSettings settings);
    void PushNewBranch(string branchName, GitOperationSettings settings);
    void PushBranch(string branchName, GitOperationSettings settings);
    void ChangeBranch(string branchName, GitOperationSettings settings);
    void FetchBranches(string[] branches, GitOperationSettings settings);
    void PullBranch(string branchName, GitOperationSettings settings);
    void UpdateBranch(string branchName, GitOperationSettings settings);
    void MergeFromLocalSourceBranch(string sourceBranchName, GitOperationSettings settings);
    string GetCurrentBranch(GitOperationSettings settings);
    string GetDefaultBranch(GitOperationSettings settings);
    bool DoesRemoteBranchExist(string branchName, GitOperationSettings settings);
    string[] GetBranchesThatExistLocally(string[] branches, GitOperationSettings settings);
    string[] GetBranchesThatExistInRemote(string[] branches, GitOperationSettings settings);
    bool IsRemoteBranchFullyMerged(string branchName, string sourceBranchName, GitOperationSettings settings);
    string[] GetBranchesThatHaveBeenMerged(string[] branches, string sourceBranchName, GitOperationSettings settings);
    (int Ahead, int Behind) GetStatusOfRemoteBranch(string branchName, string sourceBranchName, GitOperationSettings settings);
    string GetRemoteUri(GitOperationSettings settings);
    string[] GetLocalBranchesOrderedByMostRecentCommitterDate(GitOperationSettings settings);
}

public class GitOperations(IAnsiConsole console) : IGitOperations
{
    public void CreateNewBranch(string branchName, string sourceBranch, GitOperationSettings settings)
    {
        ExecuteGitCommand($"branch {branchName} {sourceBranch}", settings);
    }

    public void PushNewBranch(string branchName, GitOperationSettings settings)
    {
        ExecuteGitCommand($"push -u origin {branchName}", settings);
    }

    public void PushBranch(string branchName, GitOperationSettings settings)
    {
        ExecuteGitCommand($"push origin {branchName}", settings);
    }

    public void ChangeBranch(string branchName, GitOperationSettings settings)
    {
        ExecuteGitCommand($"checkout {branchName}", settings);
    }

    public void FetchBranches(string[] branches, GitOperationSettings settings)
    {
        ExecuteGitCommand($"fetch origin {string.Join(" ", branches)}", settings);
    }

    public void PullBranch(string branchName, GitOperationSettings settings)
    {
        ExecuteGitCommand($"pull origin {branchName}", settings);
    }

    public void UpdateBranch(string branchName, GitOperationSettings settings)
    {
        var currentBranch = GetCurrentBranch(settings);

        if (!currentBranch.Equals(branchName, StringComparison.OrdinalIgnoreCase))
        {
            ChangeBranch(branchName, settings);
        }

        PullBranch(branchName, settings);
    }

    public void MergeFromLocalSourceBranch(string sourceBranchName, GitOperationSettings settings)
    {
        ExecuteGitCommand($"merge {sourceBranchName}", settings);
    }

    public string GetCurrentBranch(GitOperationSettings settings)
    {
        return ExecuteGitCommandAndReturnOutput("branch --show-current", settings).Trim();
    }

    public string GetDefaultBranch(GitOperationSettings settings)
    {
        return ExecuteGitCommandAndReturnOutput("symbolic-ref refs/remotes/origin/HEAD", settings).Trim().Replace("refs/remotes/origin/", "");
    }

    public bool DoesRemoteBranchExist(string branchName, GitOperationSettings settings)
    {
        return ExecuteGitCommandAndReturnOutput($"ls-remote --heads origin {branchName}", settings).Trim().Length > 0;
    }

    public string[] GetBranchesThatExistLocally(string[] branches, GitOperationSettings settings)
    {
        var localBranches = ExecuteGitCommandAndReturnOutput("branch --format=%(refname:short)", settings).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        return branches.Where(b => localBranches.Any(lb => lb.Equals(b, StringComparison.OrdinalIgnoreCase))).ToArray();
    }

    public string[] GetBranchesThatExistInRemote(string[] branches, GitOperationSettings settings)
    {
        var remoteBranches = ExecuteGitCommandAndReturnOutput($"ls-remote --heads origin {string.Join(" ", branches)}", settings).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        return branches.Where(b => remoteBranches.Any(rb => rb.EndsWith(b))).ToArray();
    }

    public bool IsRemoteBranchFullyMerged(string branchName, string sourceBranchName, GitOperationSettings settings)
    {
        var remoteBranchesThatHaveBeenMerged = ExecuteGitCommandAndReturnOutput($"branch --remote --merged {sourceBranchName}", settings).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        return remoteBranchesThatHaveBeenMerged.Any(b => b.EndsWith(branchName));
    }

    public string[] GetBranchesThatHaveBeenMerged(string[] branches, string sourceBranchName, GitOperationSettings settings)
    {
        var remoteBranchesThatHaveBeenMerged = ExecuteGitCommandAndReturnOutput($"branch --remote --merged {sourceBranchName}", settings).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        return branches.Where(b => remoteBranchesThatHaveBeenMerged.Any(rb => rb.EndsWith(b))).ToArray();
    }

    public (int Ahead, int Behind) GetStatusOfRemoteBranch(string branchName, string sourceBranchName, GitOperationSettings settings)
    {
        var status = ExecuteGitCommandAndReturnOutput($"rev-list --left-right --count origin/{branchName}...origin/{sourceBranchName}", settings).Trim();
        var parts = status.Split('\t');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    public string GetRemoteUri(GitOperationSettings settings)
    {
        return ExecuteGitCommandAndReturnOutput("remote get-url origin", settings).Trim();
    }

    public string[] GetLocalBranchesOrderedByMostRecentCommitterDate(GitOperationSettings settings)
    {
        return ExecuteGitCommandAndReturnOutput("branch --format=%(refname:short) --sort=-committerdate", settings).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    private string ExecuteGitCommandAndReturnOutput(string command, GitOperationSettings settings)
    {
        if (settings.Verbose)
            console.MarkupLine($"[grey]git {command}[/]");

        var infoBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        var result = ShellExecutor.ExecuteCommand(
            "git",
            command,
            settings.WorkingDirectory ?? ".",
            (_) => { },
            (info) => infoBuilder.AppendLine(info),
            (error) => errorBuilder.AppendLine(error));

        if (result != 0)
        {
            console.MarkupLine($"[red]{errorBuilder}[/]");
            throw new Exception("Failed to execute git command.");
        }

        if (settings.Verbose && infoBuilder.Length > 0)
        {
            console.WriteLine(infoBuilder.ToString());
        }

        return infoBuilder.ToString();
    }

    private void ExecuteGitCommand(string command, GitOperationSettings settings)
    {
        if (settings.DryRun)
        {
            if (settings.Verbose)
                console.MarkupLine($"[grey]git {command}[/]");
        }
        else
        {
            var output = ExecuteGitCommandAndReturnOutput(command, settings);

            if (!settings.Verbose && output.Length > 0)
            {
                // We want to write the output of commands that perform
                // changes to the Git repository as the output might be important.
                // In verbose mode we would have already written the output
                // of the command so don't write it again.
                console.WriteLine(output);
            }
        }
    }
}
