using Stack.Config;
using Stack.Infrastructure;
using Stack.Git;

namespace Stack.Commands.Helpers
{
    public interface IStackActions
    {
        void PullChanges(Config.Stack stack);
        void PushChanges(Config.Stack stack, int maxBatchSize, bool forceWithLease);
    }

    public class StackActions(IGitClient gitClient, ILogger logger) : IStackActions
    {
        public void PullChanges(Config.Stack stack)
        {
            List<string> allBranchesInStacks = [stack.SourceBranch, .. stack.AllBranchNames];
            var branchStatus = gitClient.GetBranchStatuses([.. allBranchesInStacks]);

            foreach (var branch in allBranchesInStacks
                .Where(b =>
                    branchStatus.ContainsKey(b) &&
                    branchStatus[b].RemoteBranchExists &&
                    branchStatus[b].Behind > 0))
            {
                logger.Information($"Pulling changes for {branch.Branch()} from remote");
                gitClient.ChangeBranch(branch);
                gitClient.PullBranch(branch);
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
    }
}
