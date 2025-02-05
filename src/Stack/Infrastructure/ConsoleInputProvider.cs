using Spectre.Console;

namespace Stack.Infrastructure;

public record ChoiceGroup<T>(T Group, T[] Choices);

public interface IInputProvider
{
    string Text(string prompt, string? defaultValue = null);
    string Select(string prompt, string[] choices);
    T Select<T>(string prompt, T[] choices, Func<T, string>? converter = null) where T : notnull;
    T SelectGrouped<T>(string prompt, ChoiceGroup<T>[] choices, Func<T, string>? converter = null) where T : notnull;
    IEnumerable<T> MultiSelect<T>(string prompt, T[] choices, bool required, Func<T, string>? converter = null) where T : notnull;
    bool Confirm(string prompt, bool defaultValue = true);
}

public class ConsoleInputProvider(IAnsiConsole console) : IInputProvider
{
    private readonly IAnsiConsole console = console;

    public string Text(string prompt, string? defaultValue = null)
    {
        var textPrompt = new TextPrompt<string>(prompt);

        if (defaultValue is not null)
            textPrompt.DefaultValue(defaultValue.EscapeMarkup());

        var result = console.Prompt(textPrompt);

        return result.RemoveMarkup();
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

    public IEnumerable<T> MultiSelect<T>(string prompt, T[] choices, bool required, Func<T, string>? converter = null)
        where T : notnull
    {
        var select = new MultiSelectionPrompt<T>()
            .Title(prompt)
            .PageSize(10)
            .AddChoices(choices)
            .Required(required)
            .UseConverter(converter);

        return console.Prompt(select);
    }

    public bool Confirm(string prompt, bool defaultValue = true)
    {
        var confirmationPrompt = new ConfirmationPrompt(prompt)
        {
            DefaultValue = defaultValue
        };

        return console.Prompt(confirmationPrompt);
    }
}


