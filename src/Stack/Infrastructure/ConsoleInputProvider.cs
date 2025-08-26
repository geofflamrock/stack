using Spectre.Console;

namespace Stack.Infrastructure;

public record ChoiceGroup<T>(T Group, T[] Choices);

public interface IInputProvider
{
    Task<string> Text(string prompt, CancellationToken cancellationToken, string? defaultValue = null);
    Task<string> Select(string prompt, string[] choices, CancellationToken cancellationToken);
    Task<T> Select<T>(string prompt, T[] choices, CancellationToken cancellationToken, Func<T, string>? converter = null) where T : notnull;
    Task<T> SelectGrouped<T>(string prompt, ChoiceGroup<T>[] choices, CancellationToken cancellationToken, Func<T, string>? converter = null) where T : notnull;
    Task<IEnumerable<T>> MultiSelect<T>(string prompt, T[] choices, bool required, CancellationToken cancellationToken, Func<T, string>? converter = null) where T : notnull;
    Task<bool> Confirm(string prompt, CancellationToken cancellationToken, bool defaultValue = true);
}

public class ConsoleInputProvider(IAnsiConsole console) : IInputProvider
{
    private readonly IAnsiConsole console = console;

    public async Task<string> Text(string prompt, CancellationToken cancellationToken, string? defaultValue = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var textPrompt = new TextPrompt<string>(prompt);

        if (defaultValue is not null)
            textPrompt.DefaultValue(defaultValue.EscapeMarkup());

        var result = await console.PromptAsync(textPrompt, cancellationToken);

        return result.RemoveMarkup();
    }

    public async Task<string> Select(string prompt, string[] choices, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var select = new SelectionPrompt<string>()
            .Title(prompt)
            .PageSize(10)
            .AddChoices(choices);

        return await console.PromptAsync(select, cancellationToken);
    }

    public async Task<T> Select<T>(string prompt, T[] choices, CancellationToken cancellationToken, Func<T, string>? converter = null)
        where T : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();

        var select = new SelectionPrompt<T>()
            .Title(prompt)
            .PageSize(10)
            .AddChoices(choices)
            .UseConverter(converter);

        return await console.PromptAsync(select, cancellationToken);
    }

    public async Task<T> SelectGrouped<T>(string prompt, ChoiceGroup<T>[] groups, CancellationToken cancellationToken, Func<T, string>? converter = null)
        where T : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();

        var select = new SelectionPrompt<T>()
            .Title(prompt)
            .PageSize(10)
            .UseConverter(converter);

        foreach (var group in groups)
        {
            select.AddChoiceGroup(group.Group, group.Choices);
        }

        return await console.PromptAsync(select, cancellationToken);
    }

    public async Task<IEnumerable<T>> MultiSelect<T>(string prompt, T[] choices, bool required, CancellationToken cancellationToken, Func<T, string>? converter = null)
        where T : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();

        var select = new MultiSelectionPrompt<T>()
            .Title(prompt)
            .PageSize(10)
            .AddChoices(choices)
            .Required(required)
            .UseConverter(converter);

        return await console.PromptAsync(select, cancellationToken);
    }

    public async Task<bool> Confirm(string prompt, CancellationToken cancellationToken, bool defaultValue = true)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var confirmationPrompt = new ConfirmationPrompt(prompt)
        {
            DefaultValue = defaultValue
        };

        return await console.PromptAsync(confirmationPrompt, cancellationToken);
    }
}


