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
    void CreateNewBranch(string branchName, string sourceBranch);
    void PushNewBranch(string branchName);
    void PushBranch(string branchName);
    void ChangeBranch(string branchName);
    void FetchBranches(string[] branches);
    void PullBranch(string branchName);
    void UpdateBranch(string branchName);
    void DeleteLocalBranch(string branchName);
    void MergeFromLocalSourceBranch(string sourceBranchName);
    string GetCurrentBranch();
    string GetDefaultBranch();
    bool DoesLocalBranchExist(string branchName);
    bool DoesRemoteBranchExist(string branchName);
    string[] GetBranchesThatExistLocally(string[] branches);
    string[] GetBranchesThatExistInRemote(string[] branches);
    bool IsRemoteBranchFullyMerged(string branchName, string sourceBranchName);
    string[] GetBranchesThatHaveBeenMerged(string[] branches, string sourceBranchName);
    (int Ahead, int Behind) GetStatusOfRemoteBranch(string branchName, string sourceBranchName);
    string GetRemoteUri();
    string[] GetLocalBranchesOrderedByMostRecentCommitterDate();
    string GetRootOfRepository();
    string? GetFileContents(string path);
}

public class GitOperations(IAnsiConsole console, GitOperationSettings settings) : IGitOperations
{
    public void CreateNewBranch(string branchName, string sourceBranch)
    {
        ExecuteGitCommand($"branch {branchName} {sourceBranch}");
    }

    public void PushNewBranch(string branchName)
    {
        ExecuteGitCommand($"push -u origin {branchName}");
    }

    public void PushBranch(string branchName)
    {
        ExecuteGitCommand($"push origin {branchName}");
    }

    public void ChangeBranch(string branchName)
    {
        ExecuteGitCommand($"checkout {branchName}");
    }

    public void FetchBranches(string[] branches)
    {
        ExecuteGitCommand($"fetch origin {string.Join(" ", branches)}");
    }

    public void PullBranch(string branchName)
    {
        ExecuteGitCommand($"pull origin {branchName}");
    }

    public void UpdateBranch(string branchName)
    {
        var currentBranch = GetCurrentBranch();

        if (!currentBranch.Equals(branchName, StringComparison.OrdinalIgnoreCase))
        {
            ChangeBranch(branchName);
        }

        PullBranch(branchName);
    }

    public void DeleteLocalBranch(string branchName)
    {
        ExecuteGitCommand($"branch -D {branchName}");
    }

    public void MergeFromLocalSourceBranch(string sourceBranchName)
    {
        ExecuteGitCommand($"merge {sourceBranchName}");
    }

    public string GetCurrentBranch()
    {
        return ExecuteGitCommandAndReturnOutput("branch --show-current").Trim();
    }

    public string GetDefaultBranch()
    {
        return ExecuteGitCommandAndReturnOutput("symbolic-ref refs/remotes/origin/HEAD").Trim().Replace("refs/remotes/origin/", "");
    }

    public bool DoesRemoteBranchExist(string branchName)
    {
        return ExecuteGitCommandAndReturnOutput($"ls-remote --heads origin {branchName}").Trim().Length > 0;
    }

    public bool DoesLocalBranchExist(string branchName)
    {
        return ExecuteGitCommandAndReturnOutput($"branch --list {branchName}").Trim().Length > 0;
    }

    public string[] GetBranchesThatExistLocally(string[] branches)
    {
        var localBranches = ExecuteGitCommandAndReturnOutput("branch --format=%(refname:short)").Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        return branches.Where(b => localBranches.Any(lb => lb.Equals(b, StringComparison.OrdinalIgnoreCase))).ToArray();
    }

    public string[] GetBranchesThatExistInRemote(string[] branches)
    {
        var remoteBranches = ExecuteGitCommandAndReturnOutput($"ls-remote --heads origin {string.Join(" ", branches)}").Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        return branches.Where(b => remoteBranches.Any(rb => rb.EndsWith(b))).ToArray();
    }

    public bool IsRemoteBranchFullyMerged(string branchName, string sourceBranchName)
    {
        var remoteBranchesThatHaveBeenMerged = ExecuteGitCommandAndReturnOutput($"branch --remote --merged {sourceBranchName}").Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        return remoteBranchesThatHaveBeenMerged.Any(b => b.EndsWith(branchName));
    }

    public string[] GetBranchesThatHaveBeenMerged(string[] branches, string sourceBranchName)
    {
        var remoteBranchesThatHaveBeenMerged = ExecuteGitCommandAndReturnOutput($"branch --remote --merged {sourceBranchName}").Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        return branches.Where(b => remoteBranchesThatHaveBeenMerged.Any(rb => rb.EndsWith(b))).ToArray();
    }

    public (int Ahead, int Behind) GetStatusOfRemoteBranch(string branchName, string sourceBranchName)
    {
        var status = ExecuteGitCommandAndReturnOutput($"rev-list --left-right --count origin/{branchName}...origin/{sourceBranchName}").Trim();
        var parts = status.Split('\t');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    public string GetRemoteUri()
    {
        return ExecuteGitCommandAndReturnOutput("remote get-url origin").Trim();
    }

    public string[] GetLocalBranchesOrderedByMostRecentCommitterDate()
    {
        return ExecuteGitCommandAndReturnOutput("branch --format=%(refname:short) --sort=-committerdate").Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    public string GetRootOfRepository()
    {
        return ExecuteGitCommandAndReturnOutput("rev-parse --show-toplevel").Trim();
    }

    public string? GetFileContents(string path)
    {
        if (!File.Exists(path))
            return null;

        return File.ReadAllText(path);
    }

    private string ExecuteGitCommandAndReturnOutput(string command)
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
            console.MarkupLine($"[grey]{infoBuilder}[/]");
        }

        return infoBuilder.ToString();
    }

    private void ExecuteGitCommand(string command)
    {
        if (settings.DryRun)
        {
            if (settings.Verbose)
                console.MarkupLine($"[grey]git {command}[/]");
        }
        else
        {
            var output = ExecuteGitCommandAndReturnOutput(command);

            if (!settings.Verbose && output.Length > 0)
            {
                // We want to write the output of commands that perform
                // changes to the Git repository as the output might be important.
                // In verbose mode we would have already written the output
                // of the command so don't write it again.
                console.MarkupLine($"[grey]{output}[/]");
            }
        }
    }
}
