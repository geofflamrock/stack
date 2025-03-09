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
