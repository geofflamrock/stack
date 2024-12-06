using Spectre.Console;

namespace Stack.Infrastructure;

public record ChoiceGroup<T>(T Group, T[] Choices);

public interface IInputProvider
{
    string Text(string prompt, string? defaultValue = null);
    string Select(string prompt, string[] choices);
    T Select<T>(string prompt, T[] choices, Func<T, string>? converter = null) where T : notnull;
    T SelectGrouped<T>(string prompt, ChoiceGroup<T>[] choices, Func<T, string>? converter = null) where T : notnull;
    bool Confirm(string prompt);
}

public class ConsoleInputProvider(IAnsiConsole console) : IInputProvider
{
    private readonly IAnsiConsole console = console;

    public string Text(string prompt, string? defaultValue = null)
    {
        var textPrompt = new TextPrompt<string>(prompt);

        if (defaultValue is not null)
            textPrompt.DefaultValue(defaultValue);

        return console.Prompt(textPrompt);
    }

    public string Select(string prompt, string[] choices)
    {
        var select = new SelectionPrompt<string>()
            .Title(prompt)
            .PageSize(10)
            .AddChoices(choices);

        return console.Prompt(select);
    }

    public T Select<T>(string prompt, T[] choices, Func<T, string>? converter = null)
        where T : notnull
    {
        var select = new SelectionPrompt<T>()
            .Title(prompt)
            .PageSize(10)
            .AddChoices(choices)
            .UseConverter(converter);

        return console.Prompt(select);
    }

    public T SelectGrouped<T>(string prompt, ChoiceGroup<T>[] groups, Func<T, string>? converter = null)
        where T : notnull
    {
        var select = new SelectionPrompt<T>()
            .Title(prompt)
            .PageSize(10)
            .UseConverter(converter);

        foreach (var group in groups)
        {
            select.AddChoiceGroup(group.Group, group.Choices);
        }

        return console.Prompt(select);
    }

    public bool Confirm(string prompt) => console.Prompt(new ConfirmationPrompt(prompt));
}


