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
        ILogger logger,
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
            logger.Status("Checking status of GitHub pull requests...", () =>
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
                        logger.Warning($"Error checking GitHub pull requests: {ex.Message}");
                    }
                }
            });
        }

        return stacksToCheckStatusFor;
    }

    public static StackStatus GetStackStatus(
        Config.Stack stack,
        string currentBranch,
        ILogger logger,
        IGitClient gitClient,
        IGitHubClient gitHubClient,
        bool includePullRequestStatus = true)
    {
        var statuses = GetStackStatus(
            [stack],
            currentBranch,
            logger,
            gitClient,
            gitHubClient,
            includePullRequestStatus);

        return statuses[stack];
    }

    public static void OutputStackStatus(
        Dictionary<Config.Stack, StackStatus> stackStatuses,
        ILogger logger)
    {
        foreach (var (stack, status) in stackStatuses)
        {
            OutputStackStatus(stack, status, logger);
            logger.NewLine();
        }
    }

    public static void OutputStackStatus(
        Config.Stack stack,
        StackStatus status,
        ILogger logger)
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
        logger.Information(stack.Name.Stack());
        logger.Tree(header, [.. items]);
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
            branchNameBuilder.Append($"   {branchDetail.Status.Tip.Sha[..7]} {Markup.Escape(branchDetail.Status.Tip.Message)}");
        }

        return branchNameBuilder.ToString();
    }

    public static void OutputBranchAndStackActions(
        Config.Stack stack,
        StackStatus status,
        ILogger logger)
    {
        var statusOfBranchesInStack = status.Branches
            .Where(b => stack.Branches.Contains(b.Key))
            .Select(b => b.Value).ToList();

        if (statusOfBranchesInStack.All(branch => branch.CouldBeCleanedUp))
        {
            logger.Information("All branches exist locally but are either not in the remote repository or the pull request associated with the branch is no longer open. This stack might be able to be deleted.");
            logger.NewLine();
            logger.Information($"Run {$"stack delete --stack \"{stack.Name}\"".Example()} to delete the stack if it's no longer needed.");
            logger.NewLine();
        }
        else if (statusOfBranchesInStack.Any(branch => branch.CouldBeCleanedUp))
        {
            logger.Information("Some branches exist locally but are either not in the remote repository or the pull request associated with the branch is no longer open.");
            logger.NewLine();
            logger.Information($"Run {$"stack cleanup --stack \"{stack.Name}\"".Example()} to clean up local branches.");
            logger.NewLine();
        }
        else if (statusOfBranchesInStack.All(branch => !branch.Status.ExistsLocally))
        {
            logger.Information("No branches exist locally. This stack might be able to be deleted.");
            logger.NewLine();
            logger.Information($"Run {$"stack delete --stack \"{stack.Name}\"".Example()} to delete the stack.");
            logger.NewLine();
        }

        if (statusOfBranchesInStack.Any(branch =>
                branch.Status.ExistsLocally &&
                (!branch.Status.HasRemoteTrackingBranch || branch.Status.ExistsInRemote && branch.Status.AheadOfRemote > 0)))
        {
            logger.Information("There are changes in local branches that have not been pushed to the remote repository.");
            logger.NewLine();
            logger.Information($"Run {$"stack push --stack \"{stack.Name}\"".Example()} to push the changes to the remote repository.");
            logger.NewLine();
        }

        if (statusOfBranchesInStack.Any(branch => branch.Status.ExistsInRemote && branch.Status.ExistsLocally && branch.Status.BehindParent > 0))
        {
            logger.Information("There are changes in source branches that have not been applied to the stack.");
            logger.NewLine();
            logger.Information($"Run {$"stack update --stack \"{stack.Name}\"".Example()} to update the stack locally.");
            logger.NewLine();
            logger.Information($"Run {$"stack sync --stack \"{stack.Name}\"".Example()} to sync the stack with the remote repository.");
            logger.NewLine();
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
        ILogger logger)
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
            UpdateStackUsingRebase(stack, status, gitClient, inputProvider, logger);
        }
        else
        {
            UpdateStackUsingMerge(stack, status, gitClient, inputProvider, logger);
        }
    }

    public static void UpdateStackUsingMerge(
        Config.Stack stack,
        StackStatus status,
        IGitClient gitClient,
        IInputProvider inputProvider,
        ILogger logger)
    {
        var sourceBranch = stack.SourceBranch;

        foreach (var branch in stack.Branches)
        {
            var branchDetail = status.Branches[branch];

            if (branchDetail.IsActive)
            {
                MergeFromSourceBranch(branch, sourceBranch, gitClient, inputProvider, logger);
                sourceBranch = branch;
            }
            else
            {
                logger.Debug($"Branch '{branch}' no longer exists on the remote repository or the associated pull request is no longer open. Skipping...");
            }
        }
    }

    public static void PullChanges(Config.Stack stack, IGitClient gitClient, ILogger logger)
    {
        List<string> allBranchesInStacks = [stack.SourceBranch, .. stack.Branches];
        var branchStatus = gitClient.GetBranchStatuses([.. allBranchesInStacks]);

        foreach (var branch in allBranchesInStacks.Where(b => branchStatus.ContainsKey(b) && branchStatus[b].RemoteBranchExists))
        {
            logger.Information($"Pulling changes for {branch.Branch()} from remote");
            gitClient.ChangeBranch(branch);
            gitClient.PullBranch(branch);
        }
    }

    public static void PushChanges(
        Config.Stack stack,
        int maxBatchSize,
        bool forceWithLease,
        IGitClient gitClient,
        ILogger logger)
    {
        var branchStatus = gitClient.GetBranchStatuses([.. stack.Branches]);

        var branchesThatHaveNotBeenPushedToRemote = branchStatus.Where(b => b.Value.RemoteTrackingBranchName is null).Select(b => b.Value.BranchName).ToList();

        foreach (var branch in branchesThatHaveNotBeenPushedToRemote)
        {
            logger.Information($"Pushing new branch {branch.Branch()} to remote");
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
            logger.Information($"Pushing changes for {string.Join(", ", branches.Select(b => b.Branch()))} to remote");

            gitClient.PushBranches([.. branches], forceWithLease);
        }
    }

    public static string[] GetBranchesNeedingCleanup(Config.Stack stack, ILogger logger, IGitClient gitClient, IGitHubClient gitHubClient)
    {
        var currentBranch = gitClient.GetCurrentBranch();
        var stackStatus = GetStackStatus(stack, currentBranch, logger, gitClient, gitHubClient, true);

        return [.. stackStatus.Branches.Where(b => b.Value.CouldBeCleanedUp).Select(b => b.Key)];
    }

    public static void OutputBranchesNeedingCleanup(ILogger logger, string[] branches)
    {
        logger.Information("The following branches exist locally but are either not in the remote repository or the pull request associated with the branch is no longer open:");

        foreach (var branch in branches)
        {
            logger.Information($"  {branch.Branch()}");
        }
    }

    public static void CleanupBranches(IGitClient gitClient, ILogger logger, string[] branches)
    {
        foreach (var branch in branches)
        {
            logger.Information($"Deleting local branch {branch.Branch()}");
            gitClient.DeleteLocalBranch(branch);
        }
    }

    public static void UpdateStackDescriptionInPullRequests(
        ILogger logger,
        IGitHubClient gitHubClient,
        Config.Stack stack,
        List<GitHubPullRequest> pullRequestsInStack)
    {
        // Edit each PR and add to the top of the description
        // the details of each PR in the stack
        var prList = pullRequestsInStack
            .Select(pr => $"- {pr.Url}")
            .ToList();
        var prListMarkdown = string.Join(Environment.NewLine, prList);
        var prBodyMarkdown = $"{StackConstants.StackMarkerStart}{Environment.NewLine}{stack.PullRequestDescription}{Environment.NewLine}{Environment.NewLine}{prListMarkdown}{Environment.NewLine}{StackConstants.StackMarkerEnd}";

        foreach (var pullRequest in pullRequestsInStack)
        {
            // Find the existing part of the PR body that has the PR list
            // and replace it with the updated PR list
            var prBody = pullRequest.Body;

            var prListStart = prBody.IndexOf(StackConstants.StackMarkerStart, StringComparison.OrdinalIgnoreCase);
            var prListEnd = prBody.IndexOf(StackConstants.StackMarkerEnd, StringComparison.OrdinalIgnoreCase);

            if (prListStart >= 0 && prListEnd >= 0)
            {
                prBody = prBody.Remove(prListStart, prListEnd - prListStart + StackConstants.StackMarkerEnd.Length);
                prBody = prBody.Insert(prListStart, prBodyMarkdown);

                logger.Information($"Updating pull request {pullRequest.GetPullRequestDisplay()} with stack details");

                gitHubClient.EditPullRequest(pullRequest.Number, prBody);
            }
        }
    }

    public static void UpdateStackPullRequestDescription(IInputProvider inputProvider, IStackConfig stackConfig, List<Config.Stack> stacks, Config.Stack stack)
    {
        var defaultStackDescription = stack.PullRequestDescription ?? $"This PR is part of a stack **{stack.Name}**:";
        var stackDescription = inputProvider.Text(Questions.PullRequestStackDescription, defaultStackDescription);

        if (stackDescription != stack.PullRequestDescription)
        {
            stack.SetPullRequestDescription(stackDescription);
            stackConfig.Save(stacks);
        }
    }

    static void MergeFromSourceBranch(string branch, string sourceBranchName, IGitClient gitClient, IInputProvider inputProvider, ILogger logger)
    {
        logger.Information($"Merging {sourceBranchName.Branch()} into {branch.Branch()}");
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
        ILogger logger)
    {
        //
        // When rebasing the stack, we'll use `git rebase --update-refs` from the
        // lowest branch in the stack to pick up changes throughout all branches in the stack.
        // Because there could be changes in any branch in the stack that aren't in the ones
        // below it, we'll repeat this all the way from the bottom to the top of the stack to
        // ensure that all changes are applied in the correct order.
        //
        // For example if we have a stack like this:
        // main -> feature1 -> feature2 -> feature3
        // 
        // We'll rebase feature3 onto feature2, then feature3 onto feature1, and finally feature3 onto main.
        //
        // In addition to this, if the stack is in a state where one of the branches has been squash merged
        // into the source branch, we'll want to rebase onto that branch directly using
        // `git rebase --onto {sourceBranch} {oldParentBranch}` to ensure that the changes are 
        // applied correctly and to try and avoid merge conflicts during the rebase.
        // 
        // For example if we have a stack like this:
        // main
        //   -> feature1 (deleted in remote): Squash merged into main
        //   -> feature2
        //   -> feature3
        //  
        // We'll rebase feature3 onto feature2 using a normal `git rebase feature2 --update-refs`, 
        // then feature3 onto main using `git rebase --onto main feature1 --update-refs` to replay
        // all commits from feature3 (and therefore from feature2) on top of the latest commits of main
        // which will include the squashed commit. 
        //
        string? branchToRebaseFrom = null;
        string? lowestInactiveBranchToReParentFrom = null;

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
            logger.Warning("No active branches found in the stack.");
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
                if (lowestInactiveBranchToReParentFrom is not null)
                {
                    RebaseOntoNewParent(branchToRebaseFrom, branchToRebaseOnto, lowestInactiveBranchToReParentFrom, gitClient, inputProvider, logger);
                }
                else
                {
                    RebaseFromSourceBranch(branchToRebaseFrom, branchToRebaseOnto, gitClient, inputProvider, logger);
                }
            }
            else if (lowestInactiveBranchToReParentFrom is null)
            {
                lowestInactiveBranchToReParentFrom = branchToRebaseOnto;
            }
        }
    }

    static void RebaseFromSourceBranch(string branch, string sourceBranchName, IGitClient gitClient, IInputProvider inputProvider, ILogger logger)
    {
        logger.Information($"Rebasing {branch.Branch()} onto {sourceBranchName.Branch()}");
        gitClient.ChangeBranch(branch);

        try
        {
            gitClient.RebaseFromLocalSourceBranch(sourceBranchName);
        }
        catch (ConflictException)
        {
            HandleConflictsDuringRebase(gitClient, inputProvider);
        }
    }

    static void RebaseOntoNewParent(
        string branch,
        string newParentBranchName,
        string oldParentBranchName,
        IGitClient gitClient,
        IInputProvider inputProvider,
        ILogger logger)
    {
        logger.Information($"Rebasing {branch.Branch()} onto new parent {newParentBranchName.Branch()}");
        gitClient.ChangeBranch(branch);

        try
        {
            gitClient.RebaseOntoNewParent(newParentBranchName, oldParentBranchName);
        }
        catch (ConflictException)
        {
            HandleConflictsDuringRebase(gitClient, inputProvider);
        }
    }

    static void HandleConflictsDuringRebase(IGitClient gitClient, IInputProvider inputProvider)
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
            gitClient.AbortRebase();
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
                HandleConflictsDuringRebase(gitClient, inputProvider);
            }
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