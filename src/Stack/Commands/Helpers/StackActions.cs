using Stack.Config;
using Stack.Infrastructure;
using Stack.Git;

namespace Stack.Commands.Helpers
{
    public class StackActions : IStackActions
    {
        public UpdateStrategy? GetUpdateStrategyConfigValue(IGitClient gitClient)
        {
            return StackHelpers.GetUpdateStrategyConfigValue(gitClient);
        }

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
            StackStatus status,
            UpdateStrategy? specificUpdateStrategy,
            IGitClient gitClient,
            IInputProvider inputProvider,
            ILogger logger)
        {
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
