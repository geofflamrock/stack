using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Stack.Infrastructure;

public interface IDisplayProvider
{
    Task DisplayStatus(string message, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
    Task<T> DisplayStatus<T>(string message, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
    Task DisplaySuccess(string message, CancellationToken cancellationToken = default);
    Task DisplayStatusWithSuccess(string message, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        return DisplayStatus(message, async ct =>
        {
            await action(ct);
            await DisplaySuccess(message, ct);
        }, cancellationToken);
    }
}

public class LoggingDisplayProvider(ILogger<LoggingDisplayProvider> logger) : IDisplayProvider
{
    public async Task DisplayStatus(string message, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        logger.Status(message);
        await action(cancellationToken);
    }

    public async Task<T> DisplayStatus<T>(string message, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        logger.Status(message);
        return await action(cancellationToken);
    }

    public async Task DisplaySuccess(string message, CancellationToken cancellationToken = default)
    {
        logger.Success(message);
        await Task.CompletedTask;
    }
}

public static class KnownEvents
{
    public const int Status = 1;
    public const int Success = 2;
}

public static partial class LoggerExtensionMethods
{
    [LoggerMessage(KnownEvents.Status, LogLevel.Information, "{Message}")]
    public static partial void Status(this ILogger logger, string message);

    [LoggerMessage(KnownEvents.Success, LogLevel.Information, "{Message}")]
    public static partial void Success(this ILogger logger, string message);
}

public interface IOutputProvider
{
    Task WriteLine(string output, CancellationToken cancellationToken);

    Task WriteMessage(string message, CancellationToken cancellationToken)
        => WriteLine(RenderingHelpers.RenderMessage(message), cancellationToken);

    Task WriteNewLine(CancellationToken cancellationToken)
        => WriteLine(string.Empty, cancellationToken);

    Task WriteHeader(string header, CancellationToken cancellationToken)
        => WriteLine(RenderingHelpers.RenderHeader(header), cancellationToken);
}

public class ConsoleOutputProvider : IOutputProvider
{
    public async Task WriteLine(string output, CancellationToken cancellationToken = default)
    {
        await Console.Out.WriteLineAsync(output.AsMemory(), cancellationToken);
    }
}

public static class RenderingHelpers
{
    public static string RenderTree<T>(string header, IEnumerable<TreeItem<T>> items, Func<T, string>? itemFormatter = null)
        where T : notnull
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(writer)
        });
        var tree = new Tree(header);

        foreach (var item in items)
        {
            var formattedItem = itemFormatter?.Invoke(item.Value) ?? item.Value.ToString();
            if (formattedItem is null)
            {
                continue;
            }
            var treeNode = tree.AddNode(formattedItem);
            AddChildTreeNodes(treeNode, item, itemFormatter);
        }

        console.Write(tree);

        return writer.ToString();
    }

    public static string RenderHeader(string header)
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(writer)
        });

        var rule = new Rule(header);
        rule.LeftJustified();
        rule.DoubleBorder();
        console.Write(rule);

        return writer.ToString();
    }

    public static string RenderMessage(string message)
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(writer)
        });

        console.Markup(message);
        return writer.ToString();
    }

    static void AddChildTreeNodes<T>(TreeNode parent, TreeItem<T> item, Func<T, string>? itemFormatter = null) where T : notnull
    {
        foreach (var child in item.Children)
        {
            var formattedItem = itemFormatter?.Invoke(child.Value) ?? child.Value.ToString();
            if (formattedItem is null)
            {
                continue;
            }
            var node = parent.AddNode(formattedItem);
            if (child.Children.Count > 0)
            {
                AddChildTreeNodes(node, child);
            }
        }
    }
}

public class ConsoleDisplayProvider(IAnsiConsole console) : IDisplayProvider
{
    readonly AsyncLocal<StatusContext?> _currentStatusContext = new();

    public async Task DisplayStatus(string message, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        if (_currentStatusContext.Value is not null)
        {
            _currentStatusContext.Value.Status(message);
            await action(cancellationToken);
            return;
        }

        await console
            .Status()
            .Spinner(Spinner.Known.Dots3)
            .StartAsync(message, async (context) =>
            {
                _currentStatusContext.Value = context;
                try
                {
                    await action(cancellationToken);
                }
                finally
                {
                    if (ReferenceEquals(_currentStatusContext.Value, context))
                    {
                        _currentStatusContext.Value = null;
                    }
                }
            });
    }

    public async Task<T> DisplayStatus<T>(string message, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (_currentStatusContext.Value is not null)
        {
            _currentStatusContext.Value.Status(message);
            return await action(cancellationToken);
        }

        return await console
            .Status()
            .Spinner(Spinner.Known.Dots3)
            .StartAsync(message, async (context) =>
            {
                _currentStatusContext.Value = context;
                try
                {
                    return await action(cancellationToken);
                }
                finally
                {
                    if (ReferenceEquals(_currentStatusContext.Value, context))
                    {
                        _currentStatusContext.Value = null;
                    }
                }
            });
    }

    public async Task DisplayTree<T>(string header, IEnumerable<TreeItem<T>> items, Func<T, string>? itemFormatter = null, CancellationToken cancellationToken = default)
        where T : notnull
    {
        await Task.CompletedTask;
        var tree = new Tree(header);

        foreach (var item in items)
        {
            var formattedItem = itemFormatter?.Invoke(item.Value) ?? item.Value.ToString();
            if (formattedItem is null)
            {
                continue;
            }
            var treeNode = tree.AddNode(formattedItem);
            AddChildTreeNodes(treeNode, item, itemFormatter);
        }

        console.Write(tree);
    }

    void AddChildTreeNodes<T>(TreeNode parent, TreeItem<T> item, Func<T, string>? itemFormatter = null) where T : notnull
    {
        foreach (var child in item.Children)
        {
            var formattedItem = itemFormatter?.Invoke(child.Value) ?? child.Value.ToString();
            if (formattedItem is null)
            {
                continue;
            }
            var node = parent.AddNode(formattedItem);
            if (child.Children.Count > 0)
            {
                AddChildTreeNodes(node, child);
            }
        }
    }

    public async Task DisplayNewLine(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        console.WriteLine();
    }

    public async Task DisplayHeader(string header, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        var rule = new Rule(header);
        rule.LeftJustified();
        rule.DoubleBorder();
        console.Write(rule);
    }

    public async Task DisplaySuccess(string message, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        console.MarkupLine($"{Emoji.Known.CheckMark}  {message}", cancellationToken);
    }
}

public record TreeItem<T>(T Value, List<TreeItem<T>> Children);
