using Spectre.Console;
using Stack.Config;

namespace Stack.Infrastructure;

public static class InputProviderExtensionMethods
{
    const string SelectStackPrompt = "Select stack:";

    public static string SelectStack(this IInputProvider inputProvider, List<Config.Stack> stacks, string currentBranch)
    {
        return inputProvider.Select(SelectStackPrompt, stacks.OrderByCurrentStackThenByName(currentBranch).Select(s => s.Name).ToArray());
    }
}
