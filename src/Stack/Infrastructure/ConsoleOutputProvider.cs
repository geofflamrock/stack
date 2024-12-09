using Spectre.Console;

namespace Stack.Infrastructure;

public interface IOutputProvider
{
    void Grid(string[] headers, string[][] rows);
    void Tree(string header, string[] items);
    void Status(string message, Action action);
    void Information(string message);
    void Warning(string message);
    void Error(string message);
    void Debug(string message);
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

    public void Grid(string[] headers, string[][] rows)
    {
        var grid = new Grid();

        foreach (var header in headers)
            grid.AddColumn();

        grid.AddRow(headers);

        foreach (var row in rows)
            grid.AddRow(row);

        console.Write(grid);
    }
}

public static class OutputStyleExtensionMethods
{
    public static string Stack(this string name) => $"[yellow]{name}[/]";
    public static string Branch(this string name) => $"[blue]{name}[/]";
    public static string Muted(this string name) => $"[grey]{name}[/]";
    public static string Example(this string name) => $"[aqua]{name}[/]";
}