
using System.ComponentModel;
using Humanizer;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;

namespace Stack.Commands;

internal class BranchCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to create the branch in.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("The name of the branch to add or create.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("The action to take.")]
    [CommandOption("-a|--action")]
    public BranchAction? Action { get; init; }
}

internal enum BranchAction
{
    [Description("Add an existing branch")]
    Add,

    [Description("Create a new branch")]
    Create
}

internal class BranchCommand(IAnsiConsole console, IGitOperations gitOperations) : AsyncCommand<BranchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BranchCommandSettings settings)
    {
        await Task.CompletedTask;

        var defaultBranch = gitOperations.GetDefaultBranch(settings.GetGitOperationSettings());
        var remoteUri = gitOperations.GetRemoteUri(settings.GetGitOperationSettings());
        var currentBranch = gitOperations.GetCurrentBranch(settings.GetGitOperationSettings());
        var branches = gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate(settings.GetGitOperationSettings());

        var stacks = StackConfig.Load();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            console.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var stackSelection = settings.Stack ?? console.Prompt(Prompts.Stack(stacksForRemote, currentBranch));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        var actionPromptOption = new SelectionPrompt<BranchAction>()
            .Title("Add or create a branch:")
            .AddChoices([BranchAction.Create, BranchAction.Add])
            .UseConverter(action => action.Humanize());

        var action = console.Prompt(actionPromptOption);

        if (action == BranchAction.Add)
        {
            return await new AddBranchCommand(console, gitOperations).ExecuteAsync(context, new AddBranchCommandSettings { Stack = stack.Name, Name = settings.Name, DryRun = settings.DryRun, Verbose = settings.Verbose });
        }
        else
        {
            return await new NewBranchCommand(console, gitOperations).ExecuteAsync(context, new NewBranchCommandSettings { Stack = stack.Name, Name = settings.Name, DryRun = settings.DryRun, Verbose = settings.Verbose });
        }
    }
}
