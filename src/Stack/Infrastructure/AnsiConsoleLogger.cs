using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Stack.Infrastructure;

public class AnsiConsoleLogger(IAnsiConsole console) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);

        if (message is null)
            return;

        switch (logLevel)
        {
            case LogLevel.Critical:
            case LogLevel.Error:
                {
                    console.MarkupLine($"[red]{message}[/]");
                    break;
                }
            case LogLevel.Warning:
                {
                    console.MarkupLine($"[orange1]{message}[/]");
                    break;
                }
            case LogLevel.Information:
                {
                    console.MarkupLine(message);
                    break;
                }
            case LogLevel.Debug:
            case LogLevel.Trace:
                {
                    console.MarkupLine($"[grey]{message}[/]");
                    break;
                }
        }
    }
}
