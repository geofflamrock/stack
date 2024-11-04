using Spectre.Console;

namespace Stack.Commands;

internal static class Prompts
{
    internal static IPrompt<string> Stack(List<Config.Stack> stacks)
    {
        return new SelectionPrompt<string>().Title("Select stack:").PageSize(10).AddChoices(stacks.Select(s => s.Name).ToArray());
    }

    internal static IPrompt<string> Branch(string[] branches, string? title = null)
    {
        return new SelectionPrompt<string>().Title(title ?? "Select branch:").PageSize(10).AddChoices(branches);
    }
}