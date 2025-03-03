using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Infrastructure;

namespace Stack.Commands;

public abstract class CommandBase<T> : AsyncCommand<T> where T : CommandSettingsBase
{
    protected IAnsiConsole Console;
    protected ILogger Logger;
    protected IInputProvider InputProvider;

    public CommandBase()
    {
        Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(System.Console.Error),
        });
        Logger = new ConsoleLogger(Console);
        InputProvider = new ConsoleInputProvider(Console);
    }

    public abstract override Task<int> ExecuteAsync(CommandContext context, T settings);
}

public abstract class CommandWithHandler<TSettings, TInput, TResponse> : CommandBase<TSettings>
    where TSettings : CommandSettingsBase
    where TInput : notnull
    where TResponse : notnull
{
    public override async Task<int> ExecuteAsync(CommandContext context, TSettings settings)
    {
        var inputs = CreateInputs(settings);
        var handler = CreateHandler(settings);

        var response = await handler.Handle(inputs);
        FormatOutput(settings, response);

        return 0;
    }

    protected abstract CommandHandlerBase<TInput, TResponse> CreateHandler(TSettings settings);

    protected abstract TInput CreateInputs(TSettings settings);

    protected virtual IOutputFormatter<TResponse> CreateFormatter(TSettings settings)
    {
        return new DefaultOutputFormatter<TResponse>();
    }

    protected virtual void FormatOutput(TSettings settings, TResponse response)
    {
        var formatter = CreateFormatter(settings);

        Console.WriteLine(formatter.Format(response));
    }
}

public class DefaultOutputFormatter<T> : IOutputFormatter<T> where T : notnull
{
    public string Format(T response) => string.Empty;
}

public class JsonOutputFormatter<T> : IOutputFormatter<T> where T : notnull
{
    readonly JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string Format(T response) => JsonSerializer.Serialize(response, options);
}

public abstract class CommandHandlerBase<TInput, TResponse>
    where TInput : notnull
    where TResponse : notnull
{
    public abstract Task<TResponse> Handle(TInput inputs);
}

public interface IOutputFormatter<T> where T : notnull
{
    string Format(T response);
}

