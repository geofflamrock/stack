using Stack.Infrastructure;

namespace Stack.Commands.Helpers;

public static class Questions
{
    public const string SelectStack = "Select stack:";
    public static string ConfirmDeleteStack(string name) => $"Are you sure you want to delete stack {name.Stack()}?";
    public const string ConfirmDeleteBranches = "Are you sure you want to delete these local branches?";
}
