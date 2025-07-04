namespace Stack.Commands.Helpers;

public static class Questions
{
    public const string SelectStack = "Select stack:";
    public const string StackName = "Stack name:";
    public const string SelectBranch = "Select branch:";
    public const string BranchName = "Branch name:";
    public const string SelectSourceBranch = "Select a branch to start your stack from:";
    public const string SelectParentBranch = "Select a branch to add branch as child of:";
    public const string ConfirmSyncStack = "Are you sure you want to sync this stack with the remote repository?";
    public const string ConfirmDeleteStack = "Are you sure you want to delete this stack?";
    public const string ConfirmDeleteBranches = "Are you sure you want to delete these local branches?";
    public const string ConfirmRemoveBranch = "Are you sure you want to remove this branch from the stack?";
    public const string RemoveBranchChildAction = "What do you want to do with the children of this branch?";
    public const string AddOrCreateBranch = "Add or create a branch:";
    public const string SelectPullRequestsToCreate = "Select branches to create pull requests for:";
    public const string ConfirmCreatePullRequests = "Are you sure you want to create pull requests for branches in this stack?";
    public const string PullRequestTitle = "Title:";
    public const string OpenPullRequests = "Open new pull requests in a browser?";
    public const string CreatePullRequestAsDraft = "Create pull request as draft?";
    public const string ContinueOrAbortMerge = "Conflict(s) detected during merge. Please either resolve the conflicts, commit the result and select Continue to continue merging, or Abort.";
    public const string ContinueOrAbortRebase = "Conflict(s) detected during rebase. Please either resolve the conflicts and select Continue to continue rebasing, or Abort.";
    public const string ConfirmMigrateConfig = "Are you sure you want to migrate the configuration file to the latest version? This will create a backup of the current configuration file.";
    public const string SelectUpdateStrategy = "Select update strategy:";
}
