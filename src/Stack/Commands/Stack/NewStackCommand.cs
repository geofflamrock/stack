

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
    [CommandOption("--source-branch")]
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

public class NewStackCommand : Command<NewStackCommandSettings>
{
    protected override async Task Execute(NewStackCommandSettings settings)
    {
        var handler = new NewStackCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, settings.GetGitClientSettings()),
            new StackConfig());

        await handler.Handle(
            new NewStackCommandInputs(settings.Name, settings.SourceBranch, settings.BranchName));
    }
}

public record NewStackCommandInputs(string? Name, string? SourceBranch, string? BranchName)
{
    public static NewStackCommandInputs Empty => new(null, null, null);
}

public class NewStackCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<NewStackCommandInputs>
{
    public override async Task Handle(NewStackCommandInputs inputs)
    {
        await Task.CompletedTask;

        var name = inputProvider.Text(logger, Questions.StackName, inputs.Name);

        var branches = gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate();

        var sourceBranch = inputProvider.Select(logger, Questions.SelectSourceBranch, inputs.SourceBranch, branches);

        var stacks = stackConfig.Load();
        var remoteUri = gitClient.GetRemoteUri();
        var stack = new Config.Stack(name, remoteUri, sourceBranch, []);
        string? branchName = null;
        BranchAction? branchAction = null;

        if (inputs.BranchName is not null || inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch))
        {
            branchAction = inputs.BranchName is not null ? BranchAction.Create : inputProvider.Select(
                Questions.AddOrCreateBranch,
                [BranchAction.Create, BranchAction.Add],
                action => action.Humanize());

            logger.Information($"{Questions.AddOrCreateBranch} {branchAction.Humanize()}");

            if (branchAction == BranchAction.Create)
            {
                branchName = inputProvider.Text(logger, Questions.BranchName, inputs.BranchName, stack.GetDefaultBranchName());

                gitClient.CreateNewBranch(branchName, sourceBranch);

                if (inputProvider.Confirm(Questions.ConfirmPushBranch))
                {
                    try
                    {
                        gitClient.PushNewBranch(branchName);
                    }
                    catch (Exception)
                    {
                        logger.Warning($"An error has occurred pushing branch {branchName.Branch()} to remote repository. Use {$"stack push --name \"{name}\"".Example()} to push the branch to the remote repository.");
                    }
                }
                else
                {
                    logger.Information($"Use {$"stack push --name \"{name}\"".Example()} to push the branch to the remote repository.");
                }
            }
            else
            {
                branchName = inputProvider.SelectBranch(logger, null, branches);
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
            gitClient.ChangeBranch(branchName);
        }

        if (branchAction is BranchAction.Create)
        {
            logger.Information($"Stack {name.Stack()} created from source branch {sourceBranch.Branch()} with new branch {branchName!.Branch()}");
        }
        else if (branchAction is BranchAction.Add)
        {
            logger.Information($"Stack {name.Stack()} created from source branch {sourceBranch.Branch()} with existing branch {branchName!.Branch()}");
        }
        else
        {
            logger.Information($"Stack {name.Stack()} created from source branch {sourceBranch.Branch()}");
        }
    }
}


