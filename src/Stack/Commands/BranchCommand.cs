
using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;

namespace Stack.Commands;

internal class BranchCommandSettings : UpdateCommandSettingsBase
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

internal class BranchCommand : AsyncCommand<BranchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BranchCommandSettings settings)
    {
        await Task.CompletedTask;

        var defaultBranch = GitOperations.GetDefaultBranch(settings.GetGitOperationSettings());
        var remoteUri = GitOperations.GetRemoteUri(settings.GetGitOperationSettings());
        var currentBranch = GitOperations.GetCurrentBranch(settings.GetGitOperationSettings());
        var branches = GitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate(settings.GetGitOperationSettings());

        var stacks = StackConfig.Load();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            AnsiConsole.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var stackSelection = settings.Stack ?? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Select a stack:").PageSize(10).AddChoices(stacksForRemote.Select(s => s.Name).ToArray()));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        var actionPromptOption = new SelectionPrompt<BranchAction>()
            .Title("Add or create a branch:")
            .AddChoices([BranchAction.Add, BranchAction.Create]);

        actionPromptOption.Converter = (action) =>
        {
            var field = action.GetType().GetField(action.ToString());
            if (field == null)
                return action.ToString();

            var attributes = field.GetCustomAttributes(typeof(DescriptionAttribute), false);
            if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
            {
                return attribute.Description;
            }

            return action.ToString();
        };
        var action = AnsiConsole.Prompt(actionPromptOption);

        if (action == BranchAction.Add)
        {
            return await new AddBranchCommand().ExecuteAsync(context, new AddBranchCommandSettings { Stack = stack.Name, Name = settings.Name, DryRun = settings.DryRun, Verbose = settings.Verbose });
        }
        else
        {
            return await new NewBranchCommand().ExecuteAsync(context, new NewBranchCommandSettings { Stack = stack.Name, Name = settings.Name, DryRun = settings.DryRun, Verbose = settings.Verbose });
        }
    }
}
