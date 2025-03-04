
using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Commands.Helpers;

namespace Stack.Commands;

public class DeleteStackCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to delete.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }
}

public class DeleteStackCommand : Command<DeleteStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DeleteStackCommandSettings settings)
    {
        var handler = new DeleteStackCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, settings.GetGitClientSettings()),
            new GitHubClient(StdErrLogger, settings.GetGitHubClientSettings()),
            new StackConfig());

        var response = await handler.Handle(new DeleteStackCommandInputs(settings.Stack));

        if (response.DeletedStackName is not null)
            StdErrLogger.Information($"Stack {response.DeletedStackName.Stack()} deleted");

        return 0;
    }
}

public record DeleteStackCommandInputs(string? Stack)
{
    public static DeleteStackCommandInputs Empty => new((string?)null);
}

public record DeleteStackCommandResponse(string? DeletedStackName);

public class DeleteStackCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<DeleteStackCommandInputs, DeleteStackCommandResponse>
{
    public override async Task<DeleteStackCommandResponse> Handle(DeleteStackCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        var stack = inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException("Stack not found.");
        }

        if (inputProvider.Confirm(Questions.ConfirmDeleteStack))
        {
            var branchesNeedingCleanup = StackHelpers.GetBranchesNeedingCleanup(stack, logger, gitClient, gitHubClient);

            if (branchesNeedingCleanup.Length > 0)
            {
                StackHelpers.OutputBranchesNeedingCleanup(logger, branchesNeedingCleanup);

                if (inputProvider.Confirm(Questions.ConfirmDeleteBranches))
                {
                    StackHelpers.CleanupBranches(gitClient, logger, branchesNeedingCleanup);
                }
            }

            stacks.Remove(stack);
            stackConfig.Save(stacks);

            return new DeleteStackCommandResponse(stack.Name);
        }

        return new DeleteStackCommandResponse(null);
    }
}
