using System.Collections;
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
        var stack = inputProvider.SelectStack(outputProvider, inputs.StackName, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.StackName}' not found.");
        }

        var statuses = StackStatusHelpers.CheckStackStatus(
            [stack],
            currentBranch,
            outputProvider,
            gitOperations,
            gitHubOperations);

        var status = statuses[stack];

        var sourceBranch = stack.SourceBranch;
        var pullRequestCreateActions = new List<GitHubPullRequestCreateAction>();
        var pullRequestsInStack = new List<GitHubPullRequest>();

        foreach (var branch in stack.Branches)
        {
            var branchStatus = status.Branches[branch];
            var existingPullRequest = branchStatus.PullRequest;

            if (branchStatus.PullRequest is not null && branchStatus.PullRequest.State != GitHubPullRequestStates.Closed)
            {
                pullRequestsInStack.Add(branchStatus.PullRequest);
            }

            // If the source branch still exists and there is either no PR or the PR isn't merged
            // then we consider this branch to be the source branch for the next PR in the stack
            if (branchStatus.Status.ExistsInRemote && (branchStatus.PullRequest is null || branchStatus.PullRequest.State != GitHubPullRequestStates.Merged))
            {
                if (branchStatus.PullRequest is null || branchStatus.PullRequest.State == GitHubPullRequestStates.Closed)
                {
                    pullRequestCreateActions.Add(new GitHubPullRequestCreateAction(branch, sourceBranch));
                }

                sourceBranch = branch;
            }
        }

        StackStatusHelpers.OutputStackStatus(statuses, gitOperations, outputProvider);

        outputProvider.NewLine();

        if (pullRequestCreateActions.Count > 0)
        {
            if (inputProvider.Confirm(Questions.ConfirmCreatePullRequests))
            {
                foreach (var action in pullRequestCreateActions)
                {
                    action.Title = inputProvider.Text(Questions.PullRequestTitle(action.HeadBranch, action.BaseBranch));
                }

                void OutputUpdatedStackStatus()
                {
                    var branchDisplayItems = new List<string>();
                    var parentBranch = stack.SourceBranch;

                    foreach (var branch in stack.Branches)
                    {
                        var branchDetail = status.Branches[branch];
                        if (branchDetail.PullRequest is not null)
                        {
                            branchDisplayItems.Add(StackStatusHelpers.GetBranchAndPullRequestStatusOutput(branch, parentBranch, branchDetail, gitOperations));
                        }
                        else
                        {
                            var action = pullRequestCreateActions.FirstOrDefault(a => a.HeadBranch == branch);
                            branchDisplayItems.Add($"{StackStatusHelpers.GetBranchStatusOutput(branch, parentBranch, branchDetail, gitOperations)} *NEW* {action?.Title}");
                        }
                        parentBranch = branch;
                    }

                    outputProvider.Tree(
                        $"{stack.Name.Stack()}: {stack.SourceBranch.Muted()}",
                        [.. branchDisplayItems]);
                }

                OutputUpdatedStackStatus();

                outputProvider.NewLine();

                if (inputProvider.Confirm(Questions.ConfirmCreatePullRequests))
                {
                    foreach (var action in pullRequestCreateActions)
                    {
                        outputProvider.Information($"Creating pull request for branch {action.HeadBranch.Branch()} to {action.BaseBranch.Branch()}");
                        var pullRequest = gitHubOperations.CreatePullRequest(action.HeadBranch, action.BaseBranch, action.Title!, "");

                        if (pullRequest is not null)
                        {
                            outputProvider.Information($"Pull request {pullRequest.GetPullRequestDisplay()} created for branch {action.HeadBranch.Branch()} to {action.BaseBranch.Branch()}");
                            pullRequestsInStack.Add(pullRequest);
                        }
                    }

                    if (pullRequestsInStack.Count > 1)
                    {
                        // Did we create any new PRs? If so, check if we should change the description
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
                        var prBodyMarkdown = $"{stackMarkerStart}{Environment.NewLine}{stack.PullRequestDescription}{Environment.NewLine}{Environment.NewLine}{prListMarkdown}{Environment.NewLine}{stackMarkerEnd}";

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

                            outputProvider.Information($"Updating pull request {pullRequest.GetPullRequestDisplay()} with stack details");

                            gitHubOperations.EditPullRequest(pullRequest.Number, prBody);
                        }
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
            }
        }
        else
        {
            outputProvider.Information("No pull requests to create.");
        }

        return new CreatePullRequestsCommandResponse();
    }

    record GitHubPullRequestCreateAction(string HeadBranch, string BaseBranch)
    {
        public string? Title { get; set; }
    }
}

