using Spectre.Console;

namespace Stack.Infrastructure;

public interface IOutputProvider
{
    void Status(string message, Action action);
    void Information(string message);
    void Warning(string message);
    void Error(string message);
    void Debug(string message);
}

public class ConsoleOutputProvider(IAnsiConsole console) : IOutputProvider
{
    public void Status(string message, Action action) => console.Status().Start(message, (_) => action());

    public void Warning(string message) => console.MarkupLine($"[orange1]{message}[/]");

    public void Information(string message) => console.MarkupLine(message);

    public void Error(string message) => console.MarkupLine($"[red]{message}[/]");

    public void Debug(string message) => console.MarkupLine($"[grey]{message}[/]");
}

public static class OutputStyleExtensionMethods
{
    public static string Stack(this string name) => $"[yellow]{name}[/]";
    public static string Branch(this string name) => $"[blue]{name}[/]";
}