using Stack.Infrastructure;

namespace Stack.Commands.Helpers;

public static class Questions
{
    public const string SelectStack = "Select stack:";
    public const string StackName = "Stack name:";
    public const string SelectBranch = "Select branch:";
    public const string BranchName = "Branch name:";
    public const string SelectSourceBranch = "Select a branch to start your stack from:";
    public const string ConfirmUpdateStack = "Are you sure you want to update this stack?";
    public static string ConfirmDeleteStack(string name) => $"Are you sure you want to delete stack {name.Stack()}?";
    public const string ConfirmDeleteBranches = "Are you sure you want to delete these local branches?";
    public static string ConfirmRemoveBranch(string stackName, string branchName) => $"Are you sure you want to remove branch {branchName.Branch()} from stack {stackName.Stack()}?";
    public const string ConfirmAddOrCreateBranch = "Do you want to add an existing branch or create a new branch and add it to the stack?";
    public const string AddOrCreateBranch = "Add or create a branch:";
    public const string ConfirmSwitchToBranch = "Do you want to switch to the new branch?";
}
