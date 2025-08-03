using Stack.Config;
using Stack.Infrastructure;
using Stack.Git;

namespace Stack.Commands.Helpers
{
    public interface IStackActions
    {
        UpdateStrategy UpdateStack(
            Config.Stack stack,
            StackStatus status,
            UpdateStrategy? specificUpdateStrategy,
            IGitClient gitClient,
            IInputProvider inputProvider,
            ILogger logger);

        UpdateStrategy? GetUpdateStrategyConfigValue(IGitClient gitClient);

        void PullChanges(
            Config.Stack stack,
            IGitClient gitClient,
            ILogger logger);

        void PushChanges(
            Config.Stack stack,
            int maxBatchSize,
            bool forceWithLease,
            IGitClient gitClient,
            ILogger logger);
    }
}
