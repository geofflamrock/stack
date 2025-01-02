using System.ComponentModel;
using Humanizer;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class UpdateStackCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to update.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

public class UpdateStackCommand : AsyncCommand<UpdateStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, UpdateStackCommandSettings settings)
    {
        var console = AnsiConsole.Console;
        var outputProvider = new ConsoleOutputProvider(console);

        var handler = new UpdateStackCommandHandler(
            new ConsoleInputProvider(console),
            outputProvider,
            new GitClient(outputProvider, settings.GetGitClientSettings()),
            new GitHubClient(outputProvider, settings.GetGitHubClientSettings()),
            new StackConfig());

        await handler.Handle(new UpdateStackCommandInputs(settings.Name));

        return 0;
    }
}

public record UpdateStackCommandInputs(string? Name)
{
    public static UpdateStackCommandInputs Empty => new((string?)null);
}

public record UpdateStackCommandResponse();

public class UpdateStackCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
{
    public async Task<UpdateStackCommandResponse> Handle(UpdateStackCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            return new UpdateStackCommandResponse();
        }

        var currentBranch = gitClient.GetCurrentBranch();

        var stack = inputProvider.SelectStack(outputProvider, inputs.Name, stacksForRemote, currentBranch);

        if (stack is null)
            throw new InvalidOperationException($"Stack '{inputs.Name}' not found.");

        var status = StackHelpers.GetStackStatus(
            stack,
            currentBranch,
            outputProvider,
            gitClient,
            gitHubClient,
            false);

        StackHelpers.UpdateStack(stack, status, gitClient, outputProvider);

        if (stack.SourceBranch.Equals(currentBranch, StringComparison.InvariantCultureIgnoreCase) ||
            stack.Branches.Contains(currentBranch, StringComparer.OrdinalIgnoreCase))
        {
            gitClient.ChangeBranch(currentBranch);
        }

        return new UpdateStackCommandResponse();
    }
}