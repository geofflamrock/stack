using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Infrastructure;

namespace Stack.Commands;

public abstract class Command<T> : AsyncCommand<T> where T : CommandSettingsBase
{
    protected ILogger StdOutLogger;
    protected ILogger StdErrLogger;
    protected IInputProvider InputProvider;

    public Command()
    {
        var stdOutAnsiConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Out),
        });
        var stdErrAnsiConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Error),
        });
        StdOutLogger = new ConsoleLogger(stdOutAnsiConsole);
        StdErrLogger = new ConsoleLogger(stdErrAnsiConsole);
        InputProvider = new ConsoleInputProvider(stdErrAnsiConsole);
    }

    protected TextWriter StdOut => Console.Out;
    protected TextWriter StdErr => Console.Error;

    public override async Task<int> ExecuteAsync(CommandContext context, T settings)
    {
        await Execute(settings);

        return 0;
    }

    protected abstract Task Execute(T settings);
}



