using Spectre.Console;

namespace Stack.Infrastructure;

public interface IOutputProvider
{
    void Tree(string header, string[] items);
    void Status(string message, Action action);
    void Information(string message);
    void Warning(string message);
    void Error(string message);
    void Debug(string message);
    void Rule(string message);
}

public static class OutputProviderExtensionMethods
{
    public static void NewLine(this IOutputProvider outputProvider) => outputProvider.Information(string.Empty);
}

public class ConsoleOutputProvider(IAnsiConsole console) : IOutputProvider
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
}

public static class OutputStyleExtensionMethods
{
    public static string Stack(this string name) => $"[{Color.Yellow}]{name}[/]";
    public static string Branch(this string name) => $"[{Color.Blue}]{name}[/]";
    public static string Muted(this string name) => $"[{Color.Grey}]{name}[/]";
    public static string Example(this string name) => $"[{Color.Aqua}]{name}[/]";
    public static string Commit(this string name) => $"[{Color.Orange1}]{name}[/]";
}