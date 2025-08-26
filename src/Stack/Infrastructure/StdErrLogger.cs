using Spectre.Console;

namespace Stack.Infrastructure;

public class StdErrLogger : ConsoleLogger, IStdErrLogger
{
    public StdErrLogger() : base(CreateStdErrConsole())
    {
    }

    private static IAnsiConsole CreateStdErrConsole()
    {
        return AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Error),
        });
    }
}