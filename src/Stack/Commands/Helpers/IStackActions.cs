using Stack.Config;
using Stack.Infrastructure;
using Stack.Git;

namespace Stack.Commands.Helpers
{
    public interface IStackActions
    {
        void UpdateStack(Config.Stack stack, UpdateStrategy updateStrategy);

        UpdateStrategy? GetUpdateStrategyConfigValue();

        void PullChanges(Config.Stack stack);

        void PushChanges(
            Config.Stack stack,
            int maxBatchSize,
            bool forceWithLease);
    }
}
