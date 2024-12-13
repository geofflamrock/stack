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

    [Description("Force the update of the stack.")]
    [CommandOption("-f|--force")]
    public bool Force { get; init; }
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
            new GitOperations(outputProvider, settings.GetGitOperationSettings()),
            new GitHubOperations(outputProvider, settings.GetGitHubOperationSettings()),
            new StackConfig());

        await handler.Handle(new UpdateStackCommandInputs(settings.Name, settings.Force));

        return 0;
    }
}

public record UpdateStackCommandInputs(string? Name, bool Force)
{
    public static UpdateStackCommandInputs Empty => new(null, false);
}

public record UpdateStackCommandResponse();

public class UpdateStackCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitOperations gitOperations,
    IGitHubOperations gitHubOperations,
    IStackConfig stackConfig)
{
    public async Task<UpdateStackCommandResponse> Handle(UpdateStackCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            return new UpdateStackCommandResponse();
        }

        var currentBranch = gitOperations.GetCurrentBranch();

        var stack = inputProvider.SelectStack(outputProvider, inputs.Name, stacksForRemote, currentBranch);

        if (stack is null)
            throw new InvalidOperationException($"Stack '{inputs.Name}' not found.");

        if (inputs.Force || inputProvider.Confirm(Questions.ConfirmUpdateStack))
        {
            void MergeFromSourceBranch(string branch, string sourceBranchName)
            {
                outputProvider.Information($"Merging {sourceBranchName.Stack()} into {branch.Branch()}");

                gitOperations.UpdateBranch(sourceBranchName);
                gitOperations.UpdateBranch(branch);
                gitOperations.MergeFromLocalSourceBranch(sourceBranchName);
                gitOperations.PushBranch(branch);
            }

            var sourceBranch = stack.SourceBranch;

            foreach (var branch in stack.Branches)
            {
                var pullRequest = gitHubOperations.GetPullRequest(branch);

                if (gitOperations.DoesRemoteBranchExist(branch) &&
                    (pullRequest is null || pullRequest.State != GitHubPullRequestStates.Merged))
                {
                    MergeFromSourceBranch(branch, sourceBranch);
                    sourceBranch = branch;
                }
                else
                {
                    outputProvider.Debug($"Branch '{branch}' no longer exists on the remote repository or the associated pull request is no longer open. Skipping...");
                }
            }

            if (stack.SourceBranch.Equals(currentBranch, StringComparison.InvariantCultureIgnoreCase) ||
                stack.Branches.Contains(currentBranch, StringComparer.OrdinalIgnoreCase))
            {
                gitOperations.ChangeBranch(currentBranch);
            }
        }

        return new UpdateStackCommandResponse();
    }
}