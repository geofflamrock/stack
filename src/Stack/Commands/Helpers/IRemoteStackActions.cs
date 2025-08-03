using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands.Helpers
{
    public interface IRemoteStackActions
    {
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
