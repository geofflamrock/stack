using FluentAssertions;
using Meziantou.Extensions.Logging.Xunit;
using NSubstitute;
using Stack.Git;
using Xunit.Abstractions;

namespace Stack.Tests.Git;

public class SafeGitHubClientTests(ITestOutputHelper testOutputHelper)
{
    readonly GitHubPullRequest samplePr = new(
        42,
        "Test PR",
        "Body",
        GitHubPullRequestStates.Open,
        new Uri("https://example.com/pr/42"),
        false,
        "test-branch");

    [Fact]
    public void GetPullRequest_SwallowsException_ReturnsNull()
    {
        var inner = Substitute.For<IGitHubClient>();
        inner.GetPullRequest("feature").Returns(ci => throw new Exception("gh not found"));
        var logger = XUnitLogger.CreateLogger<SafeGitHubClient>(testOutputHelper);
        var client = new SafeGitHubClient(inner, logger);

        var pr1 = client.GetPullRequest("feature");
        var pr2 = client.GetPullRequest("feature");

        pr1.Should().BeNull();
        pr2.Should().BeNull();
    }

    [Fact]
    public void GetPullRequest_PassesThroughOnSuccess()
    {
        var inner = Substitute.For<IGitHubClient>();
        inner.GetPullRequest("feature").Returns(samplePr);
        var logger = XUnitLogger.CreateLogger<SafeGitHubClient>(testOutputHelper);
        var client = new SafeGitHubClient(inner, logger);

        var pr = client.GetPullRequest("feature");
        pr.Should().BeEquivalentTo(samplePr);
    }

    [Fact]
    public void CreatePullRequest_DoesNotSwallowException()
    {
        var inner = Substitute.For<IGitHubClient>();
        inner.CreatePullRequest(default!, default!, default!, default!, default).ReturnsForAnyArgs(ci => throw new InvalidOperationException("fail"));
        var logger = XUnitLogger.CreateLogger<SafeGitHubClient>(testOutputHelper);
        var client = new SafeGitHubClient(inner, logger);

        Assert.Throws<InvalidOperationException>(() => client.CreatePullRequest("h", "b", "t", "f", false));
    }
}