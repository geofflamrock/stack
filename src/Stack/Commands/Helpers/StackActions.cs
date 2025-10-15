using Microsoft.Extensions.Logging;
using Spectre.Console;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;
using Stack.Model;

namespace Stack.Commands.Helpers
{
    public interface IStackActions
    {
        void PullChanges(Model.Stack stack);
        void PushChanges(Model.Stack stack, int maxBatchSize, bool forceWithLease);
        Task UpdateStack(Model.Stack stack, UpdateStrategy strategy, CancellationToken cancellationToken, bool checkPullRequests = false);
    }


    public class StackActions(
        IGitClientFactory gitClientFactory,
        CliExecutionContext executionContext,
        IGitHubClient gitHubClient,
        ILogger<StackActions> logger,
        IDisplayProvider displayProvider,
        IConflictResolutionDetector conflictResolutionDetector) : IStackActions
    {
        /// <summary>
        /// Gets the default GitClient for the current working directory
        /// </summary>
        private IGitClient GetDefaultGitClient()
        {
            return gitClientFactory.Create(executionContext.WorkingDirectory);
        }

        /// <summary>
        /// Gets the appropriate GitClient for operating on a branch, either in the current working directory
        /// or in the branch's worktree if it's checked out in another worktree
        /// </summary>
        private IGitClient GetGitClientForBranch(string branchName, Dictionary<string, GitBranchStatus> branchStatuses)
        {
            if (branchStatuses.TryGetValue(branchName, out var branchStatus) && branchStatus.WorktreePath != null)
            {
                // Branch is checked out in another worktree, create a GitClient for that worktree
                return gitClientFactory.Create(branchStatus.WorktreePath);
            }

            // Use the default GitClient for the current working directory
            return GetDefaultGitClient();
        }

        public void PullChanges(Model.Stack stack)
        {
            var gitClient = GetDefaultGitClient();
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

                var branchGitClient = GetGitClientForBranch(branch, branchStatus);
                branchGitClient.PullBranch(branch);
            }

            if (nonCurrentBranches.Length > 0)
            {
                logger.FetchingNonCurrentBranches(string.Join(", ", nonCurrentBranches));
                gitClient.FetchBranchRefSpecs(nonCurrentBranches);
            }
        }

        public void PushChanges(
            Model.Stack stack,
            int maxBatchSize,
            bool forceWithLease)
        {
            var gitClient = GetDefaultGitClient();
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

        public async Task UpdateStack(Model.Stack stack, UpdateStrategy strategy, CancellationToken cancellationToken, bool checkPullRequests = false)
        {
            var gitClient = GetDefaultGitClient();

            List<string> allBranchesInStack = [stack.SourceBranch, .. stack.AllBranchNames];

            var branchStatuses = await displayProvider.DisplayStatus("Checking status of branches...", async ct =>
            {
                await Task.CompletedTask;
                return gitClient.GetBranchStatuses([.. allBranchesInStack]);
            }, cancellationToken);

            if (!branchStatuses.ContainsKey(stack.SourceBranch))
            {
                logger.SourceBranchDoesNotExist(stack.SourceBranch);
                return;
            }

            var pullRequests = new Dictionary<string, GitHubPullRequest?>();
            if (checkPullRequests)
            {
                gitHubClient.ThrowIfNotAvailable();

                await displayProvider.DisplayStatus("Checking status of pull requests...", async ct =>
                {
                    await Task.CompletedTask;
                    pullRequests = gitHubClient.GetPullRequests(stack.AllBranchNames);
                }, cancellationToken);
            }

            if (strategy == UpdateStrategy.Rebase)
            {
                await UpdateStackUsingRebase(stack, branchStatuses, pullRequests, cancellationToken);
            }
            else
            {
                await UpdateStackUsingMerge(stack, branchStatuses, pullRequests, cancellationToken);
            }
        }

        private async Task UpdateStackUsingMerge(
            Model.Stack stack,
            Dictionary<string, GitBranchStatus> branchStatuses,
            Dictionary<string, GitHubPullRequest?> pullRequests,
            CancellationToken cancellationToken)
        {
            logger.UpdatingStackUsingMerge(stack.Name);

            foreach (var branchLine in stack.GetAllBranchLines())
            {
                await UpdateBranchLineUsingMerge(branchLine, stack.SourceBranch, branchStatuses, pullRequests, cancellationToken);
            }
        }

        private async Task UpdateBranchLineUsingMerge(
            List<Branch> branchLine,
            string parentBranchName,
            Dictionary<string, GitBranchStatus> branchStatuses,
            Dictionary<string, GitHubPullRequest?> pullRequests,
            CancellationToken cancellationToken)
        {
            var currentParentBranch = parentBranchName;
            foreach (var branch in branchLine)
            {
                var branchState = GetBranchState(branch.Name, branchStatuses, pullRequests);
                if (branchState.IsActive)
                {
                    await MergeFromSourceBranch(branch.Name, currentParentBranch, branchStatuses, cancellationToken);
                    currentParentBranch = branch.Name;
                }
                else
                {
                    logger.TraceSkippingInactiveBranch(branch.Name);
                }
            }
        }

        private async Task MergeFromSourceBranch(string branch, string sourceBranchName, Dictionary<string, GitBranchStatus> branchStatuses, CancellationToken cancellationToken)
        {
            logger.MergingBranch(sourceBranchName, branch);

            var branchGitClient = GetGitClientForBranch(branch, branchStatuses);
            branchGitClient.ChangeBranch(branch);

            try
            {
                branchGitClient.MergeFromLocalSourceBranch(sourceBranchName);
            }
            catch (ConflictException)
            {
                var result = await conflictResolutionDetector.WaitForConflictResolution(
                    branchGitClient,
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
                        throw new Exception("Expected merge to be in progress but it is not. Use --verbose output for more details.");
                }
            }
        }

        private async Task UpdateStackUsingRebase(
            Model.Stack stack,
            Dictionary<string, GitBranchStatus> branchStatuses,
            Dictionary<string, GitHubPullRequest?> pullRequests,
            CancellationToken cancellationToken)
        {
            logger.UpdatingStackUsingRebase(stack.Name);

            foreach (var branchLine in stack.GetAllBranchLines())
            {
                await UpdateBranchLineUsingRebase(stack.Name, stack.SourceBranch, branchLine, branchStatuses, pullRequests, cancellationToken);
            }
        }

        private async Task UpdateBranchLineUsingRebase(
            string stackName,
            string sourceBranchName,
            List<Branch> branchLine,
            Dictionary<string, GitBranchStatus> branchStatuses,
            Dictionary<string, GitHubPullRequest?> pullRequests,
            CancellationToken cancellationToken)
        {
            //
            // When rebasing the stack, we need to be able to pick up changes at each level of the stack.
            // 
            // We need to handle a few specific scenarios:
            // - When one of the branches has been squash merged into the source branch.
            // - When a branch has additional commits that weren't rebased into children before merging into the source branch.
            //
            // The approach we'll take it is to rebase each branch at each level of the stack against
            // all active branches above it in the stack. 
            //
            // # Squash merges
            //
            // Squash merges are tricky:
            // - The branch that was squash merged will have one or more commits that are going to squashed into the source branch.
            // - Child branches will also have these commits.
            // - When the parent branch is squash merged, the child branches will still have the set of commits that 
            //   are equal to the contents of the squashed commit on the source branch.
            // - When we try and rebase the child branch we hit conflicts as we try and re-apply all
            //   the individual commits. If the commit happens to exactly match the final squashed commit it 
            //   might work, but this is unlikely in practice, especially if the branch had multiple commits.
            //
            // We can detect if a branch has been squash merged into the source branch by:
            // - Finding the common base between the branch we're rebasing and it's parent branch that was squash merged.
            //    - Finding the common base handles the case when additional commits were made to the branch before it was squash merged.
            // - Checking if that common base exists in the source branch. If it doesn't, then we know that it was squash merged.
            //
            // If we find that a branch was squash merged, we rebase directly onto the source branch, telling Git to start
            // from the common base to ignore commits up to that point:
            // `git rebase --onto {sourceBranch} {commonBaseBetweenChildAndOldParentBranch}`
            //
            // # Example
            //
            // With the following stack:
            // 
            // main 
            //   |-feature1 (deleted in remote - squash merged into main)
            //     |- feature2
            //       |- feature3
            //       |- feature4
            //     |- feature5
            //
            // We'll rebase in the following order:
            // 
            // - feature2 onto main (re-parenting as feature1 was squash merged)
            // - feature3 onto main (re-parenting as feature1 was squash merged)
            // - feature3 onto feature2
            // - feature4 onto main (re-parenting as feature1 was squash merged)
            // - feature4 onto feature2
            // - feature5 onto main (re-parenting as feature1 was squash merged)
            //
            logger.RebasingStackForBranchLine(stackName, sourceBranchName, string.Join(" -> ", branchLine.Select(b => b.Name)));
            List<BranchState> allBranchesInLine = [GetBranchState(sourceBranchName, branchStatuses, pullRequests), .. branchLine.Select(b => GetBranchState(b.Name, branchStatuses, pullRequests))];

            foreach (var branch in branchLine)
            {
                var branchState = allBranchesInLine.First(b => b.Name == branch.Name);

                if (!branchState.IsActive)
                {
                    logger.TraceSkippingInactiveBranch(branch.Name);
                    continue;
                }

                string? lowestInactiveBranchToReParentFrom = null;
                var branchesToRebaseOnto = new List<BranchState>();

                // Find all active branches above this one to 
                // rebase onto. Also work out if there is any that
                // are inactive that we need to re-parent from in
                // order to handle squash merges.
                foreach (var branchToRebaseOnto in allBranchesInLine)
                {
                    if (branchToRebaseOnto.Name == branch.Name)
                    {
                        break;
                    }

                    if (branchToRebaseOnto.IsActive)
                    {
                        branchesToRebaseOnto.Add(branchToRebaseOnto);
                    }
                    else if (lowestInactiveBranchToReParentFrom is null)
                    {
                        lowestInactiveBranchToReParentFrom = branchToRebaseOnto.Name;
                    }
                }

                foreach (var branchToRebaseOnto in branchesToRebaseOnto)
                {
                    BranchState? lowestInactiveBranchToReParentFromDetail = lowestInactiveBranchToReParentFrom is not null
                        ? allBranchesInLine.First(b => b.Name == lowestInactiveBranchToReParentFrom)
                        : null;
                    var couldRebaseOntoParent = lowestInactiveBranchToReParentFromDetail is { Exists: true };
                    var parentCommitToRebaseFrom = couldRebaseOntoParent ? GetCommitShaToReParentFrom(branch.Name, lowestInactiveBranchToReParentFrom!, branchToRebaseOnto.Name) : null;

                    if (parentCommitToRebaseFrom is not null)
                    {
                        await RebaseOntoNewParent(branch.Name, branchToRebaseOnto.Name, parentCommitToRebaseFrom, branchStatuses, cancellationToken);
                    }
                    else
                    {
                        await RebaseFromSourceBranch(branch.Name, branchToRebaseOnto.Name, branchStatuses, cancellationToken);
                    }
                }
            }
        }

        private static BranchState GetBranchState(string branchName, Dictionary<string, GitBranchStatus> branchStatuses, Dictionary<string, GitHubPullRequest?>? pullRequests)
        {
            return new BranchState(branchName, branchStatuses.GetValueOrDefault(branchName), pullRequests?.GetValueOrDefault(branchName));
        }

        private string? GetCommitShaToReParentFrom(string branchToRebase, string lowestInactiveBranchToReParentFrom, string branchToRebaseOnto)
        {
            var gitClient = GetDefaultGitClient();

            // Get the common base between the branch we're rebasing and
            // the branch we could potentially re-parent from.
            var commonBase = gitClient.GetMergeBase(branchToRebase, lowestInactiveBranchToReParentFrom);

            if (commonBase is null)
            {
                // This should never happen, but if it does, we can't re-parent
                // so we'll just rebase from the source branch instead.
                return null;
            }

            logger.CommonBaseBetweenBranches(branchToRebase, lowestInactiveBranchToReParentFrom, commonBase);

            // Now check if the common base exists in the branch we're rebasing onto
            // If it does, then we know that it got merged in. If it doesn't,
            // then we know that it got squash merged in, so we should re-parent
            // onto the new parent branch instead.
            var commonBaseExistsInBranchBeingRebasedOnto = gitClient.IsCommitReachableFromBranch(commonBase, branchToRebaseOnto);

            if (!commonBaseExistsInBranchBeingRebasedOnto)
            {
                logger.CommitDoesNotExistInNewParent(commonBase, branchToRebaseOnto);

                // Common base doesn't exist in the branch we're rebasing onto,
                // so we know that it got squash merged in. We can re-parent.
                return commonBase;
            }
            else
            {
                logger.CommitExistsInNewParent(commonBase, branchToRebaseOnto);
                return null;
            }
        }

        private async Task RebaseFromSourceBranch(string branch, string sourceBranchName, Dictionary<string, GitBranchStatus> branchStatuses, CancellationToken cancellationToken)
        {
            await displayProvider.DisplayStatusWithSuccess($"Rebasing {branch} onto {sourceBranchName}", async ct =>
            {
                var branchGitClient = GetGitClientForBranch(branch, branchStatuses);
                branchGitClient.ChangeBranch(branch);

                try
                {
                    branchGitClient.RebaseFromLocalSourceBranch(sourceBranchName);
                }
                catch (ConflictException)
                {
                    var result = await conflictResolutionDetector.WaitForConflictResolution(
                        branchGitClient,
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
                            throw new Exception("Expected rebase to be in progress but it is not. Use --verbose output for more details.");
                    }
                }
            }, cancellationToken);
        }

        private async Task RebaseOntoNewParent(
            string branch,
            string newParentBranchName,
            string oldParentCommitSha,
            Dictionary<string, GitBranchStatus> branchStatuses,
            CancellationToken cancellationToken)
        {
            await displayProvider.DisplayStatusWithSuccess($"Rebasing {branch} onto new parent {newParentBranchName}", async ct =>
            {
                var branchGitClient = GetGitClientForBranch(branch, branchStatuses);
                branchGitClient.ChangeBranch(branch);

                try
                {
                    branchGitClient.RebaseOntoNewParent(newParentBranchName, oldParentCommitSha);
                }
                catch (ConflictException)
                {
                    var result = await conflictResolutionDetector.WaitForConflictResolution(
                        branchGitClient,
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
                            throw new Exception("Expected rebase to be in progress but it is not. Use --verbose output for more details.");
                    }
                }
            }, cancellationToken);
        }
        private readonly record struct BranchState(string Name, GitBranchStatus? BranchStatus, GitHubPullRequest? PullRequest)
        {
            public bool Exists => BranchStatus is not null;
            public bool RemoteTrackingBranchExists => BranchStatus?.RemoteBranchExists ?? false;
            public bool IsActive
            {
                get
                {
                    if (BranchStatus is null)
                    {
                        return false;
                    }

                    if (BranchStatus.RemoteTrackingBranchName is null)
                    {
                        // Branch has never been pushed to remote, consider it active
                        return true;
                    }

                    if (!RemoteTrackingBranchExists)
                    {
                        // Remote tracking branch doesn't exist, consider it inactive
                        return false;
                    }

                    // If there's no associated pull request, or if the pull request is not merged, consider it active
                    return PullRequest is null || PullRequest.State != GitHubPullRequestStates.Merged;
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Source branch \"{SourceBranch}\" does not exist locally. Skipping update.")]
    public static partial void SourceBranchDoesNotExist(this ILogger logger, string sourceBranch);

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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Common base between {SourceBranch} and {TargetBranch}: {CommitSha}")]
    public static partial void CommonBaseBetweenBranches(this ILogger logger, string sourceBranch, string targetBranch, string commitSha);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Commit {CommitSha} does not exist in branch {BranchToRebaseOnto}, treating previous parent as being squash merged and re-parenting.")]
    public static partial void CommitDoesNotExistInNewParent(this ILogger logger, string commitSha, string branchToRebaseOnto);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Commit {CommitSha} exists in branch {BranchToRebaseOnto}, no need to re-parent")]
    public static partial void CommitExistsInNewParent(this ILogger logger, string commitSha, string branchToRebaseOnto);
}
