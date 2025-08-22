using Spectre.Console;

namespace Stack.Infrastructure;

public class StdErrLogger : ConsoleLogger, IStdErrLogger
{
    public StdErrLogger(IAnsiConsole console) : base(console)
    {
    }
}