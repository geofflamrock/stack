using Stack.Config;
using Stack.Infrastructure;
using Stack.Git;

namespace Stack.Commands.Helpers
{
    public class StackActions(IGitClient gitClient, IGitHubClient gitHubClient, IInputProvider inputProvider, ILogger logger) : IStackActions
    {
        private readonly IGitClient gitClient = gitClient;
        private readonly IGitHubClient gitHubClient = gitHubClient;
        private readonly IInputProvider inputProvider = inputProvider;
        private readonly ILogger logger = logger;

        public UpdateStrategy? GetUpdateStrategyConfigValue()
        {
            return StackHelpers.GetUpdateStrategyConfigValue(gitClient);
        }

        public void PullChanges(Config.Stack stack)
        {
            StackHelpers.PullChanges(stack, gitClient, logger);
        }

        public void PushChanges(
            Config.Stack stack,
            int maxBatchSize,
            bool forceWithLease)
        {
            StackHelpers.PushChanges(stack, maxBatchSize, forceWithLease, gitClient, logger);
        }

        public UpdateStrategy UpdateStack(
            Config.Stack stack,
            UpdateStrategy? specificUpdateStrategy)
        {
            var currentBranch = gitClient.GetCurrentBranch();

            var status = StackHelpers.GetStackStatus(
                stack,
                currentBranch,
                logger,
                gitClient,
                gitHubClient);

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
