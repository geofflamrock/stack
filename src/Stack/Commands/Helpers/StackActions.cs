using Stack.Config;
using Stack.Infrastructure;
using Stack.Git;

namespace Stack.Commands.Helpers
{
    public class StackActions : IRemoteStackActions, ILocalStackActions
    {
        public void PullChanges(
            Config.Stack stack,
            IGitClient gitClient,
            ILogger logger)
        {
            StackHelpers.PullChanges(stack, gitClient, logger);
        }

        public void PushChanges(
            Config.Stack stack,
            int maxBatchSize,
            bool forceWithLease,
            IGitClient gitClient,
            ILogger logger)
        {
            StackHelpers.PushChanges(stack, maxBatchSize, forceWithLease, gitClient, logger);
        }

        public UpdateStrategy UpdateStack(
            Config.Stack stack,
            UpdateStrategy? specificUpdateStrategy,
            IGitClient gitClient,
            IInputProvider inputProvider,
            ILogger logger)
        {
            var currentBranch = gitClient.GetCurrentBranch();
            var status = StackHelpers.GetStackStatus(
                stack,
                currentBranch,
                logger,
                gitClient,
                // Try to get a default IGitHubClient if needed, or pass null if not used
                null,
                true);

            return StackHelpers.UpdateStack(
                stack,
                status,
                specificUpdateStrategy,
                gitClient,
                inputProvider,
                logger);
        }
    }
}
