using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using Stack.Infrastructure;

namespace Stack.Git;

public record GitHubClientSettings(bool Verbose, string? WorkingDirectory)
{
    public static GitHubClientSettings Default => new(false, null);
}

public static class GitHubPullRequestStates
{
    public const string Open = "OPEN";
    public const string Closed = "CLOSED";
    public const string Merged = "MERGED";
}

public record GitHubPullRequest(
    int Number,
    string Title,
    string Body,
    string State,
    Uri Url,
    bool IsDraft,
    string HeadRefName);

public static class GitHubPullRequestExtensionMethods
{
    public static Color GetPullRequestColor(this GitHubPullRequest pullRequest)
    {
        if (pullRequest.IsDraft)
            return Color.Grey;

        return pullRequest.State switch
        {
            GitHubPullRequestStates.Open => Color.Green,
            GitHubPullRequestStates.Closed => Color.Red,
            GitHubPullRequestStates.Merged => Color.Purple,
            _ => Color.Default
        };
    }

    public static string GetPullRequestDisplay(this GitHubPullRequest pullRequest)
    {
        return $"[{pullRequest.GetPullRequestColor()} link={pullRequest.Url}]#{pullRequest.Number}: {Markup.Escape(pullRequest.Title)}[/]";
    }
}

public interface IGitHubClient
{
    GitHubPullRequest? GetPullRequest(string branch);
    GitHubPullRequest CreatePullRequest(
        string headBranch,
        string baseBranch,
        string title,
        string bodyFilePath,
        bool draft);
    GitHubPullRequest EditPullRequest(GitHubPullRequest pullRequest, string body);
    void OpenPullRequest(GitHubPullRequest pullRequest);
}

// Matches JsonSerializerOptions.Web: https://github.com/dotnet/runtime/blob/9d5a6a9aa463d6d10b0b0ba6d5982cc82f363dc3/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/JsonSerializerOptions.cs#L170
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(List<GitHubPullRequest>))]
internal partial class GitHubClientJsonSerializerContext : JsonSerializerContext
{
}

public class GitHubClient(ILogger logger, GitHubClientSettings settings) : IGitHubClient
{
    public GitHubPullRequest? GetPullRequest(string branch)
    {
        var output = ExecuteGitHubCommandAndReturnOutput($"pr list --json title,number,body,state,url,isDraft,headRefName --head {branch} --state all");
        var pullRequests = JsonSerializer.Deserialize(output, GitHubClientJsonSerializerContext.Default.ListGitHubPullRequest)!;

        return pullRequests.FirstOrDefault();
    }

    public GitHubPullRequest CreatePullRequest(
        string headBranch,
        string baseBranch,
        string title,
        string bodyFilePath,
        bool draft)
    {
        var command = $"pr create --title \"{Sanitize(title)}\" --body-file \"{bodyFilePath}\" --base {baseBranch} --head {headBranch}";

        if (draft)
        {
            command += " --draft";
        }

        ExecuteGitHubCommand(command);

        return GetPullRequest(headBranch) ?? throw new Exception("Failed to create pull request.");
    }

    public GitHubPullRequest EditPullRequest(GitHubPullRequest pullRequest, string body)
    {
        ExecuteGitHubCommand($"pr edit {pullRequest.Number} --body \"{Sanitize(body)}\"");
        return pullRequest with { Body = body };
    }

    public void OpenPullRequest(GitHubPullRequest pullRequest)
    {
        ExecuteGitHubCommand($"pr view {pullRequest.Number} --web");
    }

    private string ExecuteGitHubCommandAndReturnOutput(string command)
    {
        return ProcessHelpers.ExecuteProcessAndReturnOutput(
            "gh",
            command,
            settings.WorkingDirectory,
            logger,
            settings.Verbose,
            false,
            null
        );
    }

    private void ExecuteGitHubCommand(string command)
    {
        if (settings.Verbose)
            logger.Debug($"gh {command}");

        ExecuteGitHubCommandAndReturnOutput(command);
    }

    private string Sanitize(string value)
    {
        return value.Replace("\"", "\\\"");
    }
}
