using Spectre.Console;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Infrastructure;

namespace Stack.Commands.Helpers;

public static class InputProviderExtensionMethods
{
    public static string Text(
        this IInputProvider inputProvider,
        IOutputProvider outputProvider,
        string prompt,
        string? presetValue,
        string? defaultValue = null)
    {
        if (presetValue is not null)
        {
            outputProvider.Information($"{prompt} {presetValue}");

            return presetValue;
        }

        return inputProvider.Text(prompt, defaultValue);
    }

    public static string Select(
        this IInputProvider inputProvider,
        IOutputProvider outputProvider,
        string prompt,
        string? presetValue,
        string[] choices)
    {
        var selection = presetValue ?? inputProvider.Select(prompt, choices);

        outputProvider.Information($"{prompt} {selection}");

        return selection;
    }

    public static string[] MultiSelect(
        this IInputProvider inputProvider,
        IOutputProvider outputProvider,
        string prompt,
        string[] choices,
        bool required,
        string[]? presetValues = null)
    {
        var selection = presetValues ?? inputProvider.MultiSelect(prompt, choices, required);

        outputProvider.Information($"{prompt} {string.Join(", ", selection)}");

        return [.. selection];
    }

    public static Config.Stack? SelectStack(
        this IInputProvider inputProvider,
        IOutputProvider outputProvider,
        string? name,
        List<Config.Stack> stacks,
        string currentBranch)
    {
        var stackNames = stacks.OrderByCurrentStackThenByName(currentBranch).Select(s => s.Name).ToArray();
        var stackSelection =
            name ??
            (stacks.Count == 1 ? stacks.First().Name : null) ??
            inputProvider.Select(Questions.SelectStack, stackNames);
        var stack = stacks.FirstOrDefault(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        if (stack is not null)
        {
            outputProvider.Information($"{Questions.SelectStack} {stack.Name}");
        }

        return stack;
    }

    public static string SelectBranch(
        this IInputProvider inputProvider,
        IOutputProvider outputProvider,
        string? name,
        string[] branches)
    {
        return inputProvider.Select(outputProvider, Questions.SelectBranch, name, branches);
    }
}

