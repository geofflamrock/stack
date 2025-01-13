using System.ComponentModel;
using Humanizer;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class UpdateStackCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to update.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("Use rebase when updating the stack. Overrides any setting in Git configuration.")]
    [CommandOption("--rebase")]
    public bool? Rebase { get; init; }

    [Description("Use merge when updating the stack. Overrides any setting in Git configuration.")]
    [CommandOption("--merge")]
    public bool? Merge { get; init; }
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

        await handler.Handle(new UpdateStackCommandInputs(settings.Stack, settings.Rebase, settings.Merge));

        return 0;
    }
}

public record UpdateStackCommandInputs(string? Stack, bool? Rebase, bool? Merge)
{
    public static UpdateStackCommandInputs Empty => new(null, null, null);
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

        if (inputs.Rebase == true && inputs.Merge == true)
            throw new InvalidOperationException("Cannot specify both rebase and merge.");

        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            return new UpdateStackCommandResponse();
        }

        var currentBranch = gitClient.GetCurrentBranch();

        var stack = inputProvider.SelectStack(outputProvider, inputs.Stack, stacksForRemote, currentBranch);

        if (stack is null)
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");

        var status = StackHelpers.GetStackStatus(
            stack,
            currentBranch,
            outputProvider,
            gitClient,
            gitHubClient,
            false);

        StackHelpers.UpdateStack(
            stack,
            status,
            inputs.Merge == true ? UpdateStrategy.Merge : inputs.Rebase == true ? UpdateStrategy.Rebase : null,
            gitClient,
            inputProvider,
            outputProvider);

        if (stack.SourceBranch.Equals(currentBranch, StringComparison.InvariantCultureIgnoreCase) ||
            stack.Branches.Contains(currentBranch, StringComparer.OrdinalIgnoreCase))
        {
            gitClient.ChangeBranch(currentBranch);
        }

        return new UpdateStackCommandResponse();
    }
}