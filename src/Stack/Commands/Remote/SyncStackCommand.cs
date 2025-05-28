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
    [Description("The name of the stack to sync with the remote.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

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

    [Description("Confirm the sync without prompting.")]
    [CommandOption("--yes")]
    public bool Confirm { get; init; }
}

public class SyncStackCommand : Command<SyncStackCommandSettings>
{
    protected override async Task Execute(SyncStackCommandSettings settings)
    {
        var handler = new SyncStackCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, settings.GetGitClientSettings()),
            new GitHubClient(StdErrLogger, settings.GetGitHubClientSettings()),
            new FileStackConfig());

        await handler.Handle(new SyncStackCommandInputs(
            settings.Stack,
            settings.MaxBatchSize,
            settings.Rebase,
            settings.Merge,
            settings.Confirm));
    }
}

public record SyncStackCommandInputs(
    string? Stack,
    int MaxBatchSize,
    bool? Rebase,
    bool? Merge,
    bool Confirm)
{
    public static SyncStackCommandInputs Empty => new(null, 5, null, null, false);
}

public class SyncStackCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<SyncStackCommandInputs>
{
    public override async Task Handle(SyncStackCommandInputs inputs)
    {
        await Task.CompletedTask;

        if (inputs.Rebase == true && inputs.Merge == true)
            throw new InvalidOperationException("Cannot specify both rebase and merge.");

        var stackData = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            return;
        }

        var currentBranch = gitClient.GetCurrentBranch();

        var stack = inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch);

        if (stack is null)
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");

        FetchChanges();

        var status = StackHelpers.GetStackStatus(
            stack,
            currentBranch,
            logger,
            gitClient,
            gitHubClient,
            true);

        StackHelpers.OutputStackStatus(stackData.SchemaVersion, status, logger);

        logger.NewLine();

        if (inputs.Confirm || inputProvider.Confirm(Questions.ConfirmSyncStack))
        {
            logger.Information($"Syncing stack {stack.Name.Stack()} with the remote repository");

            StackHelpers.PullChanges(stack, gitClient, logger);

            StackHelpers.UpdateStack(
                stack,
                status,
                inputs.Merge == true ? UpdateStrategy.Merge : inputs.Rebase == true ? UpdateStrategy.Rebase : null,
                gitClient,
                inputProvider,
                logger);

            var forceWithLease = inputs.Rebase == true || StackHelpers.GetUpdateStrategyConfigValue(gitClient) == UpdateStrategy.Rebase;

            StackHelpers.PushChanges(stack, inputs.MaxBatchSize, forceWithLease, gitClient, logger);

            if (stack.SourceBranch.Equals(currentBranch, StringComparison.InvariantCultureIgnoreCase) ||
                stack.AllBranchNames.Contains(currentBranch, StringComparer.OrdinalIgnoreCase))
            {
                gitClient.ChangeBranch(currentBranch);
            }
        }
    }

    private void FetchChanges()
    {
        logger.Status("Fetching changes from remote repository", () =>
        {
            gitClient.Fetch(true);
        });
    }
}