using Stack.Git;

namespace Stack.Infrastructure;

public interface IGitClientSettingsUpdater
{
    void UpdateSettings(bool verbose, string? workingDirectory);
}

public interface IGitHubClientSettingsUpdater  
{
    void UpdateSettings(bool verbose, string? workingDirectory);
}

public class GitClientWrapper : IGitClient, IGitClientSettingsUpdater
{
    private IGitClient inner;
    private readonly ILogger logger;

    public GitClientWrapper(ILogger logger)
    {
        this.logger = logger;
        this.inner = new GitClient(logger, GitClientSettings.Default);
    }

    public void UpdateSettings(bool verbose, string? workingDirectory)
    {
        this.inner = new GitClient(logger, new GitClientSettings(verbose, workingDirectory));
    }

    // Delegate all IGitClient methods to inner
    public string GetCurrentBranch() => inner.GetCurrentBranch();
    public bool DoesLocalBranchExist(string branchName) => inner.DoesLocalBranchExist(branchName);
    public string[] GetBranchesThatExistLocally(string[] branches) => inner.GetBranchesThatExistLocally(branches);
    public (int Ahead, int Behind) CompareBranches(string branchName, string sourceBranchName) => inner.CompareBranches(branchName, sourceBranchName);
    public Dictionary<string, GitBranchStatus> GetBranchStatuses(string[] branches) => inner.GetBranchStatuses(branches);
    public string GetRemoteUri() => inner.GetRemoteUri();
    public string[] GetLocalBranchesOrderedByMostRecentCommitterDate() => inner.GetLocalBranchesOrderedByMostRecentCommitterDate();
    public string GetRootOfRepository() => inner.GetRootOfRepository();
    public string? GetConfigValue(string key) => inner.GetConfigValue(key);
    public bool IsAncestor(string ancestor, string descendant) => inner.IsAncestor(ancestor, descendant);
    public void Fetch(bool prune) => inner.Fetch(prune);
    public void ChangeBranch(string branchName) => inner.ChangeBranch(branchName);
    public void CreateNewBranch(string branchName, string sourceBranch) => inner.CreateNewBranch(branchName, sourceBranch);
    public void PushNewBranch(string branchName) => inner.PushNewBranch(branchName);
    public void PullBranch(string branchName) => inner.PullBranch(branchName);
    public void FetchBranchRefSpecs(string[] branchNames) => inner.FetchBranchRefSpecs(branchNames);
    public void PullBranchForWorktree(string branchName, string worktreePath) => inner.PullBranchForWorktree(branchName, worktreePath);
    public void PushBranches(string[] branches, bool forceWithLease) => inner.PushBranches(branches, forceWithLease);
    public void DeleteLocalBranch(string branchName) => inner.DeleteLocalBranch(branchName);
    public void MergeFromLocalSourceBranch(string sourceBranchName) => inner.MergeFromLocalSourceBranch(sourceBranchName);
    public void RebaseFromLocalSourceBranch(string sourceBranchName) => inner.RebaseFromLocalSourceBranch(sourceBranchName);
    public void RebaseOntoNewParent(string newParentBranchName, string oldParentBranchName) => inner.RebaseOntoNewParent(newParentBranchName, oldParentBranchName);
    public void AbortMerge() => inner.AbortMerge();
    public void AbortRebase() => inner.AbortRebase();
    public void ContinueRebase() => inner.ContinueRebase();
}

public class GitHubClientWrapper : IGitHubClient, IGitHubClientSettingsUpdater
{
    private IGitHubClient inner;
    private readonly ILogger logger;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache cache;

    public GitHubClientWrapper(ILogger logger, Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        this.logger = logger;
        this.cache = cache;
        var gitHubClient = new GitHubClient(logger, GitHubClientSettings.Default);
        this.inner = new CachingGitHubClient(gitHubClient, cache);
    }

    public void UpdateSettings(bool verbose, string? workingDirectory)
    {
        var gitHubClient = new GitHubClient(logger, new GitHubClientSettings(verbose, workingDirectory));
        this.inner = new CachingGitHubClient(gitHubClient, cache);
    }

    // Delegate all IGitHubClient methods to inner
    public GitHubPullRequest? GetPullRequest(string branch) => inner.GetPullRequest(branch);
    public GitHubPullRequest CreatePullRequest(string headBranch, string baseBranch, string title, string bodyFilePath, bool draft) => inner.CreatePullRequest(headBranch, baseBranch, title, bodyFilePath, draft);
    public GitHubPullRequest EditPullRequest(GitHubPullRequest pullRequest, string body) => inner.EditPullRequest(pullRequest, body);
    public void OpenPullRequest(GitHubPullRequest pullRequest) => inner.OpenPullRequest(pullRequest);
}