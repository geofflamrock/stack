using Microsoft.Extensions.Logging;
using MoreLinq;
using Spectre.Console;
using Stack.Commands;

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
        if (logLevel == LogLevel.Information)
        {
            var logState = LoggerMessageHelper.ThreadLocalState;

            var tagsToProcess = logState.TagArray.ToList();
            logState.Clear();

            foreach (var item in tagsToProcess)
            {
                if (item.Value is IAnsiConsoleFormattable formattable)
                {
                    logState.AddTag(item.Key, formattable.Format());
                }
                else
                {
                    logState.AddTag(item.Key, item.Value);
                }
            }
        }

        var message = formatter(state, exception);

        if (message is null)
            return;

        switch (logLevel)
        {
            case LogLevel.Critical:
            case LogLevel.Error:
                {
                    console.MarkupLine($"[{Color.Red}]{message}[/]");
                    break;
                }
            case LogLevel.Warning:
                {
                    console.MarkupLine($"[{Color.Orange1}]{message}[/]");
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
                    console.MarkupLine($"[{Color.Grey}]{message}[/]");
                    break;
                }
        }
    }
}
