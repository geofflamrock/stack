using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Stack.Infrastructure;

public interface IDisplayProvider
{
    Task DisplayStatus(string message, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
    Task DisplayTree<T>(string header, IEnumerable<TreeItem<T>> items, Func<T, string>? itemFormatter = null, CancellationToken cancellationToken = default) where T : notnull;
    Task DisplayMessage(string message, CancellationToken cancellationToken = default);
    Task DisplayHeader(string header, CancellationToken cancellationToken = default);
    Task DisplayNewLine(CancellationToken cancellationToken = default);
}

public class ConsoleDisplayProvider(IAnsiConsole console) : IDisplayProvider
{
    public async Task DisplayStatus(string message, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        await console
            .Status()
            .Spinner(Spinner.Known.Dots3)
            .StartAsync(message, async (_) => await action(cancellationToken));
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

    public async Task DisplayMessage(string message, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        console.MarkupLine(message);
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
}

public record TreeItem<T>(T Value, List<TreeItem<T>> Children);
