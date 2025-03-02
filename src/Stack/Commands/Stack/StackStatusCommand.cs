using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
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

public class StackStatusCommand : CommandBase<StackStatusCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, StackStatusCommandSettings settings)
    {
        var handler = new StackStatusCommandHandler(
            InputProvider,
            OutputProvider,
            new GitClient(OutputProvider, settings.GetGitClientSettings()),
            new GitHubClient(OutputProvider, settings.GetGitHubClientSettings()),
            new StackConfig());

        await handler.Handle(new StackStatusCommandInputs(settings.Stack, settings.All, settings.Full));

        return 0;
    }
}

public record StackStatusCommandInputs(string? Stack, bool All, bool Full);
public record StackStatusCommandResponse(Dictionary<Config.Stack, StackStatus> Statuses);

public class StackStatusCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
{
    public async Task<StackStatusCommandResponse> Handle(StackStatusCommandInputs inputs)
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
            var stack = inputProvider.SelectStack(outputProvider, inputs.Stack, stacksForRemote, currentBranch);

            if (stack is null)
            {
                throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");
            }

            stacksToCheckStatusFor.Add(stack);
        }

        var stackStatusResults = StackHelpers.GetStackStatus(
            stacksToCheckStatusFor,
            currentBranch,
            outputProvider,
            gitClient,
            gitHubClient,
            inputs.Full);

        if (stackStatusResults.Count == 1)
        {
            outputProvider.NewLine();
        }

        StackHelpers.OutputStackStatus(stackStatusResults, outputProvider);

        if (stacksToCheckStatusFor.Count == 1)
        {
            var (stack, status) = stackStatusResults.First();
            StackHelpers.OutputBranchAndStackActions(stack, status, outputProvider);
        }

        return new StackStatusCommandResponse(stackStatusResults);
    }
}
