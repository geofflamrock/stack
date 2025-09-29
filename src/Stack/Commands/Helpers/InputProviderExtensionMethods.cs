using Microsoft.Extensions.Logging;
using Spectre.Console;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Infrastructure;

namespace Stack.Commands.Helpers;

public static class InputProviderExtensionMethods
{
    public static async Task<string> Text(
        this IInputProvider inputProvider,
        ILogger logger,
        string prompt,
        string? presetValue,
        CancellationToken cancellationToken)
    {
        if (presetValue is not null)
        {
            logger.Answer(prompt, presetValue);

            return presetValue;
        }

        return await inputProvider.Text(prompt, cancellationToken);
    }

    public static async Task<string> Select(
        this IInputProvider inputProvider,
        ILogger logger,
        string prompt,
        string? presetValue,
        string[] choices,
        CancellationToken cancellationToken)
    {
        var selection = presetValue ?? await inputProvider.Select(prompt, choices, cancellationToken);

        logger.Answer(prompt, selection);

        return selection;
    }

    public static async Task<string[]> MultiSelect(
        this IInputProvider inputProvider,
        ILogger logger,
        string prompt,
        string[] choices,
        bool required,
        CancellationToken cancellationToken,
        string[]? presetValues = null)
    {
        var selection = presetValues ?? (await inputProvider.MultiSelect(prompt, choices, required, cancellationToken)).ToArray();

        logger.Answer(prompt, string.Join(", ", selection));

        return [.. selection];
    }

    public static async Task<Config.Stack?> SelectStack(
        this IInputProvider inputProvider,
        ILogger logger,
        string? name,
        List<Config.Stack> stacks,
        string currentBranch,
        CancellationToken cancellationToken)
    {
        if (stacks.Count == 0)
        {
            return null;
        }

        if (name is not null)
        {
            return stacks.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        if (stacks.Count == 1)
        {
            return stacks.First();
        }

        var stacksContainingCurrentBranch = stacks.Where(s => s.IsCurrentStack(currentBranch)).ToList();

        if (stacksContainingCurrentBranch.Count == 1)
        {
            return stacksContainingCurrentBranch.First();
        }

        var stackNames = stacks.OrderByCurrentStackThenByName(currentBranch).Select(s => s.Name).ToArray();
        var stackSelection = await inputProvider.Select(Questions.SelectStack, stackNames, cancellationToken);
        var stack = stacks.FirstOrDefault(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        if (stack is not null)
        {
            logger.Answer(Questions.SelectStack, stack.Name);
        }

        return stack;
    }

    public static Task<string> SelectBranch(
        this IInputProvider inputProvider,
        ILogger logger,
        string? name,
        string[] branches,
        CancellationToken cancellationToken)
    {
        return inputProvider.Select(logger, Questions.SelectBranch, name, branches, cancellationToken);
    }

    public static async Task<string> SelectBranch(
        this IInputProvider inputProvider,
        ILogger logger,
        string? name,
        Config.Stack stack,
        CancellationToken cancellationToken)
    {
        void GetBranchNamesWithIndentation(Branch branch, List<string> names, int level = 0)
        {
            names.Add($"{new string(' ', level * 2)}{branch.Name}");
            foreach (var child in branch.Children)
            {
                GetBranchNamesWithIndentation(child, names, level + 1);
            }
        }

        var allBranchNamesWithLevel = new List<string>();
        foreach (var branch in stack.Branches)
        {
            GetBranchNamesWithIndentation(branch, allBranchNamesWithLevel);
        }

        var branchSelection = (name ?? await inputProvider
            .SelectGrouped(
                Questions.SelectBranch,
                [new ChoiceGroup<string>(stack.SourceBranch, [.. allBranchNamesWithLevel])],
                cancellationToken))
            .Trim();

        logger.Answer(Questions.SelectBranch, branchSelection);

        return branchSelection;
    }

    public static async Task<string> SelectParentBranch(
        this IInputProvider inputProvider,
        ILogger logger,
        string? name,
        Config.Stack stack,
        CancellationToken cancellationToken)
    {
        void GetBranchNamesWithIndentation(Branch branch, List<string> names, int level = 0)
        {
            names.Add($"{new string(' ', level * 2)}{branch.Name}");
            foreach (var child in branch.Children)
            {
                GetBranchNamesWithIndentation(child, names, level + 1);
            }
        }

        var allBranchNamesWithLevel = new List<string>();
        foreach (var branch in stack.Branches)
        {
            GetBranchNamesWithIndentation(branch, allBranchNamesWithLevel, 1);
        }

        var branchSelection = (name ?? await inputProvider.Select(Questions.SelectParentBranch, [stack.SourceBranch, .. allBranchNamesWithLevel], cancellationToken)).Trim();

        logger.Answer(Questions.SelectParentBranch, branchSelection);

        return branchSelection;
    }
}

