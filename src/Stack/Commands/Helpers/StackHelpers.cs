using System.Text;
using Spectre.Console;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands.Helpers;

public class BranchDetail
{
    public BranchStatus Status { get; set; } = new(false, false, false, false, 0, 0, 0, 0, null);
    public GitHubPullRequest? PullRequest { get; set; }

    public bool IsActive => Status.ExistsLocally && Status.ExistsInRemote && (PullRequest is null || PullRequest.State != GitHubPullRequestStates.Merged);
    public bool CouldBeCleanedUp => Status.ExistsLocally && ((Status.HasRemoteTrackingBranch && !Status.ExistsInRemote) || (PullRequest is not null && PullRequest.State == GitHubPullRequestStates.Merged));
    public bool HasPullRequest => PullRequest is not null && PullRequest.State != GitHubPullRequestStates.Closed;
}
public record BranchStatus(
    bool ExistsLocally,
    bool HasRemoteTrackingBranch,
    bool ExistsInRemote,
    bool IsCurrentBranch,
    int AheadOfParent,
    int BehindParent,
    int AheadOfRemote,
    int BehindRemote,
    Commit? Tip);

public record StackStatus(Dictionary<string, BranchDetail> Branches)
{
    public string[] GetActiveBranches() => Branches.Where(b => b.Value.IsActive).Select(b => b.Key).ToArray();
}

public static class StackHelpers
{
    public static Dictionary<Config.Stack, StackStatus> GetStackStatus(
        List<Config.Stack> stacks,
        string currentBranch,
        IOutputProvider outputProvider,
        IGitClient gitClient,
        IGitHubClient gitHubClient,
        bool includePullRequestStatus = true)
    {
        var stacksToCheckStatusFor = new Dictionary<Config.Stack, StackStatus>();

        stacks
            .OrderByCurrentStackThenByName(currentBranch)
            .ToList()
            .ForEach(stack => stacksToCheckStatusFor.Add(stack, new StackStatus([])));

        var allBranchesInStacks = stacks.SelectMany(s => new List<string>([s.SourceBranch]).Concat(s.Branches)).Distinct().ToArray();

        var branchStatuses = gitClient.GetBranchStatuses(allBranchesInStacks);

        foreach (var (stack, status) in stacksToCheckStatusFor)
        {
            var parentBranch = stack.SourceBranch;

            status.Branches.Add(stack.SourceBranch, new BranchDetail());
            branchStatuses.TryGetValue(stack.SourceBranch, out var sourceBranchStatus);
            if (sourceBranchStatus is not null)
            {
                status.Branches[stack.SourceBranch].Status = new BranchStatus(
                    true,
                    sourceBranchStatus.RemoteTrackingBranchName is not null,
                    sourceBranchStatus.RemoteBranchExists,
                    sourceBranchStatus.IsCurrentBranch,
                    0,
                    0,
                    sourceBranchStatus.Ahead,
                    sourceBranchStatus.Behind,
                    sourceBranchStatus.Tip);
            }

            foreach (var branch in stack.Branches)
            {
                status.Branches.Add(branch, new BranchDetail());
                branchStatuses.TryGetValue(branch, out var branchStatus);

                if (branchStatus is not null)
                {
                    var (aheadOfParent, behindParent) = branchStatus.RemoteBranchExists ? gitClient.CompareBranches(branch, parentBranch) : (0, 0);

                    status.Branches[branch].Status = new BranchStatus(
                        true,
                        branchStatus.RemoteTrackingBranchName is not null,
                        branchStatus.RemoteBranchExists,
                        branchStatus.IsCurrentBranch,
                        aheadOfParent,
                        behindParent,
                        branchStatus.Ahead,
                        branchStatus.Behind,
                        branchStatus.Tip);

                    if (branchStatus.RemoteBranchExists)
                    {
                        parentBranch = branch;
                    }
                }
            }
        }

        if (includePullRequestStatus)
        {
            outputProvider.Status("Checking status of GitHub pull requests...", () =>
            {
                foreach (var (stack, status) in stacksToCheckStatusFor)
                {
                    try
                    {
                        foreach (var branch in stack.Branches)
                        {
                            var pr = gitHubClient.GetPullRequest(branch);

                            if (pr is not null)
                            {
                                status.Branches[branch].PullRequest = pr;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        outputProvider.Warning($"Error checking GitHub pull requests: {ex.Message}");
                    }
                }
            });
        }

        return stacksToCheckStatusFor;
    }

    public static StackStatus GetStackStatus(
        Config.Stack stack,
        string currentBranch,
        IOutputProvider outputProvider,
        IGitClient gitClient,
        IGitHubClient gitHubClient,
        bool includePullRequestStatus = true)
    {
        var statuses = GetStackStatus(
            [stack],
            currentBranch,
            outputProvider,
            gitClient,
            gitHubClient,
            includePullRequestStatus);

        return statuses[stack];
    }

    public static void OutputStackStatus(
        Dictionary<Config.Stack, StackStatus> stackStatuses,
        IOutputProvider outputProvider)
    {
        foreach (var (stack, status) in stackStatuses)
        {
            OutputStackStatus(stack, status, outputProvider);
            outputProvider.NewLine();
        }
    }

    public static void OutputStackStatus(
        Config.Stack stack,
        StackStatus status,
        IOutputProvider outputProvider)
    {
        var header = stack.SourceBranch.Branch();
        if (status.Branches.TryGetValue(stack.SourceBranch, out var sourceBranchStatus))
        {
            header = GetBranchStatusOutput(stack.SourceBranch, null, sourceBranchStatus);
        }
        var items = new List<string>();

        string parentBranch = stack.SourceBranch;

        foreach (var branch in stack.Branches)
        {
            if (status.Branches.TryGetValue(branch, out var branchDetail))
            {
                items.Add(GetBranchAndPullRequestStatusOutput(branch, parentBranch, branchDetail));

                if (branchDetail.IsActive)
                {
                    parentBranch = branch;
                }
            }
        }
        outputProvider.Information(stack.Name.Stack());
        outputProvider.Tree(header, [.. items]);
    }

    public static string GetBranchAndPullRequestStatusOutput(
        string branch,
        string? parentBranch,
        BranchDetail branchDetail)
    {
        var branchNameBuilder = new StringBuilder();
        branchNameBuilder.Append(GetBranchStatusOutput(branch, parentBranch, branchDetail));

        if (branchDetail.PullRequest is not null)
        {
            branchNameBuilder.Append($"   {branchDetail.PullRequest.GetPullRequestDisplay()}");
        }

        return branchNameBuilder.ToString();
    }

    public static string GetBranchStatusOutput(
        string branch,
        string? parentBranch,
        BranchDetail branchDetail)
    {
        var branchNameBuilder = new StringBuilder();

        var branchName = branchDetail.Status.IsCurrentBranch ? $"* {branch.Branch()}" : branch;
        Color? color = branchDetail.Status.ExistsLocally ? null : Color.Grey;
        Decoration? decoration = branchDetail.Status.ExistsLocally ? null : Decoration.Strikethrough;

        if (color is not null && decoration is not null)
        {
            branchNameBuilder.Append($"[{decoration} {color}]{branchName}[/]");
        }
        else if (color is not null)
        {
            branchNameBuilder.Append($"[{color}]{branchName}[/]");
        }
        else if (decoration is not null)
        {
            branchNameBuilder.Append($"[{decoration}]{branchName}[/]");
        }
        else
        {
            branchNameBuilder.Append(branchName);
        }

        if (branchDetail.IsActive)
        {
            if (branchDetail.Status.AheadOfRemote > 0 || branchDetail.Status.BehindRemote > 0)
            {
                branchNameBuilder.Append($" {branchDetail.Status.BehindRemote}{Emoji.Known.DownArrow}{branchDetail.Status.AheadOfRemote}{Emoji.Known.UpArrow}");
            }

            if (branchDetail.Status.AheadOfParent > 0 && branchDetail.Status.BehindParent > 0)
            {
                branchNameBuilder.Append($" ({branchDetail.Status.AheadOfParent} ahead, {branchDetail.Status.BehindParent} behind {parentBranch})".Muted());
            }
            else if (branchDetail.Status.AheadOfParent > 0)
            {
                branchNameBuilder.Append($" ({branchDetail.Status.AheadOfParent} ahead of {parentBranch})".Muted());
            }
            else if (branchDetail.Status.BehindParent > 0)
            {
                branchNameBuilder.Append($" ({branchDetail.Status.BehindParent} behind {parentBranch})".Muted());
            }
        }
        else if (branchDetail.Status.ExistsLocally && !branchDetail.Status.HasRemoteTrackingBranch)
        {
            branchNameBuilder.Append(" (no remote tracking branch)".Muted());
        }
        else if (branchDetail.Status.ExistsLocally && !branchDetail.Status.ExistsInRemote)
        {
            branchNameBuilder.Append(" (remote branch deleted)".Muted());
        }
        else if (branchDetail.PullRequest is not null && branchDetail.PullRequest.State == GitHubPullRequestStates.Merged)
        {
            branchNameBuilder.Append(" (pull request merged)".Muted());
        }

        if (branchDetail.Status.Tip is not null)
        {
            branchNameBuilder.Append($"   {branchDetail.Status.Tip.Sha[..7]} {branchDetail.Status.Tip.Message}");
        }

        return branchNameBuilder.ToString();
    }

    public static void OutputBranchAndStackActions(
        Config.Stack stack,
        StackStatus status,
        IOutputProvider outputProvider)
    {
        var statusOfBranchesInStack = status.Branches
            .Where(b => stack.Branches.Contains(b.Key))
            .Select(b => b.Value).ToList();

        if (statusOfBranchesInStack.All(branch => branch.CouldBeCleanedUp))
        {
            outputProvider.Information("All branches exist locally but are either not in the remote repository or the pull request associated with the branch is no longer open. This stack might be able to be deleted.");
            outputProvider.NewLine();
            outputProvider.Information($"Run {$"stack delete --name \"{stack.Name}\"".Example()} to delete the stack if it's no longer needed.");
            outputProvider.NewLine();
        }
        else if (statusOfBranchesInStack.Any(branch => branch.CouldBeCleanedUp))
        {
            outputProvider.Information("Some branches exist locally but are either not in the remote repository or the pull request associated with the branch is no longer open.");
            outputProvider.NewLine();
            outputProvider.Information($"Run {$"stack cleanup --name \"{stack.Name}\"".Example()} to clean up local branches.");
            outputProvider.NewLine();
        }
        else if (statusOfBranchesInStack.All(branch => !branch.Status.ExistsLocally))
        {
            outputProvider.Information("No branches exist locally. This stack might be able to be deleted.");
            outputProvider.NewLine();
            outputProvider.Information($"Run {$"stack delete --name \"{stack.Name}\"".Example()} to delete the stack.");
            outputProvider.NewLine();
        }

        if (statusOfBranchesInStack.Any(branch =>
                branch.Status.ExistsLocally &&
                (!branch.Status.HasRemoteTrackingBranch || branch.Status.ExistsInRemote && branch.Status.AheadOfRemote > 0)))
        {
            outputProvider.Information("There are changes in local branches that have not been pushed to the remote repository.");
            outputProvider.NewLine();
            outputProvider.Information($"Run {$"stack push --name \"{stack.Name}\"".Example()} to push the changes to the remote repository.");
            outputProvider.NewLine();
        }

        if (statusOfBranchesInStack.Any(branch => branch.Status.ExistsInRemote && branch.Status.ExistsLocally && branch.Status.BehindParent > 0))
        {
            outputProvider.Information("There are changes in source branches that have not been applied to the stack.");
            outputProvider.NewLine();
            outputProvider.Information($"Run {$"stack update --name \"{stack.Name}\"".Example()} to update the stack.");
            outputProvider.NewLine();
        }
    }

    public static UpdateStrategy? GetUpdateStrategyConfigValue(IGitClient gitClient)
    {
        var strategyConfigValue = gitClient.GetConfigValue("stack.update.strategy");

        if (strategyConfigValue is not null)
        {
            if (Enum.TryParse<UpdateStrategy>(strategyConfigValue, true, out var configuredStrategy))
            {
                return configuredStrategy;
            }
            else
            {
                throw new InvalidOperationException($"Invalid value '{strategyConfigValue}' for 'stack.update.strategy'.");
            }
        }

        return null;
    }

    public static void UpdateStack(
        Config.Stack stack,
        StackStatus status,
        UpdateStrategy? specificUpdateStrategy,
        IGitClient gitClient,
        IInputProvider inputProvider,
        IOutputProvider outputProvider)
    {
        var strategy = UpdateStrategy.Merge;

        if (specificUpdateStrategy is not null)
        {
            strategy = specificUpdateStrategy.Value;
        }
        else
        {
            var strategyFromConfig = GetUpdateStrategyConfigValue(gitClient);

            if (strategyFromConfig is not null)
            {
                strategy = strategyFromConfig.Value;
            }
        }

        if (strategy == UpdateStrategy.Rebase)
        {
            UpdateStackUsingRebase(stack, status, gitClient, inputProvider, outputProvider);
        }
        else
        {
            UpdateStackUsingMerge(stack, status, gitClient, inputProvider, outputProvider);
        }
    }

    public static void UpdateStackUsingMerge(
        Config.Stack stack,
        StackStatus status,
        IGitClient gitClient,
        IInputProvider inputProvider,
        IOutputProvider outputProvider)
    {
        var sourceBranch = stack.SourceBranch;

        foreach (var branch in stack.Branches)
        {
            var branchDetail = status.Branches[branch];

            if (branchDetail.IsActive)
            {
                MergeFromSourceBranch(branch, sourceBranch, gitClient, inputProvider, outputProvider);
                sourceBranch = branch;
            }
            else
            {
                outputProvider.Debug($"Branch '{branch}' no longer exists on the remote repository or the associated pull request is no longer open. Skipping...");
            }
        }
    }

    public static void PullChanges(Config.Stack stack, IGitClient gitClient, IOutputProvider outputProvider)
    {
        List<string> allBranchesInStacks = [stack.SourceBranch, .. stack.Branches];
        var branchStatus = gitClient.GetBranchStatuses([.. allBranchesInStacks]);

        foreach (var branch in allBranchesInStacks.Where(b => branchStatus.ContainsKey(b) && branchStatus[b].RemoteBranchExists))
        {
            outputProvider.Information($"Pulling changes for {branch.Branch()} from remote");
            gitClient.ChangeBranch(branch);
            gitClient.PullBranch(branch);
        }
    }

    public static void PushChanges(
        Config.Stack stack,
        int maxBatchSize,
        bool forceWithLease,
        IGitClient gitClient,
        IOutputProvider outputProvider)
    {
        var branchStatus = gitClient.GetBranchStatuses([.. stack.Branches]);

        var branchesThatHaveNotBeenPushedToRemote = branchStatus.Where(b => b.Value.RemoteTrackingBranchName is null).Select(b => b.Value.BranchName).ToList();

        foreach (var branch in branchesThatHaveNotBeenPushedToRemote)
        {
            outputProvider.Information($"Pushing new branch {branch.Branch()} to remote");
            gitClient.PushNewBranch(branch);
        }

        var branchesInStackWithRemote = branchStatus.Where(b => b.Value.RemoteBranchExists).Select(b => b.Value.BranchName).ToList();

        var branchGroupsToPush = branchesInStackWithRemote
            .Select((b, i) => new { Index = i, Value = b })
            .GroupBy(b => b.Index / maxBatchSize)
            .Select(g => g.Select(b => b.Value).ToList())
            .ToList();

        foreach (var branches in branchGroupsToPush)
        {
            outputProvider.Information($"Pushing changes for {string.Join(", ", branches.Select(b => b.Branch()))} to remote");

            gitClient.PushBranches([.. branches], forceWithLease);
        }
    }

    static void MergeFromSourceBranch(string branch, string sourceBranchName, IGitClient gitClient, IInputProvider inputProvider, IOutputProvider outputProvider)
    {
        outputProvider.Information($"Merging {sourceBranchName.Branch()} into {branch.Branch()}");
        gitClient.ChangeBranch(branch);

        try
        {
            gitClient.MergeFromLocalSourceBranch(sourceBranchName);
        }
        catch (ConflictException)
        {
            var action = inputProvider.Select(
                Questions.ContinueOrAbortMerge,
                [MergeConflictAction.Continue, MergeConflictAction.Abort],
                a => a switch
                {
                    MergeConflictAction.Continue => "Continue",
                    MergeConflictAction.Abort => "Abort",
                    _ => throw new InvalidOperationException("Invalid merge conflict action.")
                }); ;

            if (action == MergeConflictAction.Abort)
            {
                gitClient.AbortMerge();
                throw new Exception("Merge aborted due to conflicts.");
            }
        }
    }

    static void UpdateStackUsingRebase(
        Config.Stack stack,
        StackStatus status,
        IGitClient gitClient,
        IInputProvider inputProvider,
        IOutputProvider outputProvider)
    {
        //
        // When rebasing the stack, we'll use `git rebase --update-refs` from the
        // lowest branch in the stack to pick up changes throughout all branches in the stack.
        // Because there could be changes in any branch in the stack that aren't in the ones
        // below it, we'll repeat this all the way from the bottom to the top of the stack.        
        //
        // For example if we have a stack like this:
        // main -> feature1 -> feature2 -> feature3
        // 
        // We'll rebase feature3 onto feature2, then feature3 onto feature1, and finally feature3 onto main.
        //
        string? branchToRebaseFrom = null;

        foreach (var branch in stack.Branches)
        {
            var branchDetail = status.Branches[branch];

            if (branchDetail.IsActive)
            {
                branchToRebaseFrom = branch;
            }
        }

        if (branchToRebaseFrom is null)
        {
            outputProvider.Warning("No active branches found in the stack.");
            return;
        }

        var branchesToRebaseOnto = new List<string>(stack.Branches);
        branchesToRebaseOnto.Reverse();
        branchesToRebaseOnto.Remove(branchToRebaseFrom);
        branchesToRebaseOnto.Add(stack.SourceBranch);

        foreach (var branchToRebaseOnto in branchesToRebaseOnto)
        {
            var branchDetail = status.Branches[branchToRebaseOnto];

            if (branchDetail.IsActive)
            {
                RebaseFromSourceBranch(branchToRebaseFrom, branchToRebaseOnto, gitClient, inputProvider, outputProvider);
            }
        }
    }

    static void RebaseFromSourceBranch(string branch, string sourceBranchName, IGitClient gitClient, IInputProvider inputProvider, IOutputProvider outputProvider)
    {
        outputProvider.Information($"Rebasing {branch.Branch()} onto {sourceBranchName.Branch()}");
        gitClient.ChangeBranch(branch);

        void HandleConflicts()
        {
            var action = inputProvider.Select(
                Questions.ContinueOrAbortRebase,
                [MergeConflictAction.Continue, MergeConflictAction.Abort],
                a => a switch
                {
                    MergeConflictAction.Continue => "Continue",
                    MergeConflictAction.Abort => "Abort",
                    _ => throw new InvalidOperationException("Invalid rebase conflict action.")
                });

            if (action == MergeConflictAction.Abort)
            {
                gitClient.AbortMerge();
                throw new Exception("Rebase aborted due to conflicts.");
            }
            else if (action == MergeConflictAction.Continue)
            {
                try
                {
                    gitClient.ContinueRebase();
                }
                catch (ConflictException)
                {
                    HandleConflicts();
                }
            }
        }

        try
        {
            gitClient.RebaseFromLocalSourceBranch(sourceBranchName);
        }
        catch (ConflictException)
        {
            HandleConflicts();
        }
    }
}

public enum MergeConflictAction
{
    Abort,
    Continue
}

public enum UpdateStrategy
{
    Merge,
    Rebase
}