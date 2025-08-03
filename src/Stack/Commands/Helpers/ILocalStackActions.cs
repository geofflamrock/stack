using Stack.Config;
using Stack.Infrastructure;
using Stack.Git;

namespace Stack.Commands.Helpers
{
    public interface ILocalStackActions
    {
        UpdateStrategy UpdateStack(
            Config.Stack stack,
            StackStatus status,
            UpdateStrategy? specificUpdateStrategy,
            IGitClient gitClient,
            IInputProvider inputProvider,
            ILogger logger);

        UpdateStrategy? GetUpdateStrategyConfigValue(IGitClient gitClient);
    }
}
