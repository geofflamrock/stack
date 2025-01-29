using Stack.Git;

namespace Stack.Tests.Helpers;

public class TestGitHubRepositoryBuilder
{
    readonly Dictionary<string, GitHubPullRequest> _pullRequests = new();

    public TestGitHubRepositoryBuilder WithPullRequest(string branch, Action<TestGitHubPullRequestBuilder> pullRequestBuilder)
    {
        var builder = new TestGitHubPullRequestBuilder();
        pullRequestBuilder(builder);
        _pullRequests.Add(branch, builder.Build());
        return this;
    }

    public TestGitHubRepository Build()
    {
        return new TestGitHubRepository(_pullRequests);
    }
}

public class TestGitHubPullRequestBuilder
{
    int _number = Some.Int();
    string _title = Some.Name();
    string _body = Some.Name();
    string _state = GitHubPullRequestStates.Open;
    Uri _url = Some.HttpsUri();
    bool _draft = false;

    public TestGitHubPullRequestBuilder WithNumber(int number)
    {
        _number = number;
        return this;
    }

    public TestGitHubPullRequestBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public TestGitHubPullRequestBuilder WithBody(string body)
    {
        _body = body;
        return this;
    }

    public TestGitHubPullRequestBuilder Merged()
    {
        _state = GitHubPullRequestStates.Merged;
        return this;
    }

    public TestGitHubPullRequestBuilder Open()
    {
        _state = GitHubPullRequestStates.Open;
        return this;
    }

    public TestGitHubPullRequestBuilder Closed()
    {
        _state = GitHubPullRequestStates.Closed;
        return this;
    }

    public TestGitHubPullRequestBuilder WithUrl(Uri url)
    {
        _url = url;
        return this;
    }

    public TestGitHubPullRequestBuilder AsDraft()
    {
        _draft = true;
        return this;
    }

    public GitHubPullRequest Build()
    {
        return new GitHubPullRequest(_number, _title, _body, _state, _url, _draft);
    }
}

public class TestGitHubRepository(Dictionary<string, GitHubPullRequest> PullRequests) : IGitHubClient
{
    public Dictionary<string, GitHubPullRequest> PullRequests { get; } = PullRequests;

    public GitHubPullRequest CreatePullRequest(string headBranch, string baseBranch, string title, string bodyFilePath, bool draft)
    {
        var prBody = File.ReadAllText(bodyFilePath).Trim();
        var pr = new GitHubPullRequest(Some.Int(), title, prBody, GitHubPullRequestStates.Open, Some.HttpsUri(), draft);
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