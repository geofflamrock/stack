
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
}

public class DeleteStackCommand : AsyncCommand<DeleteStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DeleteStackCommandSettings settings)
    {
        await Task.CompletedTask;

        var console = AnsiConsole.Console;
        var outputProvider = new ConsoleOutputProvider(console);

        var handler = new DeleteStackCommandHandler(
            new ConsoleInputProvider(console),
            outputProvider,
            new GitClient(outputProvider, settings.GetGitClientSettings()),
            new GitHubClient(outputProvider, settings.GetGitHubClientSettings()),
            new StackConfig());

        var response = await handler.Handle(new DeleteStackCommandInputs(settings.Name));

        if (response.DeletedStackName is not null)
            console.MarkupLine($"Stack {response.DeletedStackName.Stack()} deleted");

        return 0;
    }
}

public record DeleteStackCommandInputs(string? Name)
{
    public static DeleteStackCommandInputs Empty => new((string?)null);
}

public record DeleteStackCommandResponse(string? DeletedStackName);

public class DeleteStackCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
{
    public async Task<DeleteStackCommandResponse> Handle(DeleteStackCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        var stack = inputProvider.SelectStack(outputProvider, inputs.Name, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException("Stack not found.");
        }

        if (inputProvider.Confirm(Questions.ConfirmDeleteStack))
        {
            var branchesNeedingCleanup = CleanupStackCommandHandler.GetBranchesNeedingCleanup(stack, gitClient, gitHubClient);

            if (branchesNeedingCleanup.Length > 0)
            {
                CleanupStackCommandHandler.OutputBranchesNeedingCleanup(outputProvider, branchesNeedingCleanup);

                if (inputProvider.Confirm(Questions.ConfirmDeleteBranches))
                {
                    CleanupStackCommandHandler.CleanupBranches(gitClient, outputProvider, branchesNeedingCleanup);
                }
            }

            stacks.Remove(stack);
            stackConfig.Save(stacks);

            return new DeleteStackCommandResponse(stack.Name);
        }

        return new DeleteStackCommandResponse(null);
    }
}
