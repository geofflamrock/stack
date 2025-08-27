using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Stack.Infrastructure;

public class AnsiConsoleLoggerProvider : ILoggerProvider
{
    readonly IAnsiConsole console;

    public AnsiConsoleLoggerProvider(IAnsiConsole console)
    {
        this.console = console;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new AnsiConsoleLogger(console);
    }

    public void Dispose()
    {
    }
}
