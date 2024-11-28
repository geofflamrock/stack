using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class CreatePullRequestsCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to create pull requests for.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

public class CreatePullRequestsCommand : AsyncCommand<CreatePullRequestsCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CreatePullRequestsCommandSettings settings)
    {
        var console = AnsiConsole.Console;

        var handler = new CreatePullRequestsCommandHandler(
            new ConsoleInputProvider(console),
            new ConsoleOutputProvider(console),
            new GitOperations(console, settings.GetGitOperationSettings()),
            new GitHubOperations(console, settings.GetGitHubOperationSettings()),
            new StackConfig());

        await handler.Handle(new CreatePullRequestsCommandInputs(settings.Name));

        return 0;
    }
}

public record CreatePullRequestsCommandInputs(string? StackName)
{
    public static CreatePullRequestsCommandInputs Empty => new((string?)null);
}

public record CreatePullRequestsCommandResponse();

public class CreatePullRequestsCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitOperations gitOperations,
    IGitHubOperations gitHubOperations,
    IStackConfig stackConfig)
{
    public async Task<CreatePullRequestsCommandResponse> Handle(CreatePullRequestsCommandInputs inputs)
    {
        await Task.CompletedTask;

        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            outputProvider.Information("No stacks found for current repository.");
            return new CreatePullRequestsCommandResponse();
        }

        var currentBranch = gitOperations.GetCurrentBranch();
        var stackNames = stacksForRemote.OrderByCurrentStackThenByName(currentBranch).Select(s => s.Name).ToArray();
        var stackSelection = inputs.StackName ?? inputProvider.Select(Questions.SelectStack, stackNames);
        var stack = stacksForRemote.FirstOrDefault(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.StackName}' not found.");
        }

        var stackStatusCommandHandler = new StackStatusCommandHandler(
            inputProvider,
            outputProvider,
            gitOperations,
            gitHubOperations,
            stackConfig);

        await stackStatusCommandHandler.Handle(new StackStatusCommandInputs(stack.Name, false));

        outputProvider.NewLine();

        if (inputProvider.Confirm(Questions.ConfirmCreatePullRequests))
        {
            var sourceBranch = stack.SourceBranch;
            var pullRequestsInStack = new List<GitHubPullRequest>();

            foreach (var branch in stack.Branches)
            {
                var existingPullRequest = gitHubOperations.GetPullRequest(branch);

                if (existingPullRequest is not null && existingPullRequest.State != GitHubPullRequestStates.Closed)
                {
                    outputProvider.Information($"Pull request {existingPullRequest.GetPullRequestDisplay()} already exists for branch {branch.Branch()} to {sourceBranch.Branch()}. Skipping...");
                    pullRequestsInStack.Add(existingPullRequest);
                }

                if (gitOperations.DoesRemoteBranchExist(branch))
                {
                    if (existingPullRequest is null || existingPullRequest.State == GitHubPullRequestStates.Closed)
                    {
                        var prTitle = inputProvider.Text(Questions.PullRequestTitle(branch, sourceBranch));
                        outputProvider.Information($"Creating pull request for branch {branch.Branch()} to {sourceBranch.Branch}");
                        var pullRequest = gitHubOperations.CreatePullRequest(branch, sourceBranch, prTitle, "");

                        if (pullRequest is not null)
                        {
                            outputProvider.Information($"Pull request {pullRequest.GetPullRequestDisplay()} created for branch {branch.Branch()} to {sourceBranch.Branch()}");
                            pullRequestsInStack.Add(pullRequest);
                        }
                    }

                    sourceBranch = branch;
                }
            }

            if (pullRequestsInStack.Count > 1)
            {
                var defaultStackDescription = stack.PullRequestDescription ?? $"This PR is part of a stack **{stack.Name}**:";
                var stackDescription = inputProvider.Text(Questions.PullRequestStackDescription, defaultStackDescription);

                if (stackDescription != stack.PullRequestDescription)
                {
                    stack.SetPullRequestDescription(stackDescription);
                    stackConfig.Save(stacks);
                }

                // Edit each PR and add to the top of the description
                // the details of each PR in the stack
                var stackMarkerStart = "<!-- stack-pr-list -->";
                var stackMarkerEnd = "<!-- /stack-pr-list -->";
                var prList = pullRequestsInStack
                    .Select(pr => $"- {pr.Url}")
                    .ToList();
                var prListMarkdown = string.Join(Environment.NewLine, prList);
                var prBodyMarkdown = $"{stackMarkerStart}{Environment.NewLine}{stackDescription}{Environment.NewLine}{Environment.NewLine}{prListMarkdown}{Environment.NewLine}{stackMarkerEnd}";

                foreach (var pullRequest in pullRequestsInStack)
                {
                    // Find the existing part of the PR body that has the PR list
                    // and replace it with the updated PR list
                    var prBody = pullRequest.Body;

                    var prListStart = prBody.IndexOf(stackMarkerStart, StringComparison.OrdinalIgnoreCase);
                    var prListEnd = prBody.IndexOf(stackMarkerEnd, StringComparison.OrdinalIgnoreCase);

                    if (prListStart >= 0 && prListEnd >= 0)
                    {
                        prBody = prBody.Remove(prListStart, prListEnd - prListStart + stackMarkerEnd.Length);
                    }

                    if (prListStart == -1)
                    {
                        prListStart = 0;
                    }

                    if (prBody.Length > 0 && prListStart == 0)
                    {
                        // Add some newlines so that the PR list is separated from the rest of the PR body
                        prBody = prBody.Insert(prListStart, prBodyMarkdown + "\n\n");
                    }
                    else
                    {
                        prBody = prBody.Insert(prListStart, prBodyMarkdown);
                    }

                    gitHubOperations.EditPullRequest(pullRequest.Number, prBody);
                }
            }
            else
            {
                outputProvider.Information("Only one pull request in stack, not adding PR list to description.");
            }

            if (inputProvider.Confirm(Questions.OpenPullRequests))
            {
                foreach (var pullRequest in pullRequestsInStack)
                {
                    Process.Start(new ProcessStartInfo(pullRequest.Url.ToString())
                    {
                        UseShellExecute = true
                    });
                }
            }
        }

        return new CreatePullRequestsCommandResponse();
    }
}