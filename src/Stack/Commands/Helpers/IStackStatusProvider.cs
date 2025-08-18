using Stack.Config;

namespace Stack.Commands.Helpers;

public interface IStackStatusProvider
{
    List<StackStatus> GetStackStatus(List<Config.Stack> stacks, string currentBranch, bool includePullRequestStatus = true);

    StackStatus GetStackStatus(Config.Stack stack, string currentBranch, bool includePullRequestStatus = true);
}
