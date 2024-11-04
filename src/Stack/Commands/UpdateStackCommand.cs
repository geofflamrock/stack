using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;

namespace Stack.Commands;

internal class UpdateStackCommandSettings : UpdateCommandSettingsBase
{
    [Description("The name of the stack to update.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

internal class UpdateStackCommand : AsyncCommand<UpdateStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, UpdateStackCommandSettings settings)
    {
        await Task.CompletedTask;

        var stacks = StackConfig.Load();

        var remoteUri = GitOperations.GetRemoteUri(settings.GetGitOperationSettings());

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            AnsiConsole.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var stackSelection = settings.Name ?? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Select a stack to update:").PageSize(10).AddChoices(stacksForRemote.Select(s => s.Name).ToArray()));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));
        var currentBranch = GitOperations.GetCurrentBranch();

        AnsiConsole.MarkupLine($"Stack: {stack.Name}");

        if (AnsiConsole.Prompt(new ConfirmationPrompt("Are you sure you want to update the branches in this stack?")))
        {
            void MergeFromSourceBranch(string branch, string sourceBranchName)
            {
                AnsiConsole.MarkupLine($"Merging [blue]{sourceBranchName}[/] into [blue]{branch}[/]");

                GitOperations.UpdateBranch(sourceBranchName, settings.GetGitOperationSettings());
                GitOperations.UpdateBranch(branch, settings.GetGitOperationSettings());
                GitOperations.MergeFromLocalSourceBranch(sourceBranchName, settings.GetGitOperationSettings());
                GitOperations.PushBranch(branch, settings.GetGitOperationSettings());
            }

            var sourceBranch = stack.SourceBranch;

            foreach (var branch in stack.Branches)
            {
                if (GitOperations.DoesRemoteBranchExist(branch))
                {
                    MergeFromSourceBranch(branch, sourceBranch);
                    sourceBranch = branch;
                }
                else
                {
                    // Remote branch no longer exists, skip over
                    AnsiConsole.MarkupLine($"[red]Branch '{branch}' no longer exists on the remote repository. Skipping...[/]");
                }
            }

            if (stack.SourceBranch.Equals(currentBranch, StringComparison.InvariantCultureIgnoreCase) ||
                stack.Branches.Contains(currentBranch, StringComparer.OrdinalIgnoreCase))
            {
                GitOperations.ChangeBranch(currentBranch, settings.GetGitOperationSettings());
            }
        }

        return 0;
    }
}
