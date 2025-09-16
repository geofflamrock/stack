using Spectre.Console;

namespace Stack.Infrastructure;

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
