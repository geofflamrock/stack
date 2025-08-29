using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Stack.Infrastructure;

public interface IAnsiConsoleWriter
{
    void Tree(Tree<string> tree);
    void Tree(string header, string[] items);
    void Status(string message, Action action);
    void Rule(string message);
    void WriteLine(string message);
}

public class AnsiConsoleWriter(IAnsiConsole console) : IAnsiConsoleWriter
{
    public void Status(string message, Action action) => console.Status().Start(message, (_) => action());

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

    public void WriteLine(string message) => console.MarkupLine(message);
}

public record Tree<T>(string Header, List<TreeItem<T>> Children);

public record TreeItem<T>(T Value, List<TreeItem<T>> Children);
