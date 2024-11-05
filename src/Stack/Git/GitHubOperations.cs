using System.Text;
using Octopus.Shellfish;
using Spectre.Console;

namespace Stack.Git;

internal record GitHubOperationSettings(bool DryRun, bool Verbose)
{
    public static GitHubOperationSettings Default => new GitHubOperationSettings(false, false);
}

internal static class GitHubPullRequestStates
{
    public static string Open = "OPEN";
    public static string Closed = "CLOSED";
    public static string Merged = "MERGED";
}

internal record GitHubPullRequest(int Number, string Title, string State, Uri Url);

internal static class GitHubOperations
{

    public static GitHubPullRequest? GetPullRequest(string branch, GitHubOperationSettings settings)
    {
        var output = ExecuteGitHubCommandAndReturnOutput($"pr list --json title,number,state,url --head {branch} --state all", settings);
        var pullRequests = System.Text.Json.JsonSerializer.Deserialize<List<GitHubPullRequest>>(output,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        return pullRequests.FirstOrDefault();
    }

    private static string ExecuteGitHubCommandAndReturnOutput(string command, GitHubOperationSettings settings)
    {
        if (settings.Verbose)
            AnsiConsole.MarkupLine($"[grey]git {command}[/]");

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
            AnsiConsole.MarkupLine($"[red]{errorBuilder}[/]");
            throw new Exception("Failed to execute gh command.");
        }

        if (settings.Verbose && infoBuilder.Length > 0)
        {
            AnsiConsole.WriteLine(infoBuilder.ToString());
        }

        return infoBuilder.ToString();
    }

    private static void ExecuteGitHubCommand(string command, GitHubOperationSettings settings)
    {
        if (settings.Verbose)
            AnsiConsole.MarkupLine($"[grey]gh {command}[/]");

        if (!settings.DryRun)
        {
            ExecuteGitHubCommandInternal(command);
        }
    }

    private static void ExecuteGitHubCommandInternal(string command)
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
            AnsiConsole.MarkupLine($"[red]{errorBuilder}[/]");
            throw new Exception("Failed to execute gh command.");
        }
        else
        {
            if (infoBuilder.Length > 0)
            {
                AnsiConsole.WriteLine(infoBuilder.ToString());
            }
        }
    }
}
