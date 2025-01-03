
using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Commands.Helpers;

namespace Stack.Commands;

public class CleanupStackCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to cleanup.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("Cleanup the stack without prompting.")]
    [CommandOption("-f|--force")]
    public bool Force { get; init; }
}

public class CleanupStackCommand : AsyncCommand<CleanupStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CleanupStackCommandSettings settings)
    {
        await Task.CompletedTask;

        var console = AnsiConsole.Console;
        var outputProvider = new ConsoleOutputProvider(console);

        var handler = new CleanupStackCommandHandler(
            new ConsoleInputProvider(console),
            outputProvider,
            new GitClient(outputProvider, settings.GetGitClientSettings()),
            new GitHubClient(outputProvider, settings.GetGitHubClientSettings()),
            new StackConfig());

        await handler.Handle(new CleanupStackCommandInputs(settings.Name, settings.Force));

        return 0;
    }
}

public record CleanupStackCommandInputs(string? Name, bool Force)
{
    public static CleanupStackCommandInputs Empty => new(null, false);
}

public record CleanupStackCommandResponse(string? CleanedUpStackName);

public class CleanupStackCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
{
    public async Task Handle(CleanupStackCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        var stack = inputProvider.SelectStack(outputProvider, inputs.Name, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.Name}' not found.");
        }

        var branchesInTheStackThatExistLocally = gitClient.GetBranchesThatExistLocally([.. stack.Branches]);
        var branchesInTheStackThatExistInTheRemote = gitClient.GetBranchesThatExistInRemote([.. stack.Branches]);

        var branchesToCleanUp = GetBranchesNeedingCleanup(stack, gitClient, gitHubClient);

        if (branchesToCleanUp.Length == 0)
        {
            outputProvider.Information("No branches to clean up");
            return;
        }

        if (!inputs.Force)
        {
            OutputBranchesNeedingCleanup(outputProvider, branchesToCleanUp);
        }

        if (inputs.Force || inputProvider.Confirm(Questions.ConfirmDeleteBranches))
        {
            CleanupBranches(gitClient, outputProvider, branchesToCleanUp);

            outputProvider.Information($"Stack {stack.Name.Stack()} cleaned up");
        }
    }

    public static string[] GetBranchesNeedingCleanup(Config.Stack stack, IGitClient gitClient, IGitHubClient gitHubClient)
    {
        var branchesInTheStackThatExistLocally = gitClient.GetBranchesThatExistLocally([.. stack.Branches]);
        var branchesInTheStackThatExistInTheRemote = gitClient.GetBranchesThatExistInRemote([.. stack.Branches]);

        var branchesThatCanBeCleanedUp = branchesInTheStackThatExistLocally.Except(branchesInTheStackThatExistInTheRemote).ToList();
        var branchesThatAreLocalAndInRemote = branchesInTheStackThatExistLocally.Intersect(branchesInTheStackThatExistInTheRemote);

        foreach (var branch in branchesThatAreLocalAndInRemote)
        {
            var pullRequest = gitHubClient.GetPullRequest(branch);

            if (pullRequest is not null && pullRequest.State != GitHubPullRequestStates.Open)
            {
                branchesThatCanBeCleanedUp.Add(branch);
            }
        }

        return branchesThatCanBeCleanedUp.ToArray();
    }

    public static void OutputBranchesNeedingCleanup(IOutputProvider outputProvider, string[] branches)
    {
        outputProvider.Information("The following branches exist locally but are either not in the remote repository or the pull request associated with the branch is no longer open:");

        foreach (var branch in branches)
        {
            outputProvider.Information($"  {branch.Branch()}");
        }
    }

    public static void CleanupBranches(IGitClient gitClient, IOutputProvider outputProvider, string[] branches)
    {
        foreach (var branch in branches)
        {
            outputProvider.Information($"Deleting local branch {branch.Branch()}");
            gitClient.DeleteLocalBranch(branch);
        }
    }
}
