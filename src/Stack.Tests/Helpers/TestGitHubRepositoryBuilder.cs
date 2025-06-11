using Stack.Git;

namespace Stack.Tests.Helpers;

public class TestGitHubRepositoryBuilder
{
    readonly Dictionary<string, GitHubPullRequest> pullRequests = new();

    public TestGitHubRepositoryBuilder WithPullRequest(string branch, Action<TestGitHubPullRequestBuilder>? pullRequestBuilder = null)
    {
        var builder = new TestGitHubPullRequestBuilder()
            .WithHeadRefName(branch);
        pullRequestBuilder?.Invoke(builder);
        pullRequests.Add(branch, builder.Build());
        return this;
    }

    public TestGitHubRepository Build()
    {
        return new TestGitHubRepository(pullRequests);
    }
}

public class TestGitHubPullRequestBuilder
{
    int number = Some.Int();
    string title = Some.Name();
    string body = Some.Name();
    string state = GitHubPullRequestStates.Open;
    Uri url = Some.HttpsUri();
    bool draft = false;
    string headRefName = Some.Name();

    public TestGitHubPullRequestBuilder WithTitle(string title)
    {
        this.title = title;
        return this;
    }

    public TestGitHubPullRequestBuilder WithBody(string body)
    {
        this.body = body;
        return this;
    }

    public TestGitHubPullRequestBuilder Merged()
    {
        state = GitHubPullRequestStates.Merged;
        return this;
    }

    public TestGitHubPullRequestBuilder WithHeadRefName(string headRefName)
    {
        this.headRefName = headRefName;
        return this;
    }

    public GitHubPullRequest Build()
    {
        return new GitHubPullRequest(number, title, body, state, url, draft, headRefName);
    }
}

public class TestGitHubRepository(Dictionary<string, GitHubPullRequest> PullRequests) : IGitHubClient
{
    public Dictionary<string, GitHubPullRequest> PullRequests { get; } = PullRequests;

    public GitHubPullRequest CreatePullRequest(
        string headBranch,
        string baseBranch,
        string title,
        string bodyFilePath,
        bool draft)
    {
        var prBody = File.ReadAllText(bodyFilePath).Trim();
        var pr = new GitHubPullRequest(Some.Int(), title, prBody, GitHubPullRequestStates.Open, Some.HttpsUri(), draft, headBranch);
        PullRequests.Add(headBranch, pr);
        return pr;
    }

    public void EditPullRequest(int number, string body)
    {
        if (!PullRequests.Any(pr => pr.Value.Number == number))
        {
            throw new InvalidOperationException("Pull request not found.");
        }

        var pr = PullRequests.First(p => p.Value.Number == number);
        PullRequests[pr.Key] = pr.Value with { Body = body };
    }

    public GitHubPullRequest? GetPullRequest(string branch)
    {
        return PullRequests.GetValueOrDefault(branch);
    }

    public void OpenPullRequest(GitHubPullRequest pullRequest)
    {
    }
}