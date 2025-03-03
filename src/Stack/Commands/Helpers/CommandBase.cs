using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Infrastructure;

namespace Stack.Commands;

public abstract class CommandBase<T> : AsyncCommand<T> where T : CommandSettingsBase
{
    protected IAnsiConsole StdOut;
    protected IAnsiConsole StdErr;
    protected ILogger StdOutLogger;
    protected ILogger StdErrLogger;
    protected IInputProvider InputProvider;

    public CommandBase()
    {
        StdOut = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Out),
        });
        StdErr = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Error),
        });
        StdOutLogger = new ConsoleLogger(StdOut);
        StdErrLogger = new ConsoleLogger(StdErr);
        InputProvider = new ConsoleInputProvider(StdErr);
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
        WriteOutput(settings, response);

        return 0;
    }

    protected abstract CommandHandlerBase<TInput, TResponse> CreateHandler(TSettings settings);

    protected abstract TInput CreateInputs(TSettings settings);

    protected virtual IOutputFormatter<TResponse> CreateFormatter(TSettings settings)
    {
        return new DefaultOutputFormatter<TResponse>();
    }

    protected virtual void WriteOutput(TSettings settings, TResponse response)
    {
        var formatter = CreateFormatter(settings);
        StdOut.WriteLine(formatter.Format(response));
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

