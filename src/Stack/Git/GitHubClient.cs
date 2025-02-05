using System.Text;
using Octopus.Shellfish;
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

public record GitHubPullRequest(int Number, string Title, string Body, string State, Uri Url, bool IsDraft, GitHubLabel[] Labels)
{
    public string[] LabelNames => [.. Labels.Select(l => l.Name)];
}

public record GitHubLabel(string Name);

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
        string[] labels,
        bool draft);
    void EditPullRequest(int number, string body);
    void OpenPullRequest(GitHubPullRequest pullRequest);
    string[] GetLabels();
}

public class GitHubClient(IOutputProvider outputProvider, GitHubClientSettings settings) : IGitHubClient
{
    public GitHubPullRequest? GetPullRequest(string branch)
    {
        var output = ExecuteGitHubCommandAndReturnOutput($"pr list --json title,number,body,state,url,isDraft,labels --head {branch} --state all");
        var pullRequests = System.Text.Json.JsonSerializer.Deserialize<List<GitHubPullRequest>>(output,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        return pullRequests.FirstOrDefault();
    }

    public GitHubPullRequest CreatePullRequest(
        string headBranch,
        string baseBranch,
        string title,
        string bodyFilePath,
        string[] labels,
        bool draft)
    {
        var command = $"pr create --title \"{Sanitize(title)}\" --body-file \"{bodyFilePath}\" --base {baseBranch} --head {headBranch}";

        if (draft)
        {
            command += " --draft";
        }

        if (labels.Length > 0)
        {
            command += " --label " + string.Join(',', labels.Select(l => Sanitize(l)));
        }

        ExecuteGitHubCommand(command);

        return GetPullRequest(headBranch) ?? throw new Exception("Failed to create pull request.");
    }

    public void EditPullRequest(int number, string body)
    {
        ExecuteGitHubCommand($"pr edit {number} --body \"{Sanitize(body)}\"");
    }

    public void OpenPullRequest(GitHubPullRequest pullRequest)
    {
        ExecuteGitHubCommand($"pr view {pullRequest.Number} --web");
    }

    public string[] GetLabels()
    {
        var output = ExecuteGitHubCommandAndReturnOutput($"label list --limit {int.MaxValue} --json name");
        var labels = System.Text.Json.JsonSerializer.Deserialize<List<GitHubLabel>>(output,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        return [.. labels.Select(l => l.Name)];
    }

    private string ExecuteGitHubCommandAndReturnOutput(string command)
    {
        if (settings.Verbose)
            outputProvider.Debug($"gh {command}");

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
            outputProvider.Error(Markup.Escape(errorBuilder.ToString()));
            throw new Exception("Failed to execute gh command.");
        }

        if (settings.Verbose && infoBuilder.Length > 0)
        {
            outputProvider.Debug(Markup.Escape(infoBuilder.ToString()));
        }

        return infoBuilder.ToString();
    }

    private void ExecuteGitHubCommand(string command)
    {
        if (settings.Verbose)
            outputProvider.Debug($"gh {command}");

        ExecuteGitHubCommandInternal(command);
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
            outputProvider.Error($"{errorBuilder}");
            throw new Exception("Failed to execute gh command.");
        }
        else
        {
            if (infoBuilder.Length > 0)
            {
                outputProvider.Debug(Markup.Escape(infoBuilder.ToString()));
            }
        }
    }

    private string Sanitize(string value)
    {
        return value.Replace("\"", "\\\"");
    }
}
