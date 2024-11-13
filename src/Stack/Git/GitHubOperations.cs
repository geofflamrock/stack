using System.Text;
using Octopus.Shellfish;
using Spectre.Console;

namespace Stack.Git;

internal record GitHubOperationSettings(bool DryRun, bool Verbose, string? WorkingDirectory)
{
    public static GitHubOperationSettings Default => new(false, false, null);
}

internal static class GitHubPullRequestStates
{
    public const string Open = "OPEN";
    public const string Closed = "CLOSED";
    public const string Merged = "MERGED";
}

internal record GitHubPullRequest(int Number, string Title, string Body, string State, Uri Url);

internal static class GitHubPullRequestExtensionMethods
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

internal interface IGitHubOperations
{
    GitHubPullRequest? GetPullRequest(string branch, GitHubOperationSettings settings);
    GitHubPullRequest? CreatePullRequest(string headBranch, string baseBranch, string title, string body, GitHubOperationSettings settings);
    void EditPullRequest(int number, string body, GitHubOperationSettings settings);
}

internal class GitHubOperations(IAnsiConsole console) : IGitHubOperations
{
    public GitHubPullRequest? GetPullRequest(string branch, GitHubOperationSettings settings)
    {
        var output = ExecuteGitHubCommandAndReturnOutput($"pr list --json title,number,body,state,url --head {branch} --state all", settings);
        var pullRequests = System.Text.Json.JsonSerializer.Deserialize<List<GitHubPullRequest>>(output,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        return pullRequests.FirstOrDefault();
    }

    public GitHubPullRequest? CreatePullRequest(string headBranch, string baseBranch, string title, string body, GitHubOperationSettings settings)
    {
        ExecuteGitHubCommand($"pr create --title \"{title}\" --body \"{body}\" --base {baseBranch} --head {headBranch}", settings);

        if (settings.DryRun)
        {
            return null;
        }

        return GetPullRequest(headBranch, settings);
    }

    public void EditPullRequest(int number, string body, GitHubOperationSettings settings)
    {
        ExecuteGitHubCommand($"pr edit {number} --body \"{body}\"", settings);
    }

    private string ExecuteGitHubCommandAndReturnOutput(string command, GitHubOperationSettings settings)
    {
        if (settings.Verbose)
            console.MarkupLine($"[grey]git {command}[/]");

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
            console.WriteLine(infoBuilder.ToString());
        }

        return infoBuilder.ToString();
    }

    private void ExecuteGitHubCommand(string command, GitHubOperationSettings settings)
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
            ".",
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
                console.WriteLine(infoBuilder.ToString());
            }
        }
    }
}
