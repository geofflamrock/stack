using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class OpenPullRequestsCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to open PRs for.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

public class OpenPullRequestsCommand : AsyncCommand<OpenPullRequestsCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, OpenPullRequestsCommandSettings settings)
    {
        var console = AnsiConsole.Console;

        var handler = new OpenPullRequestsCommandHandler(
            new ConsoleInputProvider(console),
            new ConsoleOutputProvider(console),
            new GitOperations(console, settings.GetGitOperationSettings()),
            new GitHubOperations(console, settings.GetGitHubOperationSettings()),
            new StackConfig());

        await handler.Handle(new OpenPullRequestsCommandInputs(settings.Name));

        return 0;
    }
}

public record OpenPullRequestsCommandInputs(string? StackName)
{
    public static OpenPullRequestsCommandInputs Empty => new((string?)null);
}

public record OpenPullRequestsCommandResponse();

public class OpenPullRequestsCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitOperations gitOperations,
    IGitHubOperations gitHubOperations,
    IStackConfig stackConfig)
{
    public async Task<OpenPullRequestsCommandResponse> Handle(OpenPullRequestsCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            outputProvider.Information("No stacks found for current repository.");
            return new OpenPullRequestsCommandResponse();
        }

        var currentBranch = gitOperations.GetCurrentBranch();
        var stack = InputHelpers.SelectStack(inputProvider, inputs.StackName, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.StackName}' not found.");
        }

        var pullRequestsInStack = new List<GitHubPullRequest>();

        foreach (var branch in stack.Branches)
        {
            var existingPullRequest = gitHubOperations.GetPullRequest(branch);

            if (existingPullRequest is not null && existingPullRequest.State != GitHubPullRequestStates.Closed)
            {
                pullRequestsInStack.Add(existingPullRequest);
            }
        }

        if (pullRequestsInStack.Count == 0)
        {
            outputProvider.Information($"No pull requests found for stack {stack.Name.Branch()}");
            return new OpenPullRequestsCommandResponse();
        }

        foreach (var pullRequest in pullRequestsInStack)
        {
            gitHubOperations.OpenPullRequest(pullRequest);
        }

        return new OpenPullRequestsCommandResponse();
    }
}