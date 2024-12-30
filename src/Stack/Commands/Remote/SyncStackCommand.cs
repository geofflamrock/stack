using System.ComponentModel;
using Humanizer;
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
            new GitOperations(outputProvider, settings.GetGitOperationSettings()),
            new GitHubOperations(outputProvider, settings.GetGitHubOperationSettings()),
            new StackConfig());

        await handler.Handle(new SyncStackCommandInputs(settings.Name, settings.NoConfirm, settings.MaxBatchSize));

        return 0;
    }
}

public record SyncStackCommandInputs(string? Name, bool NoConfirm, int MaxBatchSize)
{
    public static SyncStackCommandInputs Empty => new(null, false, 5);
}

public record SyncStackCommandResponse();

public class SyncStackCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitOperations gitOperations,
    IGitHubOperations gitHubOperations,
    IStackConfig stackConfig)
{
    public async Task<SyncStackCommandResponse> Handle(SyncStackCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            return new SyncStackCommandResponse();
        }

        var currentBranch = gitOperations.GetCurrentBranch();

        var stack = inputProvider.SelectStack(outputProvider, inputs.Name, stacksForRemote, currentBranch);

        if (stack is null)
            throw new InvalidOperationException($"Stack '{inputs.Name}' not found.");

        var status = StackStatusHelpers.GetStackStatus(
            stack,
            currentBranch,
            outputProvider,
            gitOperations,
            gitHubOperations,
            true);

        StackStatusHelpers.OutputStackStatus(stack, status, outputProvider);

        outputProvider.NewLine();

        if (inputs.NoConfirm || inputProvider.Confirm(Questions.ConfirmSyncStack))
        {
            outputProvider.Information($"Syncing stack {stack.Name.Stack()} with the remote repository");

            FetchChanges();

            PullChanges(stack);

            UpdateStack(stack, status);

            PushChanges(stack, inputs);

            if (stack.SourceBranch.Equals(currentBranch, StringComparison.InvariantCultureIgnoreCase) ||
                stack.Branches.Contains(currentBranch, StringComparer.OrdinalIgnoreCase))
            {
                gitOperations.ChangeBranch(currentBranch);
            }
        }

        return new SyncStackCommandResponse();
    }

    private void FetchChanges()
    {
        outputProvider.Information("Fetching changes from remote repository");
        gitOperations.Fetch(true);
    }

    private void PullChanges(Config.Stack stack)
    {
        var branchStatus = gitOperations.GetBranchStatuses([stack.SourceBranch, .. stack.Branches]);

        foreach (var branch in branchStatus.Where(b => b.Value.RemoteBranchExists))
        {
            outputProvider.Information($"Pulling changes for {branch.Value.BranchName.Branch()} from remote");
            gitOperations.ChangeBranch(branch.Value.BranchName);
            gitOperations.PullBranch(branch.Value.BranchName);
        }
    }

    private void UpdateStack(Config.Stack stack, StackStatus status)
    {
        void MergeFromSourceBranch(string branch, string sourceBranchName)
        {
            outputProvider.Information($"Merging {sourceBranchName.Branch()} into {branch.Branch()}");
            gitOperations.ChangeBranch(branch);
            gitOperations.MergeFromLocalSourceBranch(sourceBranchName);
        }

        var sourceBranch = stack.SourceBranch;

        foreach (var branch in stack.Branches)
        {
            var branchDetail = status.Branches[branch];

            if (branchDetail.IsActive)
            {
                MergeFromSourceBranch(branch, sourceBranch);
                sourceBranch = branch;
            }
            else
            {
                outputProvider.Debug($"Branch '{branch}' no longer exists on the remote repository or the associated pull request is no longer open. Skipping...");
            }
        }
    }

    private void PushChanges(Config.Stack stack, SyncStackCommandInputs inputs)
    {
        var branchStatus = gitOperations.GetBranchStatuses([.. stack.Branches]);

        var branchesThatHaveNotBeenPushedToRemote = branchStatus.Where(b => b.Value.RemoteTrackingBranchName is null).Select(b => b.Value.BranchName).ToList();

        foreach (var branch in branchesThatHaveNotBeenPushedToRemote)
        {
            outputProvider.Information($"Pushing new branch {branch.Branch()} to remote");
            gitOperations.PushNewBranch(branch);
        }

        var branchesInStackWithRemote = branchStatus.Where(b => b.Value.RemoteBranchExists).Select(b => b.Value.BranchName).ToList();

        var branchGroupsToPush = branchesInStackWithRemote
            .Select((b, i) => new { Index = i, Value = b })
            .GroupBy(b => b.Index / inputs.MaxBatchSize)
            .Select(g => g.Select(b => b.Value).ToList())
            .ToList();

        foreach (var branches in branchGroupsToPush)
        {
            outputProvider.Information($"Pushing changes for {string.Join(", ", branches.Select(b => b.Branch()))} to remote");

            gitOperations.PushBranches([.. branches], false, false);
        }
    }
}