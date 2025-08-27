using Spectre.Console;

namespace Stack.Infrastructure;

public static class OutputStyleExtensionMethods
{
    public static string Stack(this string name) => $"[{Color.Yellow}]{name}[/]";
    public static string Branch(this string name) => $"[{Color.Blue}]{name}[/]";
    public static string Muted(this string name) => $"[{Color.Grey}]{name}[/]";
    public static string Example(this string name) => $"[{Color.Aqua}]{name}[/]";
    public static string Highlighted(this string name) => $"[{Color.Green}]{name}[/]";
}