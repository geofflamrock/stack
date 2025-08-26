using Spectre.Console;

namespace Stack.Infrastructure;

public class StdOutLogger : ConsoleLogger, IStdOutLogger
{
    public StdOutLogger() : base(CreateStdOutConsole())
    {
    }

    private static IAnsiConsole CreateStdOutConsole()
    {
        return AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Out),
        });
    }
}