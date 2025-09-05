using Stack.Config;
using Stack.Infrastructure;
using Stack.Git;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Stack.Commands.Helpers
{
    public interface IStackActions
    {
        void PullChanges(Config.Stack stack);
        void PushChanges(Config.Stack stack, int maxBatchSize, bool forceWithLease);
        Task UpdateStack(Config.Stack stack, UpdateStrategy strategy, CancellationToken cancellationToken);
    }

    public class StackActions(
        IGitClient gitClient,
        IGitHubClient gitHubClient,
        ILogger<StackActions> logger,
        IDisplayProvider displayProvider) : IStackActions
    {
        public void PullChanges(Config.Stack stack)
        {
            List<string> allBranchesInStacks = [stack.SourceBranch, .. stack.AllBranchNames];
            var branchStatus = gitClient.GetBranchStatuses([.. allBranchesInStacks]);

            var currentBranch = gitClient.GetCurrentBranch();

            var branchesNeedingUpdate = allBranchesInStacks
                .Where(b => branchStatus.ContainsKey(b)
                            && branchStatus[b].RemoteBranchExists
                            && branchStatus[b].Behind > 0)
                .ToList();

            if (branchesNeedingUpdate.Count == 0)
            {
                return;
            }

            // Pull the current branch and fetch ref-specs for the others
            var shouldPullCurrent = branchesNeedingUpdate.Contains(currentBranch);
            // Identify branches that are behind and checked out in another worktree (marker '+') so can be pulled directly
            var branchesInOtherWorktrees = branchesNeedingUpdate
                .Where(b =>
                    branchStatus[b].IsCurrentBranch == false &&
                    !b.Equals(currentBranch, StringComparison.OrdinalIgnoreCase) &&
                    branchStatus[b].WorktreePath is not null)
                .ToArray();

            var nonCurrentBranches = branchesNeedingUpdate
                .Where(b => !b.Equals(currentBranch, StringComparison.OrdinalIgnoreCase) && !branchesInOtherWorktrees.Contains(b))
                .ToArray();

            if (shouldPullCurrent)
            {
                logger.PullingCurrentBranch(currentBranch);
                gitClient.PullBranch(currentBranch);
            }

            // Pull branches that are in other worktrees directly
            foreach (var branch in branchesInOtherWorktrees)
            {
                var worktreePath = branchStatus[branch].WorktreePath!; // not null due to filter
                logger.PullingWorktreeBranch(branch, worktreePath);
                gitClient.PullBranchForWorktree(branch, worktreePath);
            }

            if (nonCurrentBranches.Length > 0)
            {
                logger.FetchingNonCurrentBranches(string.Join(", ", nonCurrentBranches));
                gitClient.FetchBranchRefSpecs(nonCurrentBranches);
            }
        }

        public void PushChanges(
            Config.Stack stack,
            int maxBatchSize,
            bool forceWithLease)
        {
            var branchStatus = gitClient.GetBranchStatuses([.. stack.AllBranchNames]);

            var branchesThatHaveNotBeenPushedToRemote = branchStatus
                .Where(b => b.Value.RemoteTrackingBranchName is null)
                .Select(b => b.Value.BranchName)
                .ToList();

            foreach (var branch in branchesThatHaveNotBeenPushedToRemote)
            {
                logger.PushingNewBranch(branch);
                gitClient.PushNewBranch(branch);
            }

            var branchesThatAreAheadOfTheRemote = branchStatus
                .Where(b => b.Value.RemoteBranchExists && b.Value.Ahead > 0)
                .Select(b => b.Value.BranchName)
                .ToList();

            var branchGroupsToPush = branchesThatAreAheadOfTheRemote
                .Select((b, i) => new { Index = i, Value = b })
                .GroupBy(b => b.Index / maxBatchSize)
                .Select(g => g.Select(b => b.Value).ToList())
                .ToList();

            foreach (var branches in branchGroupsToPush)
            {
                logger.PushingBranches(string.Join(", ", branches));
                gitClient.PushBranches([.. branches], forceWithLease);
            }
        }

        public async Task UpdateStack(Config.Stack stack, UpdateStrategy strategy, CancellationToken cancellationToken)
        {
            var currentBranch = gitClient.GetCurrentBranch();

            var status = StackHelpers.GetStackStatus(
                stack,
                currentBranch,
                logger,
                displayProvider,
                gitClient,
                gitHubClient,
                true);

            if (strategy == UpdateStrategy.Rebase)
            {
                await UpdateStackUsingRebase(stack, status, cancellationToken);
            }
            else
            {
                await UpdateStackUsingMerge(stack, status, cancellationToken);
            }
        }

        private async Task UpdateStackUsingMerge(
            Config.Stack stack,
            StackStatus status,
            CancellationToken cancellationToken)
        {
            logger.UpdatingStackUsingMerge(status.Name);

            var allBranchLines = status.GetAllBranchLines();

            foreach (var branchLine in allBranchLines)
            {
                await UpdateBranchLineUsingMerge(branchLine, status.SourceBranch, cancellationToken);
            }
        }

        private async Task UpdateBranchLineUsingMerge(
            List<BranchDetail> branchLine,
            BranchDetailBase parentBranch,
            CancellationToken cancellationToken)
        {
            var currentParentBranch = parentBranch;
            foreach (var branch in branchLine)
            {
                if (branch.IsActive)
                {
                    await MergeFromSourceBranch(branch.Name, currentParentBranch.Name, cancellationToken);
                    currentParentBranch = branch;
                }
                else
                {
                    logger.TraceSkippingInactiveBranch(branch.Name);
                }
            }
        }

        private async Task MergeFromSourceBranch(string branch, string sourceBranchName, CancellationToken cancellationToken)
        {
            logger.MergingBranch(sourceBranchName, branch);
            gitClient.ChangeBranch(branch);

            try
            {
                gitClient.MergeFromLocalSourceBranch(sourceBranchName);
            }
            catch (ConflictException)
            {
                var result = await ConflictResolutionDetector.WaitForConflictResolution(
                    gitClient,
                    logger,
                    ConflictOperationType.Merge,
                    TimeSpan.FromSeconds(1),
                    null,
                    cancellationToken);

                switch (result)
                {
                    case ConflictResolutionResult.Completed:
                        break; // proceed
                    case ConflictResolutionResult.Aborted:
                        throw new Exception("Merge aborted due to conflicts.");
                    case ConflictResolutionResult.Timeout:
                        throw new TimeoutException("Timed out waiting for merge conflict resolution.");
                    case ConflictResolutionResult.NotStarted:
                        logger.LogWarning("Expected merge to be in progress but marker not found. Proceeding cautiously.");
                        break;
                }
            }
        }

        private async Task UpdateStackUsingRebase(
            Config.Stack stack,
            StackStatus status,
            CancellationToken cancellationToken)
        {
            logger.UpdatingStackUsingRebase(status.Name);

            var allBranchLines = status.GetAllBranchLines();

            foreach (var branchLine in allBranchLines)
            {
                await UpdateBranchLineUsingRebase(status, branchLine, cancellationToken);
            }
        }

        private async Task UpdateBranchLineUsingRebase(StackStatus status, List<BranchDetail> branchLine, CancellationToken cancellationToken)
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
            logger.RebasingStackForBranchLine(status.Name, status.SourceBranch.Name, string.Join(" -> ", branchLine.Select(b => b.Name)));

            BranchDetail? lowestActionBranch = null;
            foreach (var branch in branchLine)
            {
                if (branch.IsActive)
                {
                    lowestActionBranch = branch;
                }
            }

            if (lowestActionBranch is null)
            {
                logger.NoActiveBranchesFound();
                return;
            }

            string? branchToRebaseFrom = lowestActionBranch.Name;
            string? lowestInactiveBranchToReParentFrom = null;

            List<BranchDetailBase> branchesToRebaseOnto = [.. branchLine];
            branchesToRebaseOnto.Reverse();
            branchesToRebaseOnto.Remove(lowestActionBranch);
            branchesToRebaseOnto.Add(status.SourceBranch);

            List<BranchDetailBase> allBranchesInStack = [status.SourceBranch, .. branchLine];

            foreach (var branchToRebaseOnto in branchesToRebaseOnto)
            {
                if (branchToRebaseOnto.IsActive)
                {
                    var lowestInactiveBranchToReParentFromDetail = lowestInactiveBranchToReParentFrom is not null ? allBranchesInStack.First(b => b.Name == lowestInactiveBranchToReParentFrom) : null;
                    var shouldRebaseOntoParent = lowestInactiveBranchToReParentFromDetail is not null && lowestInactiveBranchToReParentFromDetail.Exists;

                    if (shouldRebaseOntoParent)
                    {
                        shouldRebaseOntoParent = gitClient.IsAncestor(branchToRebaseFrom, lowestInactiveBranchToReParentFrom!);
                    }

                    if (shouldRebaseOntoParent)
                    {
                        await RebaseOntoNewParent(branchToRebaseFrom, branchToRebaseOnto.Name, lowestInactiveBranchToReParentFrom!, cancellationToken);
                    }
                    else
                    {
                        await RebaseFromSourceBranch(branchToRebaseFrom, branchToRebaseOnto.Name, cancellationToken);
                    }
                }
                else if (lowestInactiveBranchToReParentFrom is null)
                {
                    lowestInactiveBranchToReParentFrom = branchToRebaseOnto.Name;
                }
            }
        }

        private async Task RebaseFromSourceBranch(string branch, string sourceBranchName, CancellationToken cancellationToken)
        {
            await displayProvider.DisplayStatus($"Rebasing {branch} onto {sourceBranchName}", async ct =>
            {
                gitClient.ChangeBranch(branch);

                try
                {
                    gitClient.RebaseFromLocalSourceBranch(sourceBranchName);
                }
                catch (ConflictException)
                {
                    var result = await ConflictResolutionDetector.WaitForConflictResolution(
                        gitClient,
                        logger,
                        ConflictOperationType.Rebase,
                        TimeSpan.FromSeconds(1),
                        null,
                        cancellationToken);

                    switch (result)
                    {
                        case ConflictResolutionResult.Completed:
                            break;
                        case ConflictResolutionResult.Aborted:
                            throw new Exception("Rebase aborted due to conflicts.");
                        case ConflictResolutionResult.Timeout:
                            throw new TimeoutException("Timed out waiting for rebase conflict resolution.");
                        case ConflictResolutionResult.NotStarted:
                            logger.LogWarning("Expected rebase to be in progress but marker not found. Proceeding cautiously.");
                            break;
                    }
                }

                await displayProvider.DisplayMessage($"{Emoji.Known.CheckMark}  Rebasing {branch} onto {sourceBranchName}...", ct);
            }, cancellationToken);
        }

        private async Task RebaseOntoNewParent(
            string branch,
            string newParentBranchName,
            string oldParentBranchName,
            CancellationToken cancellationToken)
        {
            logger.RebasingBranchOntoNewParent(branch, newParentBranchName);
            gitClient.ChangeBranch(branch);

            try
            {
                gitClient.RebaseOntoNewParent(newParentBranchName, oldParentBranchName);
            }
            catch (ConflictException)
            {
                var result = await ConflictResolutionDetector.WaitForConflictResolution(
                    gitClient,
                    logger,
                    ConflictOperationType.Rebase,
                    TimeSpan.FromSeconds(1),
                    null,
                    cancellationToken);

                switch (result)
                {
                    case ConflictResolutionResult.Completed:
                        break;
                    case ConflictResolutionResult.Aborted:
                        throw new Exception("Rebase aborted due to conflicts.");
                    case ConflictResolutionResult.Timeout:
                        throw new TimeoutException("Timed out waiting for rebase conflict resolution.");
                    case ConflictResolutionResult.NotStarted:
                        logger.LogWarning("Expected rebase to be in progress but marker not found. Proceeding cautiously.");
                        break;
                }
            }
        }
    }
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Pulling changes for {Branch} from remote")]
    public static partial void PullingCurrentBranch(this ILogger logger, string branch);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pulling changes for {Branch} (worktree: \"{WorktreePath}\") from remote")]
    public static partial void PullingWorktreeBranch(this ILogger logger, string branch, string worktreePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fetching changes for {Branches} from remote")]
    public static partial void FetchingNonCurrentBranches(this ILogger logger, string branches);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pushing new branch {Branch} to remote")]
    public static partial void PushingNewBranch(this ILogger logger, string branch);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pushing changes for {Branches} to remote")]
    public static partial void PushingBranches(this ILogger logger, string branches);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Updating stack \"{Stack}\" using merge...")]
    public static partial void UpdatingStackUsingMerge(this ILogger logger, string stack);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Branch {Branch} no longer exists on the remote repository or the associated pull request is no longer open. Skipping...")]
    public static partial void TraceSkippingInactiveBranch(this ILogger logger, string branch);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Merging {SourceBranch} into {Branch}")]
    public static partial void MergingBranch(this ILogger logger, string sourceBranch, string branch);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Updating stack \"{Stack}\" using rebase...")]
    public static partial void UpdatingStackUsingRebase(this ILogger logger, string stack);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rebasing stack \"{Stack}\" for branch line: {SourceBranch} --> {BranchLine}")]
    public static partial void RebasingStackForBranchLine(this ILogger logger, string stack, string sourceBranch, string branchLine);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No active branches found for branch line.")]
    public static partial void NoActiveBranchesFound(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rebasing {Branch} onto {SourceBranch}")]
    public static partial void RebasingBranchOnto(this ILogger logger, string branch, string sourceBranch);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rebasing {Branch} onto new parent {NewParentBranch}")]
    public static partial void RebasingBranchOntoNewParent(this ILogger logger, string branch, string newParentBranch);
}
