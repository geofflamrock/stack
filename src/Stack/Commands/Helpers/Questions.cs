using Spectre.Console;
using Stack.Config;

namespace Stack.Commands.Helpers;

public static class Questions
{
    public const string SelectStack = "Select stack:";
    public const string ConfirmDeleteStack = "Are you sure you want to delete this stack?";
    public const string ConfirmDeleteBranches = "Do you want to delete these local branches?";
}
