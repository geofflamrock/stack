using Spectre.Console;

namespace Stack.Infrastructure;

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
