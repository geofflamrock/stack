using Spectre.Console;
using Spectre.Console.Rendering;

namespace Stack.Infrastructure;

public interface ILogger
{
    void Tree(Tree<string> tree);
    void Tree(string header, string[] items);
    void Status(string message, Action action);
    void Information(string message);
    void Warning(string message);
    void Error(string message);
    void Debug(string message);
    void Rule(string message);
}

public static class LoggerExtensionMethods
{
    public static void NewLine(this ILogger logger) => logger.Information(string.Empty);
}

public class ConsoleLogger(IAnsiConsole console) : ILogger
{
    public void Status(string message, Action action) => console.Status().Start(message, (_) => action());

    public void Warning(string message) => console.MarkupLine($"[orange1]{message}[/]");

    public void Information(string message) => console.MarkupLine(message);

    public void Error(string message) => console.MarkupLine($"[red]{message}[/]");

    public void Debug(string message) => console.MarkupLine($"[grey]{message}[/]");

    public void Tree(string header, string[] items)
    {
        var tree = new Tree(header);

        foreach (var item in items)
            tree.AddNode(item);

        console.Write(tree);
    }

    public void Rule(string message)
    {
        var rule = new Rule(message);
        rule.LeftJustified();
        rule.DoubleBorder();
        console.Write(rule);
    }

    public void Tree(Tree<string> tree)
    {
        var consoleTree = new Tree(tree.Header);
        foreach (var child in tree.Children)
        {
            var treeNode = consoleTree.AddNode(child.Value);
            AddChildTreeNodes(treeNode, child);
        }

        console.Write(consoleTree);
    }

    void AddChildTreeNodes(TreeNode parent, TreeItem<string> item)
    {
        foreach (var child in item.Children)
        {
            var node = parent.AddNode(child.Value);
            if (child.Children.Count > 0)
            {
                AddChildTreeNodes(node, child);
            }
        }
    }
}

public record Tree<T>(string Header, List<TreeItem<T>> Children);

public record TreeItem<T>(T Value, List<TreeItem<T>> Children);

public static class OutputStyleExtensionMethods
{
    public static string Stack(this string name) => $"[{Color.Yellow}]{name}[/]";
    public static string Branch(this string name) => $"[{Color.Blue}]{name}[/]";
    public static string Muted(this string name) => $"[{Color.Grey}]{name}[/]";
    public static string Example(this string name) => $"[{Color.Aqua}]{name}[/]";
    public static string Highlighted(this string name) => $"[{Color.Green}]{name}[/]";
}