using Stack.Infrastructure;
using Xunit.Abstractions;

namespace Stack.Tests.Helpers;

public class TestLogger(ITestOutputHelper testOutputHelper) : ILogger
{
    public void Debug(string message)
    {
        testOutputHelper.WriteLine($"DEBUG: {message}");
    }

    public void Error(string message)
    {
        testOutputHelper.WriteLine($"ERROR: {message}");
    }

    public void Information(string message)
    {
        testOutputHelper.WriteLine($"INFO: {message}");
    }

    public void Rule(string message)
    {
        testOutputHelper.WriteLine($"RULE: {message}");
    }

    public void Status(string message, Action action)
    {
        testOutputHelper.WriteLine($"STATUS: {message}");
        action();
    }

    public void Tree(string header, string[] items)
    {
        testOutputHelper.WriteLine($"TREE: {header}");
        foreach (var item in items)
        {
            testOutputHelper.WriteLine($"  {item}");
        }
    }

    public void Warning(string message)
    {
        testOutputHelper.WriteLine($"WARNING: {message}");
    }

    public void Tree(Tree<string> tree)
    {
        testOutputHelper.WriteLine($"TREE: {tree.Header}");
        foreach (var child in tree.Children)
        {
            testOutputHelper.WriteLine($"  {child.Value}");
            // TODO: Handle child nodes if needed
        }
    }
}
