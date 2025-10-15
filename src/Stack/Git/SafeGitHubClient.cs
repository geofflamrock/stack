using Microsoft.Extensions.Logging;

namespace Stack.Git;

/// <summary>
/// Decorator that makes pull request status retrieval resilient to missing GitHub CLI,
/// authentication, or connectivity issues. It swallows exceptions in GetPullRequest,
/// logging a single warning and returning null. All other operations are passed through
/// without interception so that explicit user actions still surface errors.
/// </summary>
public class SafeGitHubClient(IGitHubClient inner, ILogger<SafeGitHubClient> logger) : IGitHubClient
{
    bool warningEmitted;

    public GitHubPullRequest? GetPullRequest(string branch)
    {
        try
        {
            return inner.GetPullRequest(branch);
        }
        catch (Exception ex)
        {
            if (!warningEmitted)
            {
                logger.GitHubPullRequestStatusUnavailable(ex.Message);
                warningEmitted = true;
            }
            return null; // suppress failure; treat as no PR
        }
    }

    public GitHubPullRequest CreatePullRequest(string headBranch, string baseBranch, string title, string bodyFilePath, bool draft)
        => inner.CreatePullRequest(headBranch, baseBranch, title, bodyFilePath, draft);

    public GitHubPullRequest EditPullRequest(GitHubPullRequest pullRequest, string body)
        => inner.EditPullRequest(pullRequest, body);

    public void OpenPullRequest(GitHubPullRequest pullRequest)
        => inner.OpenPullRequest(pullRequest);

    public void ThrowIfNotAvailable() => inner.ThrowIfNotAvailable();
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not retrieve GitHub pull request status: {Reason}.")]
    public static partial void GitHubPullRequestStatusUnavailable(this ILogger logger, string reason);
}