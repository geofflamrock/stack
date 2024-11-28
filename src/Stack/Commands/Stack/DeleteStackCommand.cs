
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
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("Force cleanup and delete the stack without prompting.")]
    [CommandOption("-f|--force")]
    public bool Force { get; init; }
}

public class DeleteStackCommand : AsyncCommand<DeleteStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DeleteStackCommandSettings settings)
    {
        await Task.CompletedTask;

        var console = AnsiConsole.Console;
        var handler = new DeleteStackCommandHandler(
            new ConsoleInputProvider(console),
            new ConsoleOutputProvider(console),
            new GitOperations(console, settings.GetGitOperationSettings()),
            new GitHubOperations(console, settings.GetGitHubOperationSettings()),
            new StackConfig());

        var response = await handler.Handle(new DeleteStackCommandInputs(settings.Name, settings.Force));

        if (response.DeletedStackName is not null)
            console.MarkupLine($"Stack {response.DeletedStackName.Stack()} deleted");

        return 0;
    }
}

public record DeleteStackCommandInputs(string? Name, bool Force)
{
    public static DeleteStackCommandInputs Empty => new(null, false);
}

public record DeleteStackCommandResponse(string? DeletedStackName);

public class DeleteStackCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitOperations gitOperations,
    IGitHubOperations gitHubOperations,
    IStackConfig stackConfig)
{
    public async Task<DeleteStackCommandResponse> Handle(DeleteStackCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri();
        var currentBranch = gitOperations.GetCurrentBranch();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        var stack = InputHelpers.SelectStack(inputProvider, outputProvider, inputs.Name, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException("Stack not found.");
        }

        if (inputs.Force || inputProvider.Confirm(Questions.ConfirmDeleteStack(stack.Name)))
        {
            var branchesNeedingCleanup = CleanupStackCommandHandler.GetBranchesNeedingCleanup(stack, gitOperations, gitHubOperations);

            if (branchesNeedingCleanup.Length > 0)
            {
                if (!inputs.Force)
                    CleanupStackCommandHandler.OutputBranchesNeedingCleanup(outputProvider, branchesNeedingCleanup);

                if (inputs.Force || inputProvider.Confirm(Questions.ConfirmDeleteBranches))
                {
                    CleanupStackCommandHandler.CleanupBranches(gitOperations, outputProvider, branchesNeedingCleanup);
                }
            }

            stacks.Remove(stack);
            stackConfig.Save(stacks);

            return new DeleteStackCommandResponse(stack.Name);
        }

        return new DeleteStackCommandResponse(null);
    }
}
