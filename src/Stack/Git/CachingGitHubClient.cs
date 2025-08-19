using Microsoft.Extensions.Caching.Memory;

namespace Stack.Git;

public class CachingGitHubClient : IGitHubClient
{
    readonly IGitHubClient inner;
    readonly IMemoryCache cache;
    readonly MemoryCacheEntryOptions cacheOptions;

    public CachingGitHubClient(IGitHubClient inner, IMemoryCache? cache = null)
    {
        this.inner = inner;
        this.cache = cache ?? new MemoryCache(new MemoryCacheOptions());
        cacheOptions = new MemoryCacheEntryOptions();
    }

    public GitHubPullRequest? GetPullRequest(string branch)
    {
        var cacheKey = GetCacheKey(branch);
        if (cache.TryGetValue(cacheKey, out GitHubPullRequest? cached))
        {
            // Cache null values if there is no PR as this isn't likely to change in the lifetime of a cli
            return cached;
        }

        var pr = inner.GetPullRequest(branch);
        cache.Set(cacheKey, pr, cacheOptions);
        return pr;
    }

    public GitHubPullRequest CreatePullRequest(string headBranch, string baseBranch, string title, string bodyFilePath, bool draft)
    {
        var pr = inner.CreatePullRequest(headBranch, baseBranch, title, bodyFilePath, draft);
        cache.Set(GetCacheKey(headBranch), pr, cacheOptions);
        return pr;
    }

    public GitHubPullRequest EditPullRequest(GitHubPullRequest pullRequest, string body)
    {
        var updatedPullRequest = inner.EditPullRequest(pullRequest, body);
        cache.Set(GetCacheKey(updatedPullRequest.HeadRefName), updatedPullRequest, cacheOptions);
        return updatedPullRequest;
    }

    public void OpenPullRequest(GitHubPullRequest pullRequest)
    {
        inner.OpenPullRequest(pullRequest);
    }

    static string GetCacheKey(string branch) => $"pr:{branch}";
}
