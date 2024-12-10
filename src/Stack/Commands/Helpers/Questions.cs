using Humanizer;
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
    public const string ConfirmDeleteStack = "Are you sure you want to delete this stack?";
    public const string ConfirmDeleteBranches = "Are you sure you want to delete these local branches?";
    public const string ConfirmRemoveBranch = "Are you sure you want to remove this branch from the stack?";
    public const string ConfirmAddOrCreateBranch = "Do you want to add an existing branch or create a new branch and add it to the stack?";
    public const string AddOrCreateBranch = "Add or create a branch:";
    public const string ConfirmSwitchToBranch = "Do you want to switch to the new branch?";
    public static string ConfirmStartCreatePullRequests(int numberOfBranchesWithoutPullRequests) => $"There {"are".ToQuantity(numberOfBranchesWithoutPullRequests, ShowQuantityAs.None)} {"branch".ToQuantity(numberOfBranchesWithoutPullRequests)} to create pull requests for. Do you want to continue?";
    public const string ConfirmCreatePullRequests = "Are you sure you want to create pull requests for branches in this stack?";
    public static string PullRequestTitle(string sourceBranch, string targetBranch) => $"Title for pull request from {sourceBranch.Branch()} -> {targetBranch.Branch()}:";
    public const string PullRequestStackDescription = "Stack description for pull request:";
    public const string OpenPullRequests = "Open the pull requests in the browser?";
    public const string CreatePullRequestsAsDrafts = "Create pull requests as drafts?";
}
