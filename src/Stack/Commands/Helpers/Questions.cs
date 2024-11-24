using Stack.Infrastructure;

namespace Stack.Commands.Helpers;

public static class Questions
{
    public const string SelectStack = "Select stack:";
    public const string SelectBranch = "Select branch:";
    public static string ConfirmDeleteStack(string name) => $"Are you sure you want to delete stack {name.Stack()}?";
    public const string ConfirmDeleteBranches = "Are you sure you want to delete these local branches?";
    public static string ConfirmRemoveBranch(string stackName, string branchName) => $"Are you sure you want to remove branch {branchName.Branch()} from stack {stackName.Stack()}?";
}
