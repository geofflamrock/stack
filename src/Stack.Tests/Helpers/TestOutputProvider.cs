using Stack.Infrastructure;
using Xunit.Abstractions;

namespace Stack.Tests.Helpers;

public class TestOutputProvider(ITestOutputHelper testOutputHelper) : IOutputProvider
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
}
