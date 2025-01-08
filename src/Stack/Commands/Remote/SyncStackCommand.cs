using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class SyncStackCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to update.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("Don't ask for confirmation before syncing the stack.")]
    [CommandOption("-y|--yes")]
    public bool NoConfirm { get; init; }

    [Description("The maximum number of branches to push changes for at once.")]
    [CommandOption("--max-batch-size")]
    [DefaultValue(5)]
    public int MaxBatchSize { get; init; } = 5;

    [Description("Use rebase instead of merge when updating the stack.")]
    [CommandOption("--rebase")]
    public bool Rebase { get; init; }
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
            settings.NoConfirm,
            settings.MaxBatchSize,
            settings.Rebase));

        return 0;
    }
}

public record SyncStackCommandInputs(
    string? Name,
    bool NoConfirm,
    int MaxBatchSize,
    bool Rebase)
{
    public static SyncStackCommandInputs Empty => new(null, false, 5, false);
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

        if (inputs.NoConfirm || inputProvider.Confirm(Questions.ConfirmSyncStack))
        {
            outputProvider.Information($"Syncing stack {stack.Name.Stack()} with the remote repository");

            StackHelpers.PullChanges(stack, gitClient, outputProvider);

            StackHelpers.UpdateStack(
                stack,
                status,
                inputs.Rebase ? UpdateStrategy.Rebase : UpdateStrategy.Merge,
                gitClient,
                inputProvider,
                outputProvider);

            StackHelpers.PushChanges(stack, inputs.MaxBatchSize, inputs.Rebase, gitClient, outputProvider);

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