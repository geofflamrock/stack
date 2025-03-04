using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Infrastructure;

namespace Stack.Commands;

public abstract class Command<T> : AsyncCommand<T> where T : CommandSettingsBase
{
    protected IAnsiConsole StdOut;
    protected IAnsiConsole StdErr;
    protected ILogger StdOutLogger;
    protected ILogger StdErrLogger;
    protected IInputProvider InputProvider;

    public Command()
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



