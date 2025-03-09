using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class StackStatusCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to show the status of.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("Show status of all stacks.")]
    [CommandOption("--all")]
    public bool All { get; init; }

    [Description("Show full status including pull requests.")]
    [CommandOption("--full")]
    public bool Full { get; init; }
}

public class StackStatusCommand : CommandWithOutput<StackStatusCommandSettings, StackStatusCommandResponse>
{
    protected override async Task<StackStatusCommandResponse> Execute(StackStatusCommandSettings settings)
    {
        var handler = new StackStatusCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, settings.GetGitClientSettings()),
            new GitHubClient(StdErrLogger, settings.GetGitHubClientSettings()),
            new StackConfig());

        return await handler.Handle(new StackStatusCommandInputs(settings.Stack, settings.All, settings.Full));
    }

    protected override void WriteOutput(StackStatusCommandSettings settings, StackStatusCommandResponse response)
    {
        StackHelpers.OutputStackStatus(response.Statuses, StdOutLogger);

        if (response.Statuses.Count == 1)
        {
            var (stack, status) = response.Statuses.First();
            StackHelpers.OutputBranchAndStackActions(stack, status, StdOutLogger);
        }
    }
}

public record StackStatusCommandInputs(string? Stack, bool All, bool Full);
public record StackStatusCommandResponse(Dictionary<Config.Stack, StackStatus> Statuses);

public class StackStatusCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<StackStatusCommandInputs, StackStatusCommandResponse>
{
    public override async Task<StackStatusCommandResponse> Handle(StackStatusCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksToCheckStatusFor = new List<Config.Stack>();

        if (inputs.All)
        {
            stacksToCheckStatusFor.AddRange(stacksForRemote);
        }
        else
        {
            var stack = inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch);

            if (stack is null)
            {
                throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");
            }

            stacksToCheckStatusFor.Add(stack);
        }

        var stackStatusResults = StackHelpers.GetStackStatus(
            stacksToCheckStatusFor,
            currentBranch,
            logger,
            gitClient,
            gitHubClient,
            inputs.Full);

        return new StackStatusCommandResponse(stackStatusResults);
    }
}
