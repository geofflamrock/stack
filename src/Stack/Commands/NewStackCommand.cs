

using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;

namespace Stack.Commands;

internal class NewStackCommandSettings : UpdateCommandSettingsBase
{
    [Description("The name of the stack. Must be unique.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("The source branch to use for the new branch. Defaults to the default branch for the repository.")]
    [CommandOption("-s|--source-branch")]
    public string? SourceBranch { get; init; }

    [Description("The name of the branch to create within the stack.")]
    [CommandOption("-b|--branch")]
    public string? BranchName { get; init; }
}

internal class NewStackCommand : AsyncCommand<NewStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, NewStackCommandSettings settings)
    {
        await Task.CompletedTask;

        var defaultBranch = GitOperations.GetDefaultBranch(settings.GetGitOperationSettings());
        var currentBranch = GitOperations.GetCurrentBranch(settings.GetGitOperationSettings());
        var branches = GitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate(settings.GetGitOperationSettings());

        var name = settings.Name ?? AnsiConsole.Prompt(new TextPrompt<string>("Stack name:"));

        var branchesPrompt = new SelectionPrompt<string>().Title("Select a branch to start your stack from:").PageSize(10);

        branchesPrompt.AddChoices(branches);

        var sourceBranch = settings.SourceBranch ?? AnsiConsole.Prompt(branchesPrompt);
        AnsiConsole.WriteLine($"Source branch: {sourceBranch}");

        var stacks = StackConfig.Load();
        var remoteUri = GitOperations.GetRemoteUri(settings.GetGitOperationSettings());
        stacks.Add(new Config.Stack(name, remoteUri, sourceBranch, []));

        StackConfig.Save(stacks);

        AnsiConsole.WriteLine($"Stack '{name}' created from source branch '{sourceBranch}'");

        if (AnsiConsole.Prompt(new ConfirmationPrompt("Do you want to add an existing branch or create a new branch and add it to the stack?")))
        {
            return await new BranchCommand().ExecuteAsync(context, new BranchCommandSettings { Stack = name, Verbose = settings.Verbose });
        }

        return 0;
    }
}
