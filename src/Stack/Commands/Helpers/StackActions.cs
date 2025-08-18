using Stack.Config;
using Stack.Infrastructure;
using Stack.Git;

namespace Stack.Commands.Helpers
{
    public interface IStackActions
    {
        void PullChanges(Config.Stack stack);
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
    }
}
