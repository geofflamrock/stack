using System.Diagnostics;
using Spectre.Console;
using Stack.Infrastructure;

namespace Stack.Git;

public record GitClientSettings(bool Verbose, string? WorkingDirectory)
{
    public static GitClientSettings Default => new(false, null);
}

public record Commit(string Sha, string Message);

public record GitBranchStatus(string BranchName, string? RemoteTrackingBranchName, bool RemoteBranchExists, bool IsCurrentBranch, int Ahead, int Behind, Commit Tip);

public class ConflictException : Exception;

public interface IGitClient
{
    string GetCurrentBranch();
    bool DoesLocalBranchExist(string branchName);
    string[] GetBranchesThatExistLocally(string[] branches);
    (int Ahead, int Behind) CompareBranches(string branchName, string sourceBranchName);
    Dictionary<string, GitBranchStatus> GetBranchStatuses(string[] branches);
    string GetRemoteUri();
    string[] GetLocalBranchesOrderedByMostRecentCommitterDate();
    string GetRootOfRepository();
    string? GetConfigValue(string key);
    bool IsAncestor(string ancestor, string descendant);
    void Fetch(bool prune);

    void ChangeBranch(string branchName);
    void CreateNewBranch(string branchName, string sourceBranch);
    void PushNewBranch(string branchName);
    void PullBranch(string branchName);
    void PushBranches(string[] branches, bool forceWithLease);
    void DeleteLocalBranch(string branchName);

    void MergeFromLocalSourceBranch(string sourceBranchName);
    void RebaseFromLocalSourceBranch(string sourceBranchName);
    void RebaseOntoNewParent(string newParentBranchName, string oldParentBranchName);
    void AbortMerge();
    void AbortRebase();
    void ContinueRebase();
}

public class GitClient(ILogger logger, GitClientSettings settings) : IGitClient
{
    public string GetCurrentBranch()
    {
        return ExecuteGitCommandAndReturnOutput("branch --show-current").Trim();
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

    public (int Ahead, int Behind) CompareBranches(string branchName, string sourceBranchName)
    {
        var status = ExecuteGitCommandAndReturnOutput($"rev-list --left-right --count {branchName}...{sourceBranchName}").Trim();
        var parts = status.Split('\t');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    public Dictionary<string, GitBranchStatus> GetBranchStatuses(string[] branches)
    {
        var statuses = new Dictionary<string, GitBranchStatus>();

        var gitBranchVerbose = ExecuteGitCommandAndReturnOutput("branch -vv").Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        foreach (var branchStatus in gitBranchVerbose)
        {
            var status = GitBranchStatusParser.Parse(branchStatus);

            if (status is not null && branches.Contains(status.BranchName))
            {
                statuses.Add(status.BranchName, status);
            }
        }

        return statuses;
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

    public string? GetConfigValue(string key)
    {
        var configValue = ExecuteGitCommandAndReturnOutput($"config --get {key}", false, exitCode =>
        {
            if (exitCode == 1)
            {
                return null;
            }

            return new Exception("Failed to get config value.");
        })?.Trim();

        return string.IsNullOrEmpty(configValue) ? null : configValue;
    }

    public bool IsAncestor(string ancestor, string descendant)
    {
        var isAncestor = false;

        ExecuteGitCommand($"merge-base --is-ancestor {ancestor} {descendant}", false, exitCode =>
        {
            isAncestor = exitCode == 0;
            return null;
        });

        return isAncestor;
    }

    public void Fetch(bool prune)
    {
        ExecuteGitCommand($"fetch origin {(prune ? "--prune" : string.Empty)}");
    }

    public void ChangeBranch(string branchName)
    {
        ExecuteGitCommand($"checkout {branchName}");
    }

    public void CreateNewBranch(string branchName, string sourceBranch)
    {
        ExecuteGitCommand($"branch {branchName} {sourceBranch}");
    }

    public virtual void PushNewBranch(string branchName)
    {
        ExecuteGitCommand($"push -u origin {branchName}");
    }

    public void PullBranch(string branchName)
    {
        ExecuteGitCommand($"pull origin {branchName}");
    }

    public void PushBranches(string[] branches, bool forceWithLease)
    {
        var command = $"push origin {string.Join(" ", branches)}";
        if (forceWithLease)
        {
            command += " --force-with-lease";
        }

        ExecuteGitCommand(command, true);
    }

    public void DeleteLocalBranch(string branchName)
    {
        ExecuteGitCommand($"branch -D {branchName}");
    }

    public void MergeFromLocalSourceBranch(string sourceBranchName)
    {
        ExecuteGitCommand($"merge {sourceBranchName}", false, exitCode =>
        {
            if (exitCode > 0)
            {
                return new ConflictException();
            }

            return null;
        });
    }

    public void RebaseFromLocalSourceBranch(string sourceBranchName)
    {
        ExecuteGitCommand($"rebase {sourceBranchName} --update-refs", false, exitCode =>
        {
            if (exitCode > 0)
            {
                return new ConflictException();
            }

            return null;
        });
    }

    public void RebaseOntoNewParent(string newParentBranchName, string oldParentBranchName)
    {
        ExecuteGitCommand($"rebase --onto {newParentBranchName} {oldParentBranchName} --update-refs", false, exitCode =>
        {
            if (exitCode > 0)
            {
                return new ConflictException();
            }

            return null;
        });
    }

    public void AbortMerge()
    {
        ExecuteGitCommand("merge --abort");
    }

    public void AbortRebase()
    {
        ExecuteGitCommand("rebase --abort");
    }

    public void ContinueRebase()
    {
        ExecuteGitCommand($"rebase --continue", false, exitCode =>
        {
            if (exitCode > 0)
            {
                return new ConflictException();
            }

            return null;
        });
    }

    private string ExecuteGitCommandAndReturnOutput(
        string command,
        bool captureStandardError = false,
        Func<int, Exception?>? exceptionHandler = null)
    {
        return ProcessHelpers.ExecuteProcessAndReturnOutput(
            "git",
            command,
            settings.WorkingDirectory,
            logger,
            settings.Verbose,
            captureStandardError,
            exceptionHandler
        );
    }

    private void ExecuteGitCommand(
        string command,
        bool captureStandardError = false,
        Func<int, Exception?>? exceptionHandler = null)
    {
        var output = ExecuteGitCommandAndReturnOutput(command, captureStandardError, exceptionHandler);

        if (!settings.Verbose && output.Length > 0)
        {
            // We want to write the output of commands that perform
            // changes to the Git repository as the output might be important.
            // In verbose mode we would have already written the output
            // of the command so don't write it again.
            logger.Debug(Markup.Escape(output));
        }
    }
}
