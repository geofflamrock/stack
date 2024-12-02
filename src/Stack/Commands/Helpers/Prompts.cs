using Spectre.Console;
using Stack.Config;

namespace Stack.Commands;

public static class Prompts
{
    public static IPrompt<string> Stack(List<Models.Stack> stacks, string currentBranch)
    {
        return new SelectionPrompt<string>().Title("Select stack:")
            .PageSize(10)
            .AddChoices(stacks.OrderByCurrentStackThenByName(currentBranch).Select(s => s.Name).ToArray());
    }

    public static IPrompt<string> Branch(string[] branches, string? title = null)
    {
        return new SelectionPrompt<string>().Title(title ?? "Select branch:").PageSize(10).AddChoices(branches);
    }
}