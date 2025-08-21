using Stack.Config;
using Stack.Infrastructure;
using Stack.Git;

namespace Stack.Commands.Helpers
{
    public interface IStackActions
    {
        void PullChanges(Config.Stack stack);
        void PushChanges(Config.Stack stack, int maxBatchSize, bool forceWithLease);
        void UpdateStack(Config.Stack stack, UpdateStrategy strategy);
    }

    public class StackActions(IGitClient gitClient, IGitHubClient gitHubClient, IInputProvider inputProvider, ILogger logger) : IStackActions
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
            var nonCurrentBranches = branchesNeedingUpdate
                .Where(b => !b.Equals(currentBranch, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (shouldPullCurrent)
            {
                logger.Information($"Pulling changes for {currentBranch.Branch()} from remote");
                gitClient.PullBranch(currentBranch);
            }

            if (nonCurrentBranches.Length > 0)
            {
                logger.Information($"Fetching changes for {string.Join(", ", nonCurrentBranches.Select(b => b.Branch()))} from remote");
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
                logger.Information($"Pushing new branch {branch.Branch()} to remote");
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
                logger.Information($"Pushing changes for {string.Join(", ", branches.Select(b => b.Branch()))} to remote");

                gitClient.PushBranches([.. branches], forceWithLease);
            }
        }

        public void UpdateStack(Config.Stack stack, UpdateStrategy strategy)
        {
            var currentBranch = gitClient.GetCurrentBranch();

            var status = StackHelpers.GetStackStatus(
                stack,
                currentBranch,
                logger,
                gitClient,
                gitHubClient,
                true);

            if (strategy == UpdateStrategy.Rebase)
            {
                UpdateStackUsingRebase(stack, status);
            }
            else
            {
                UpdateStackUsingMerge(stack, status);
            }
        }

        private void UpdateStackUsingMerge(
            Config.Stack stack,
            StackStatus status)
        {
            logger.Information($"Updating stack {status.Name.Stack()} using merge...");

            var allBranchLines = status.GetAllBranchLines();

            foreach (var branchLine in allBranchLines)
            {
                UpdateBranchLineUsingMerge(branchLine, status.SourceBranch);
            }
        }

        public void UpdateBranchLineUsingMerge(
            List<BranchDetail> branchLine,
            BranchDetailBase parentBranch)
        {
            var currentParentBranch = parentBranch;
            foreach (var branch in branchLine)
            {
                if (branch.IsActive)
                {
                    MergeFromSourceBranch(branch.Name, currentParentBranch.Name);
                    currentParentBranch = branch;
                }
                else
                {
                    logger.Debug($"Branch '{branch}' no longer exists on the remote repository or the associated pull request is no longer open. Skipping...");
                }
            }
        }

        private void MergeFromSourceBranch(string branch, string sourceBranchName)
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

        private void UpdateStackUsingRebase(
            Config.Stack stack,
            StackStatus status)
        {
            logger.Information($"Updating stack {status.Name.Stack()} using rebase...");

            var allBranchLines = status.GetAllBranchLines();

            foreach (var branchLine in allBranchLines)
            {
                UpdateBranchLineUsingRebase(status, branchLine);
            }
        }

        private void UpdateBranchLineUsingRebase(StackStatus status, List<BranchDetail> branchLine)
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
            logger.Information($"Rebasing stack {status.Name.Stack()} for branch line: {status.SourceBranch.Name.Branch()} --> {string.Join(" -> ", branchLine.Select(b => b.Name.Branch()))}");

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
                logger.Warning("No active branches found in the stack.");
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
                        RebaseOntoNewParent(branchToRebaseFrom, branchToRebaseOnto.Name, lowestInactiveBranchToReParentFrom!);
                    }
                    else
                    {
                        RebaseFromSourceBranch(branchToRebaseFrom, branchToRebaseOnto.Name);
                    }
                }
                else if (lowestInactiveBranchToReParentFrom is null)
                {
                    lowestInactiveBranchToReParentFrom = branchToRebaseOnto.Name;
                }
            }
        }

        private void RebaseFromSourceBranch(string branch, string sourceBranchName)
        {
            logger.Information($"Rebasing {branch.Branch()} onto {sourceBranchName.Branch()}");
            gitClient.ChangeBranch(branch);

            try
            {
                gitClient.RebaseFromLocalSourceBranch(sourceBranchName);
            }
            catch (ConflictException)
            {
                HandleConflictsDuringRebase();
            }
        }

        private void RebaseOntoNewParent(
            string branch,
            string newParentBranchName,
            string oldParentBranchName)
        {
            logger.Information($"Rebasing {branch.Branch()} onto new parent {newParentBranchName.Branch()}");
            gitClient.ChangeBranch(branch);

            try
            {
                gitClient.RebaseOntoNewParent(newParentBranchName, oldParentBranchName);
            }
            catch (ConflictException)
            {
                HandleConflictsDuringRebase();
            }
        }

        private void HandleConflictsDuringRebase()
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
                    HandleConflictsDuringRebase();
                }
            }
        }
    }
}
