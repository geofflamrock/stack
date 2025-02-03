using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class SetPullRequestLabelsCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to label PRs for.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("Labels to apply to the PRs. Can be provided multiple times.")]
    [CommandOption("-l|--label <VALUES>")]
    public string[]? Labels { get; set; }
}

public class SetPullRequestLabelsCommand : AsyncCommand<SetPullRequestLabelsCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SetPullRequestLabelsCommandSettings settings)
    {
        var console = AnsiConsole.Console;
        var outputProvider = new ConsoleOutputProvider(console);

        var handler = new SetPullRequestLabelsCommandHandler(
            new ConsoleInputProvider(console),
            outputProvider,
            new GitClient(outputProvider, settings.GetGitClientSettings()),
            new GitHubClient(outputProvider, settings.GetGitHubClientSettings()),
            new StackConfig());

        await handler.Handle(new SetPullRequestLabelsCommandInputs(settings.Stack, settings.Labels));

        return 0;
    }
}

public record SetPullRequestLabelsCommandInputs(string? Stack, string[]? Labels)
{
    public static SetPullRequestLabelsCommandInputs Empty => new(null, null);
}

public record SetPullRequestLabelsCommandResponse();

public class SetPullRequestLabelsCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
{
    public async Task<SetPullRequestLabelsCommandResponse> Handle(SetPullRequestLabelsCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            outputProvider.Information("No stacks found for current repository.");
            return new SetPullRequestLabelsCommandResponse();
        }

        var currentBranch = gitClient.GetCurrentBranch();
        var stack = inputProvider.SelectStack(outputProvider, inputs.Stack, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");
        }

        var status = StackHelpers.GetStackStatus(
            stack,
            currentBranch,
            outputProvider,
            gitClient,
            gitHubClient,
            true);

        var pullRequestsInStack = new List<GitHubPullRequest>();

        foreach (var branch in stack.Branches)
        {
            var branchDetail = status.Branches[branch];
            if (branchDetail.PullRequest is not null)
            {
                pullRequestsInStack.Add(branchDetail.PullRequest);
            }
        }

        if (pullRequestsInStack.Count == 0)
        {
            outputProvider.Information($"No pull requests found for stack {stack.Name.Branch()}");
            return new SetPullRequestLabelsCommandResponse();
        }

        if (StackHelpers.UpdateStackPullRequestLabels(inputProvider, outputProvider, gitHubClient, stackConfig, stacks, stack, inputs.Labels))
        {
            StackHelpers.UpdateLabelsInPullRequests(outputProvider, gitHubClient, stack, pullRequestsInStack);
        }
        else
        {
            outputProvider.Information("Labels have not changed.");
        }

        return new SetPullRequestLabelsCommandResponse();
    }
}