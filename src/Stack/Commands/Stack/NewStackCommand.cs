

using System.ComponentModel;
using Humanizer;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
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

    [Description("Don't push the new branch to the remote repository.")]
    [CommandOption("--no-push")]
    [DefaultValue(false)]
    public bool NoPush { get; init; }
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
        var outputProvider = new ConsoleOutputProvider(console);

        var handler = new NewStackCommandHandler(
            new ConsoleInputProvider(console),
            outputProvider,
            new GitOperations(outputProvider, settings.GetGitOperationSettings()),
            new StackConfig());

        await handler.Handle(
            new NewStackCommandInputs(settings.Name, settings.SourceBranch, settings.BranchName, settings.NoPush));

        return 0;
    }
}

public record NewStackCommandInputs(string? Name, string? SourceBranch, string? BranchName, bool NoPush)
{
    public static NewStackCommandInputs Empty => new(null, null, null, false);
}

public record NewStackCommandResponse(string StackName, string SourceBranch, BranchAction? BranchAction, string? BranchName);

public class NewStackCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitOperations gitOperations,
    IStackConfig stackConfig)
{
    public async Task<NewStackCommandResponse> Handle(NewStackCommandInputs inputs)
    {
        await Task.CompletedTask;

        var name = inputProvider.Text(outputProvider, Questions.StackName, inputs.Name);

        var branches = gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate();

        var sourceBranch = inputProvider.Select(outputProvider, Questions.SelectSourceBranch, inputs.SourceBranch, branches);

        var stacks = stackConfig.Load();
        var remoteUri = gitOperations.GetRemoteUri();
        var stack = new Config.Stack(name, remoteUri, sourceBranch, []);
        string? branchName = null;
        BranchAction? branchAction = null;

        if (inputs.BranchName is not null || inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch))
        {
            branchAction = inputs.BranchName is not null ? BranchAction.Create : inputProvider.Select(
                Questions.AddOrCreateBranch,
                [BranchAction.Create, BranchAction.Add],
                action => action.Humanize());

            outputProvider.Information($"{Questions.AddOrCreateBranch} {branchAction.Humanize()}");

            if (branchAction == BranchAction.Create)
            {
                branchName = inputProvider.Text(outputProvider, Questions.BranchName, inputs.BranchName, stack.GetDefaultBranchName());

                gitOperations.CreateNewBranch(branchName, sourceBranch);

                if (!inputs.NoPush)
                {
                    gitOperations.PushNewBranch(branchName);
                }
            }
            else
            {
                branchName = inputProvider.SelectBranch(outputProvider, null, branches);
            }
        }

        if (branchName is not null)
        {
            stack.Branches.Add(branchName);
        }

        stacks.Add(stack);

        stackConfig.Save(stacks);

        if (branchName is not null && (inputs.BranchName is not null || inputProvider.Confirm(Questions.ConfirmSwitchToBranch)))
        {
            gitOperations.ChangeBranch(branchName);
        }

        if (branchAction is BranchAction.Create)
        {
            outputProvider.Information($"Stack {name.Stack()} created from source branch {sourceBranch.Branch()} with new branch {branchName!.Branch()}");
        }
        else if (branchAction is BranchAction.Add)
        {
            outputProvider.Information($"Stack {name.Stack()} created from source branch {sourceBranch.Branch()} with existing branch {branchName!.Branch()}");
        }
        else
        {
            outputProvider.Information($"Stack {name.Stack()} created from source branch {sourceBranch.Branch()}");
        }

        return new NewStackCommandResponse(name, sourceBranch, branchAction, branchName);
    }
}


