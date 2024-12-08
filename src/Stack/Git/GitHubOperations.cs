using System.Text;
using Octopus.Shellfish;
using Spectre.Console;

namespace Stack.Git;

public record GitHubOperationSettings(bool DryRun, bool Verbose, string? WorkingDirectory)
{
    public static GitHubOperationSettings Default => new(false, false, null);
}

public static class GitHubPullRequestStates
{
    public const string Open = "OPEN";
    public const string Closed = "CLOSED";
    public const string Merged = "MERGED";
}

public record GitHubPullRequest(int Number, string Title, string Body, string State, Uri Url);

public static class GitHubPullRequestExtensionMethods
{
    public static Color GetPullRequestColor(this GitHubPullRequest pullRequest)
    {
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
        return $"[{pullRequest.GetPullRequestColor()} link={pullRequest.Url}]#{pullRequest.Number}: {pullRequest.Title}[/]";
    }
}

public interface IGitHubOperations
{
    GitHubPullRequest? GetPullRequest(string branch);
    GitHubPullRequest? CreatePullRequest(string headBranch, string baseBranch, string title, string body);
    void EditPullRequest(int number, string body);
    void OpenPullRequest(GitHubPullRequest pullRequest);
}

public class GitHubOperations(IAnsiConsole console, GitHubOperationSettings settings) : IGitHubOperations
{
    public GitHubPullRequest? GetPullRequest(string branch)
    {
        var output = ExecuteGitHubCommandAndReturnOutput($"pr list --json title,number,body,state,url --head {branch} --state all");
        var pullRequests = System.Text.Json.JsonSerializer.Deserialize<List<GitHubPullRequest>>(output,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        return pullRequests.FirstOrDefault();
    }

    public GitHubPullRequest? CreatePullRequest(string headBranch, string baseBranch, string title, string body)
    {
        ExecuteGitHubCommand($"pr create --title \"{title}\" --body \"{body}\" --base {baseBranch} --head {headBranch}");

        if (settings.DryRun)
        {
            return null;
        }

        return GetPullRequest(headBranch);
    }

    public void EditPullRequest(int number, string body)
    {
        ExecuteGitHubCommand($"pr edit {number} --body \"{body}\"");
    }

    public void OpenPullRequest(GitHubPullRequest pullRequest)
    {
        ExecuteGitHubCommand($"pr view {pullRequest.Number} --web");
    }

    private string ExecuteGitHubCommandAndReturnOutput(string command)
    {
        if (settings.Verbose)
            console.MarkupLine($"[grey]gh {command}[/]");

        var infoBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        var result = ShellExecutor.ExecuteCommand(
            "gh",
            command,
            settings.WorkingDirectory ?? ".",
            (_) => { },
            (info) => infoBuilder.AppendLine(info),
            (error) => errorBuilder.AppendLine(error));

        if (result != 0)
        {
            console.MarkupLine($"[red]{errorBuilder}[/]");
            throw new Exception("Failed to execute gh command.");
        }

        if (settings.Verbose && infoBuilder.Length > 0)
        {
            console.MarkupLine($"[grey]{infoBuilder}[/]");
        }

        return infoBuilder.ToString();
    }

    private void ExecuteGitHubCommand(string command)
    {
        if (settings.Verbose)
            console.MarkupLine($"[grey]gh {command}[/]");

        if (!settings.DryRun)
        {
            ExecuteGitHubCommandInternal(command);
        }
    }

    private void ExecuteGitHubCommandInternal(string command)
    {
        var infoBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        var result = ShellExecutor.ExecuteCommand(
            "gh",
            command,
            settings.WorkingDirectory ?? ".",
            (_) => { },
            (info) => infoBuilder.AppendLine(info),
            (error) => errorBuilder.AppendLine(error));

        if (result != 0)
        {
            console.MarkupLine($"[red]{errorBuilder}[/]");
            throw new Exception("Failed to execute gh command.");
        }
        else
        {
            if (infoBuilder.Length > 0)
            {
                console.MarkupLine($"[grey]{infoBuilder}[/]");
            }
        }
    }
}
