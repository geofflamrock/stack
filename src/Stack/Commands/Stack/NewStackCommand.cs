

using System.ComponentModel;
using Humanizer;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class NewStackCommandSettings : CommandSettingsBase
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

public enum BranchAction
{
    [Description("Add an existing branch")]
    Add,

    [Description("Create a new branch")]
    Create
}

public class NewStackCommand : AsyncCommand<NewStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, NewStackCommandSettings settings)
    {
        var console = AnsiConsole.Console;
        var handler = new NewStackCommandHandler(
            new NewStackCommandInputProvider(new ConsoleInputProvider(console)),
            new GitOperations(console, settings.GetGitOperationSettings()),
            new StackConfig());

        var response = await handler.Handle(
            new NewStackCommandInputs(settings.Name, settings.SourceBranch, settings.BranchName));

        if (response.BranchAction is BranchAction.Create)
        {
            console.MarkupLine($"Stack [yellow]{response.StackName}[/] created from source branch [blue]{response.SourceBranch}[/] with new branch [blue]{response.BranchName}[/]");
        }
        else if (response.BranchAction is BranchAction.Add)
        {
            console.MarkupLine($"Stack [yellow]{response.StackName}[/] created from source branch [blue]{response.SourceBranch}[/] with existing branch [blue]{response.BranchName}[/]");
        }
        else
        {
            console.MarkupLine($"Stack [yellow]{response.StackName}[/] created from source branch [blue]{response.SourceBranch}[/]");
        }

        return 0;
    }
}

public record NewStackCommandInputs(string? Name, string? SourceBranch, string? BranchName)
{
    public static NewStackCommandInputs Empty => new(null, null, null);
}

public record NewStackCommandResponse(string StackName, string SourceBranch, BranchAction? BranchAction, string? BranchName);

public interface INewStackCommandInputProvider
{
    string GetStackName();
    string GetSourceBranch(string[] branches);
    string GetNewBranchName();
    bool ConfirmAddOrCreateBranch();
    BranchAction SelectAddOrCreateBranch();
    string GetBranchToAdd(string[] branches);
    bool ConfirmSwitchToBranch();
}

public class NewStackCommandInputProvider(IInputProvider inputProvider) : INewStackCommandInputProvider
{
    public const string StackNamePrompt = "Stack name:";
    public const string SourceBranchPrompt = "Select a branch to start your stack from:";
    public const string BranchNamePrompt = "Branch name:";
    public const string ConfirmAddOrCreateBranchPrompt = "Do you want to add an existing branch or create a new branch and add it to the stack?";
    public const string AddOrCreateBranchPrompt = "Add or create a branch:";
    public const string AddBranchNamePrompt = "Select a branch to add to the stack:";
    public const string SwitchToBranchPrompt = "Do you want to switch to the new branch?";

    public string GetStackName() => inputProvider.Text(StackNamePrompt);
    public string GetSourceBranch(string[] branches) => inputProvider.Select(SourceBranchPrompt, branches);
    public string GetNewBranchName() => inputProvider.Text(BranchNamePrompt);
    public bool ConfirmAddOrCreateBranch() => inputProvider.Confirm(ConfirmAddOrCreateBranchPrompt);
    public BranchAction SelectAddOrCreateBranch() => inputProvider.Select(AddOrCreateBranchPrompt, [BranchAction.Create, BranchAction.Add], action => action.Humanize());
    public string GetBranchToAdd(string[] branches) => inputProvider.Select(AddBranchNamePrompt, branches);
    public bool ConfirmSwitchToBranch() => inputProvider.Confirm(SwitchToBranchPrompt);
}

public class NewStackCommandHandler(INewStackCommandInputProvider inputProvider, IGitOperations gitOperations, IStackConfig stackConfig)
{
    public async Task<NewStackCommandResponse> Handle(NewStackCommandInputs inputs)
    {
        await Task.CompletedTask;

        var name = inputs.Name ?? inputProvider.GetStackName();

        var branches = gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate();

        var sourceBranch = inputs.SourceBranch ?? inputProvider.GetSourceBranch(branches);

        var stacks = stackConfig.Load();
        var remoteUri = gitOperations.GetRemoteUri();
        var stack = new Config.Stack(name, remoteUri, sourceBranch, []);
        string? branchName = null;
        BranchAction? branchAction = null;

        if (inputs.BranchName is not null || inputProvider.ConfirmAddOrCreateBranch())
        {
            branchAction = inputs.BranchName is not null ? BranchAction.Create : inputProvider.SelectAddOrCreateBranch();

            if (branchAction == BranchAction.Create)
            {
                branchName = inputs.BranchName ?? inputProvider.GetNewBranchName();

                gitOperations.CreateNewBranch(branchName, sourceBranch);
                gitOperations.PushNewBranch(branchName);
            }
            else
            {
                branchName = inputProvider.GetBranchToAdd(branches);
            }
        }

        if (branchName is not null)
        {
            stack.Branches.Add(branchName);
        }

        stacks.Add(stack);

        stackConfig.Save(stacks);

        if (branchName is not null && (inputs.BranchName is not null || inputProvider.ConfirmSwitchToBranch()))
        {
            gitOperations.ChangeBranch(branchName);
        }

        return new NewStackCommandResponse(name, sourceBranch, branchAction, branchName);
    }
}


