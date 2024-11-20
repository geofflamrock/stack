using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;

namespace Stack.Commands;

public class UpdateStackCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to update.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

public class UpdateStackCommand(
    IAnsiConsole console,
    IGitOperations gitOperations,
    IStackConfig stackConfig) : AsyncCommand<UpdateStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, UpdateStackCommandSettings settings)
    {
        await Task.CompletedTask;

        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri(settings.GetGitOperationSettings());

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            console.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var currentBranch = gitOperations.GetCurrentBranch(settings.GetGitOperationSettings());
        var stackSelection = settings.Name ?? console.Prompt(Prompts.Stack(stacksForRemote, currentBranch));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        console.MarkupLine($"Stack: {stack.Name}");

        if (console.Prompt(new ConfirmationPrompt("Are you sure you want to update the branches in this stack?")))
        {
            void MergeFromSourceBranch(string branch, string sourceBranchName)
            {
                console.MarkupLine($"Merging [blue]{sourceBranchName}[/] into [blue]{branch}[/]");

                gitOperations.UpdateBranch(sourceBranchName, settings.GetGitOperationSettings());
                gitOperations.UpdateBranch(branch, settings.GetGitOperationSettings());
                gitOperations.MergeFromLocalSourceBranch(sourceBranchName, settings.GetGitOperationSettings());
                gitOperations.PushBranch(branch, settings.GetGitOperationSettings());
            }

            var sourceBranch = stack.SourceBranch;

            foreach (var branch in stack.Branches)
            {
                if (gitOperations.DoesRemoteBranchExist(branch, settings.GetGitOperationSettings()))
                {
                    MergeFromSourceBranch(branch, sourceBranch);
                    sourceBranch = branch;
                }
                else
                {
                    // Remote branch no longer exists, skip over
                    console.MarkupLine($"[red]Branch '{branch}' no longer exists on the remote repository. Skipping...[/]");
                }
            }

            if (stack.SourceBranch.Equals(currentBranch, StringComparison.InvariantCultureIgnoreCase) ||
                stack.Branches.Contains(currentBranch, StringComparer.OrdinalIgnoreCase))
            {
                gitOperations.ChangeBranch(currentBranch, settings.GetGitOperationSettings());
            }
        }

        return 0;
    }
}
