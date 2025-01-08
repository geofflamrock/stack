using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class SyncStackCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to update.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("The maximum number of branches to push changes for at once.")]
    [CommandOption("--max-batch-size")]
    [DefaultValue(5)]
    public int MaxBatchSize { get; init; } = 5;

    [Description("Use rebase when updating the stack. Overrides any setting in Git configuration.")]
    [CommandOption("--rebase")]
    public bool? Rebase { get; init; }

    [Description("Use merge when updating the stack. Overrides any setting in Git configuration.")]
    [CommandOption("--merge")]
    public bool? Merge { get; init; }
}

public class SyncStackCommand : AsyncCommand<SyncStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SyncStackCommandSettings settings)
    {
        var console = AnsiConsole.Console;
        var outputProvider = new ConsoleOutputProvider(console);

        var handler = new SyncStackCommandHandler(
            new ConsoleInputProvider(console),
            outputProvider,
            new GitClient(outputProvider, settings.GetGitClientSettings()),
            new GitHubClient(outputProvider, settings.GetGitHubClientSettings()),
            new StackConfig());

        await handler.Handle(new SyncStackCommandInputs(
            settings.Name,
            settings.MaxBatchSize,
            settings.Rebase,
            settings.Merge));

        return 0;
    }
}

public record SyncStackCommandInputs(
    string? Name,
    int MaxBatchSize,
    bool? Rebase,
    bool? Merge)
{
    public static SyncStackCommandInputs Empty => new(null, 5, null, null);
}

public record SyncStackCommandResponse();

public class SyncStackCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
{
    public async Task<SyncStackCommandResponse> Handle(SyncStackCommandInputs inputs)
    {
        await Task.CompletedTask;

        if (inputs.Rebase == true && inputs.Merge == true)
            throw new InvalidOperationException("Cannot specify both rebase and merge.");

        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            return new SyncStackCommandResponse();
        }

        var currentBranch = gitClient.GetCurrentBranch();

        var stack = inputProvider.SelectStack(outputProvider, inputs.Name, stacksForRemote, currentBranch);

        if (stack is null)
            throw new InvalidOperationException($"Stack '{inputs.Name}' not found.");

        FetchChanges();

        var status = StackHelpers.GetStackStatus(
            stack,
            currentBranch,
            outputProvider,
            gitClient,
            gitHubClient,
            true);

        StackHelpers.OutputStackStatus(stack, status, outputProvider);

        outputProvider.NewLine();

        if (inputProvider.Confirm(Questions.ConfirmSyncStack))
        {
            outputProvider.Information($"Syncing stack {stack.Name.Stack()} with the remote repository");

            StackHelpers.PullChanges(stack, gitClient, outputProvider);

            StackHelpers.UpdateStack(
                stack,
                status,
                inputs.Merge == true ? UpdateStrategy.Merge : inputs.Rebase == true ? UpdateStrategy.Rebase : null,
                gitClient,
                inputProvider,
                outputProvider);

            var forceWithLease = inputs.Rebase == true || StackHelpers.GetUpdateStrategyConfigValue(gitClient) == UpdateStrategy.Rebase;

            StackHelpers.PushChanges(stack, inputs.MaxBatchSize, forceWithLease, gitClient, outputProvider);

            if (stack.SourceBranch.Equals(currentBranch, StringComparison.InvariantCultureIgnoreCase) ||
                stack.Branches.Contains(currentBranch, StringComparer.OrdinalIgnoreCase))
            {
                gitClient.ChangeBranch(currentBranch);
            }
        }

        return new SyncStackCommandResponse();
    }

    private void FetchChanges()
    {
        outputProvider.Status("Fetching changes from remote repository", () =>
        {
            gitClient.Fetch(true);
        });
    }
}