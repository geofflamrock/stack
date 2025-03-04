using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class NewBranchCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to create the branch in.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("The name of the branch to create.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

public class NewBranchCommand : Command<NewBranchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, NewBranchCommandSettings settings)
    {
        var handler = new NewBranchCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, settings.GetGitClientSettings()),
            new StackConfig());

        await handler.Handle(new NewBranchCommandInputs(settings.Stack, settings.Name));

        return 0;
    }
}

public record NewBranchCommandInputs(string? StackName, string? BranchName)
{
    public static NewBranchCommandInputs Empty => new(null, null);
}

public class NewBranchCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<NewBranchCommandInputs>
{
    public override async Task Handle(NewBranchCommandInputs inputs)
    {
        await Task.CompletedTask;

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();
        var branches = gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate();

        var stacks = stackConfig.Load();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            logger.Information("No stacks found for current repository.");
            return;
        }

        var stack = inputProvider.SelectStack(logger, inputs.StackName, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.StackName}' not found.");
        }

        var sourceBranch = stack.Branches.LastOrDefault() ?? stack.SourceBranch;

        var branchName = inputProvider.Text(logger, Questions.BranchName, inputs.BranchName, stack.GetDefaultBranchName());

        if (stack.Branches.Contains(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' already exists in stack '{stack.Name}'.");
        }

        if (gitClient.DoesLocalBranchExist(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' already exists locally.");
        }

        logger.Information($"Creating branch {branchName.Branch()} from {sourceBranch.Branch()} in stack {stack.Name.Stack()}");

        gitClient.CreateNewBranch(branchName, sourceBranch);

        stack.Branches.Add(branchName);

        stackConfig.Save(stacks);

        logger.Information($"Branch {branchName.Branch()} created.");

        if (inputProvider.Confirm(Questions.ConfirmPushBranch))
        {
            try
            {
                gitClient.PushNewBranch(branchName);
            }
            catch (Exception)
            {
                logger.Warning($"An error has occurred pushing branch {branchName.Branch()} to remote repository. Use {$"stack push --name \"{stack.Name}\"".Example()} to push the branch to the remote repository.");
            }
        }
        else
        {
            logger.Information($"Use {$"stack push --name \"{stack.Name}\"".Example()} to push the branch to the remote repository.");
        }

        if (inputProvider.Confirm(Questions.ConfirmSwitchToBranch))
        {
            gitClient.ChangeBranch(branchName);
        }
    }
}