using System.ComponentModel;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class RemoveBranchCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to create the branch in.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("The name of the branch to add.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("Confirm the removal of the branch without prompting.")]
    [CommandOption("--yes")]
    public bool Confirm { get; init; }
}

public class RemoveBranchCommand : Command<RemoveBranchCommandSettings>
{
    protected override async Task Execute(RemoveBranchCommandSettings settings)
    {
        var handler = new RemoveBranchCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, settings.GetGitClientSettings()),
            new StackConfig());

        await handler.Handle(new RemoveBranchCommandInputs(settings.Stack, settings.Name, settings.Confirm));
    }
}

public record RemoveBranchCommandInputs(string? StackName, string? BranchName, bool Confirm)
{
    public static RemoveBranchCommandInputs Empty => new(null, null, false);
}

public class RemoveBranchCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<RemoveBranchCommandInputs>
{
    public override async Task Handle(RemoveBranchCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();
        var stack = inputProvider.SelectStack(logger, inputs.StackName, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.StackName}' not found.");
        }

        var branchName = inputProvider.SelectBranch(logger, inputs.BranchName, [.. stack.Branches]);

        if (!stack.Branches.Contains(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' not found in stack '{stack.Name}'.");
        }

        if (inputs.Confirm || inputProvider.Confirm(Questions.ConfirmRemoveBranch))
        {
            stack.Branches.Remove(branchName);
            stackConfig.Save(stacks);

            logger.Information($"Branch {branchName.Branch()} removed from stack {stack.Name.Stack()}");
        }
    }
}

