using System.ComponentModel;
using Humanizer;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class UpdateStackCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to update.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("Force the update of the stack.")]
    [CommandOption("-f|--force")]
    public bool Force { get; init; }
}

public class UpdateStackCommand(
    IAnsiConsole console,
    IGitOperations gitOperations,
    IStackConfig stackConfig) : AsyncCommand<UpdateStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, UpdateStackCommandSettings settings)
    {
        var handler = new UpdateStackCommandHandler(
            new UpdateStackCommandInputProvider(new ConsoleInputProvider(console)),
            new ConsoleOutputProvider(console),
            gitOperations,
            stackConfig);

        await handler.Handle(
            new UpdateStackCommandInputs(settings.Name, settings.Force),
            settings.GetGitOperationSettings());

        return 0;
    }
}

public interface IUpdateStackCommandInputProvider
{
    string SelectStack(List<Config.Stack> stacks, string currentBranch);
    bool ConfirmUpdate();
}

public class UpdateStackCommandInputProvider(IInputProvider inputProvider) : IUpdateStackCommandInputProvider
{
    const string SelectStackPrompt = "Select stack:";
    const string UpdateStackPrompt = "Are you sure you want to update this stack?";

    public string SelectStack(List<Config.Stack> stacks, string currentBranch)
    {
        return inputProvider.Select(SelectStackPrompt, stacks.OrderByCurrentStackThenByName(currentBranch).Select(s => s.Name).ToArray());
    }

    public bool ConfirmUpdate()
    {
        return inputProvider.Confirm(UpdateStackPrompt);
    }
}

public record UpdateStackCommandInputs(string? Name, bool Force)
{
    public static UpdateStackCommandInputs Empty => new(null, false);
}

public record UpdateStackCommandResponse();

public class UpdateStackCommandHandler(
    IUpdateStackCommandInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitOperations gitOperations,
    IStackConfig stackConfig)
{
    public async Task<UpdateStackCommandResponse> Handle(
        UpdateStackCommandInputs inputs,
        GitOperationSettings gitOperationSettings)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri(gitOperationSettings);

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            return new UpdateStackCommandResponse();
        }

        var currentBranch = gitOperations.GetCurrentBranch(gitOperationSettings);
        var stackSelection = inputs.Name ?? inputProvider.SelectStack(stacksForRemote, currentBranch);
        var stack = stacksForRemote.FirstOrDefault(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        if (stack is null)
            throw new InvalidOperationException($"Stack '{stackSelection}' not found.");

        if (inputs.Force || inputProvider.ConfirmUpdate())
        {
            void MergeFromSourceBranch(string branch, string sourceBranchName)
            {
                outputProvider.Information($"Merging {sourceBranchName.Stack()} into {branch.Branch()}");

                gitOperations.UpdateBranch(sourceBranchName, gitOperationSettings);
                gitOperations.UpdateBranch(branch, gitOperationSettings);
                gitOperations.MergeFromLocalSourceBranch(sourceBranchName, gitOperationSettings);
                gitOperations.PushBranch(branch, gitOperationSettings);
            }

            var sourceBranch = stack.SourceBranch;

            foreach (var branch in stack.Branches)
            {
                if (gitOperations.DoesRemoteBranchExist(branch, gitOperationSettings))
                {
                    MergeFromSourceBranch(branch, sourceBranch);
                    sourceBranch = branch;
                }
                else
                {
                    outputProvider.Debug($"Branch '{branch}' no longer exists on the remote repository. Skipping...");
                }
            }

            if (stack.SourceBranch.Equals(currentBranch, StringComparison.InvariantCultureIgnoreCase) ||
                stack.Branches.Contains(currentBranch, StringComparer.OrdinalIgnoreCase))
            {
                gitOperations.ChangeBranch(currentBranch, gitOperationSettings);
            }
        }

        return new UpdateStackCommandResponse();
    }
}