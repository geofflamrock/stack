using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using Stack.Git;

namespace Stack.Tests.Git;

public class CachingGitHubClientTests
{
    [Fact]
    public void GetPullRequest_WhenPullRequestExistsForBranch_CachesPullRequest()
    {
        var pr = new GitHubPullRequest(
            Number: 123,
            Title: "Test PR",
            Body: "Body",
            State: GitHubPullRequestStates.Open,
            Url: new Uri("https://example.com/pr/123"),
            IsDraft: false,
            HeadRefName: "feature/test");

        var inner = Substitute.For<IGitHubClient>();
        inner.GetPullRequest("feature/test").Returns(pr);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new CachingGitHubClient(inner, cache);

        var first = sut.GetPullRequest("feature/test");
        var second = sut.GetPullRequest("feature/test");

        first.Should().BeSameAs(pr);
        second.Should().BeSameAs(pr);
        inner.Received(1).GetPullRequest("feature/test");
    }

    [Fact]
    public void GetPullRequest_WhenNoPullRequestExistsForBranch_CachesNull()
    {
        var inner = Substitute.For<IGitHubClient>();
        inner.GetPullRequest("missing").Returns((GitHubPullRequest?)null);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new CachingGitHubClient(inner, cache);

        sut.GetPullRequest("missing").Should().BeNull();
        sut.GetPullRequest("missing").Should().BeNull();
        inner.Received(1).GetPullRequest("missing");
    }

    [Fact]
    public void CreatePullRequest_CachesTheCreatedPullRequestUsingTheHeadBranch()
    {
        var createdPr = new GitHubPullRequest(
            Number: 123,
            Title: "Test PR",
            Body: "Body",
            State: GitHubPullRequestStates.Open,
            Url: new Uri("https://example.com/pr/123"),
            IsDraft: false,
            HeadRefName: "feature/test");

        var inner = Substitute.For<IGitHubClient>();
        inner.CreatePullRequest("feature/test", "main", "title", "body.md", false).Returns(createdPr);
        inner.GetPullRequest("feature/test").Returns(null as GitHubPullRequest);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new CachingGitHubClient(inner, cache);

        // prime cache with null
        sut.GetPullRequest("feature/test").Should().BeNull();

        // create PR should update cache
        var pr = sut.CreatePullRequest("feature/test", "main", "title", "body.md", false);
        pr.Should().BeSameAs(createdPr);

        sut.GetPullRequest("feature/test").Should().BeSameAs(createdPr);
        inner.Received(1).GetPullRequest("feature/test"); // only initial null fetch
        inner.Received(1).CreatePullRequest("feature/test", "main", "title", "body.md", false);
    }

    [Fact]
    public void EditPullRequest_InvalidatesCacheEntryForBranch()
    {
        var pr = new GitHubPullRequest(
            Number: 123,
            Title: "Test PR",
            Body: "Body",
            State: GitHubPullRequestStates.Open,
            Url: new Uri("https://example.com/pr/123"),
            IsDraft: false,
            HeadRefName: "feature/test");

        var inner = Substitute.For<IGitHubClient>();
        inner.GetPullRequest("feature/test").Returns(pr);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new CachingGitHubClient(inner, cache);

        var cachedPullRequest = sut.GetPullRequest("feature/test");
        cachedPullRequest.Should().BeSameAs(pr);
        inner.EditPullRequest(pr, "new body").Returns(pr with { Body = "new body" });

        var updatedPr = sut.EditPullRequest(pr, "new body");

        var afterEdit = sut.GetPullRequest("feature/test");
        afterEdit?.Body.Should().Be(updatedPr.Body);
    }
}
