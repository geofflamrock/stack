using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Git;

public record Commit(string Sha, string Message);

public record GitBranchStatus(
    string BranchName,
    string? RemoteTrackingBranchName,
    bool RemoteBranchExists,
    bool IsCurrentBranch,
    int Ahead,
    int Behind,
    Commit Tip,
    string? WorktreePath = null);

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
    string? GetMergeBase(string branch1, string branch2);
    bool IsCommitReachableFromBranch(string commitSha, string branchName);

    bool IsMergeInProgress();
    bool IsRebaseInProgress();
    string GetHeadSha();
    string? GetOriginalHeadSha();

    void Fetch(bool prune);

    void ChangeBranch(string branchName);
    void CreateNewBranch(string branchName, string sourceBranch);
    void PushNewBranch(string branchName);
    void PullBranch(string branchName);
    void FetchBranchRefSpecs(string[] branchNames);
    void PushBranches(string[] branches, bool forceWithLease);
    void DeleteLocalBranch(string branchName);

    void MergeFromLocalSourceBranch(string sourceBranchName);
    void RebaseFromLocalSourceBranch(string sourceBranchName);
    void RebaseOntoNewParent(string newParentBranchName, string oldParentBranchName);
    void AbortMerge();
    void AbortRebase();
    void ContinueRebase();
}

public class GitClient(ILogger<GitClient> logger, string workingDirectory) : IGitClient
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
        var configValue = ExecuteGitCommandAndReturnOutput($"config --get {key}", false, (info, result) =>
        {
            if (result.ExitCode == 1)
            {
                return;
            }

            throw new Exception("Failed to get config value.");
        })?.Trim();

        return string.IsNullOrEmpty(configValue) ? null : configValue;
    }

    public bool IsAncestor(string ancestor, string descendant)
    {
        var isAncestor = false;

        ExecuteGitCommand($"merge-base --is-ancestor {ancestor} {descendant}", false, (info, result) =>
        {
            isAncestor = result.ExitCode == 0;
        });

        return isAncestor;
    }

    public bool IsMergeInProgress()
    {
        var inProgress = true;
        // "git rev-parse -q --verify MERGE_HEAD" returns 0 when a merge is in progress
        ExecuteGitCommand("rev-parse -q --verify MERGE_HEAD", false, (info, result) =>
        {
            inProgress = result.ExitCode == 0;
        });
        return inProgress;
    }

    public bool IsRebaseInProgress()
    {
        // Detect presence of .git/rebase-merge or .git/rebase-apply directories
        // Use repo root to build path; Git guarantees these directories during an interactive or normal rebase

        // Resolve the git dir for this working tree (works for normal repo & worktrees)
        var gitDir = ExecuteGitCommandAndReturnOutput("rev-parse --git-dir").Trim();

        // rev-parse may return a relative path (e.g. .git); make it absolute
        if (!Path.IsPathRooted(gitDir))
        {
            gitDir = Path.GetFullPath(Path.Combine(workingDirectory, gitDir));
        }

        var rebaseMerge = Path.Combine(gitDir, "rebase-merge");
        var rebaseApply = Path.Combine(gitDir, "rebase-apply");
        return Directory.Exists(rebaseMerge) || Directory.Exists(rebaseApply);
    }

    public string GetHeadSha()
    {
        return ExecuteGitCommandAndReturnOutput("rev-parse HEAD").Trim();
    }

    public string? GetOriginalHeadSha()
    {
        // ORIG_HEAD is updated by Git before dangerous operations (merge, rebase, reset).
        // Use quiet verify; exit code 0 when ref exists, 1 otherwise.
        string? orig = ExecuteGitCommandAndReturnOutput("rev-parse -q --verify ORIG_HEAD", false, (info, result) =>
        {
            if (result.ExitCode > 1)
            {
                throw new Exception("Failed to read ORIG_HEAD");
            }
        })?.Trim();

        return string.IsNullOrWhiteSpace(orig) ? null : orig;
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

    public void FetchBranchRefSpecs(string[] branchNames)
    {
        if (branchNames is null || branchNames.Length == 0)
        {
            return;
        }

        var refSpecs = string.Join(" ", branchNames.Select(b => $"{b}:{b}"));
        ExecuteGitCommand($"fetch origin {refSpecs}");
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
        ExecuteGitCommand($"merge {sourceBranchName}", false, (info, result) =>
        {
            if (result.StandardOutput.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                result.StandardError.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException();
            }

            throw new ProcessException(result.StandardError, info.FileName, info.Arguments, result.ExitCode);
        });
    }

    public void RebaseFromLocalSourceBranch(string sourceBranchName)
    {
        ExecuteGitCommand($"rebase {sourceBranchName} --update-refs", false, (info, result) =>
        {
            if (result.StandardOutput.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                result.StandardError.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException();
            }

            throw new ProcessException(result.StandardError, info.FileName, info.Arguments, result.ExitCode);
        });
    }

    public void RebaseOntoNewParent(string newParentBranchName, string oldParentBranchName)
    {
        ExecuteGitCommand($"rebase --onto {newParentBranchName} {oldParentBranchName} --update-refs", false, (info, result) =>
        {
            if (result.StandardOutput.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                result.StandardError.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException();
            }

            throw new ProcessException(result.StandardError, info.FileName, info.Arguments, result.ExitCode);
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
        ExecuteGitCommand($"rebase --continue", false, (info, result) =>
        {
            if (result.StandardOutput.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                result.StandardError.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException();
            }

            throw new ProcessException(result.StandardError, info.FileName, info.Arguments, result.ExitCode);
        });
    }

    private string ExecuteGitCommandAndReturnOutput(
        string command,
        bool captureStandardError = false,
        Action<ProcessExecutionInfo, ProcessExecutionResult>? exceptionHandler = null)
    {
        var result = ProcessHelpers.ExecuteProcessAndReturnOutput(
            "git",
            command,
            workingDirectory,
            logger,
            captureStandardError,
            exceptionHandler
        );

        var output = result.StandardOutput;

        if (captureStandardError && !string.IsNullOrWhiteSpace(result.StandardError))
        {
            output += $"{Environment.NewLine}{result.StandardError}";
        }

        return output;
    }

    private void ExecuteGitCommand(
        string command,
        bool captureStandardError = false,
        Action<ProcessExecutionInfo, ProcessExecutionResult>? exceptionHandler = null)
    {
        ExecuteGitCommandAndReturnOutput(command, captureStandardError, exceptionHandler);
    }

    public string? GetMergeBase(string branch1, string branch2)
    {
        var mergeBase = ExecuteGitCommandAndReturnOutput($"merge-base {branch1} {branch2}", false, (info, result) =>
        {
            if (result.ExitCode > 1)
            {
                throw new Exception("Failed to get merge base");
            }
        })?.Trim();

        return string.IsNullOrWhiteSpace(mergeBase) ? null : mergeBase;
    }

    public bool IsCommitReachableFromBranch(string commitSha, string branchName)
    {
        var branchesThatContainTheCommit = ExecuteGitCommandAndReturnOutput($"branch --contains {commitSha}", false, (info, result) =>
        {
            if (result.ExitCode > 1)
            {
                throw new Exception("Failed to check if commit is reachable from branch");
            }
        })?.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(b => b.TrimStart('*', ' ').Trim())
            .ToArray() ?? Array.Empty<string>();

        return branchesThatContainTheCommit.Contains(branchName);
    }
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "{Output}")]
    public static partial void DebugCommandOutput(this ILogger logger, string output);
}
