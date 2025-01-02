using System.ComponentModel;
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
        var outputProvider = new ConsoleOutputProvider(console);

        var handler = new CreatePullRequestsCommandHandler(
            new ConsoleInputProvider(console),
            outputProvider,
            new GitClient(outputProvider, settings.GetGitClientSettings()),
            new GitHubOperations(outputProvider, settings.GetGitHubOperationSettings()),
            new FileOperations(),
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
    IGitClient gitClient,
    IGitHubOperations gitHubOperations,
    IFileOperations fileOperations,
    IStackConfig stackConfig)
{
    public async Task<CreatePullRequestsCommandResponse> Handle(CreatePullRequestsCommandInputs inputs)
    {
        await Task.CompletedTask;

        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            outputProvider.Information("No stacks found for current repository.");
            return new CreatePullRequestsCommandResponse();
        }

        var currentBranch = gitClient.GetCurrentBranch();
        var stack = inputProvider.SelectStack(outputProvider, inputs.StackName, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.StackName}' not found.");
        }

        var status = StackHelpers.GetStackStatus(
            stack,
            currentBranch,
            outputProvider,
            gitClient,
            gitHubOperations);

        var sourceBranch = stack.SourceBranch;
        var pullRequestCreateActions = new List<GitHubPullRequestCreateAction>();

        foreach (var branch in stack.Branches)
        {
            var branchDetail = status.Branches[branch];

            if (branchDetail.IsActive)
            {
                if (!branchDetail.HasPullRequest)
                {
                    pullRequestCreateActions.Add(new GitHubPullRequestCreateAction(branch, sourceBranch));
                }

                sourceBranch = branch;
            }
        }

        StackHelpers.OutputStackStatus(stack, status, outputProvider);

        outputProvider.NewLine();

        if (pullRequestCreateActions.Count > 0)
        {
            if (inputProvider.Confirm(Questions.ConfirmStartCreatePullRequests(pullRequestCreateActions.Count)))
            {
                GetPullRequestInformation(inputProvider, outputProvider, gitClient, fileOperations, pullRequestCreateActions);

                outputProvider.NewLine();

                OutputUpdatedStackStatus(outputProvider, stack, status, pullRequestCreateActions);

                outputProvider.NewLine();

                if (inputProvider.Confirm(Questions.ConfirmCreatePullRequests))
                {
                    var newPullRequests = CreatePullRequests(outputProvider, gitHubOperations, status, pullRequestCreateActions);

                    var pullRequestsInStack = status.Branches.Values
                        .Where(branch => branch.HasPullRequest)
                        .Select(branch => branch.PullRequest!)
                        .ToList();

                    if (pullRequestsInStack.Count > 1)
                    {
                        UpdatePullRequestStackDescriptions(inputProvider, outputProvider, gitHubOperations, stackConfig, stacks, stack, pullRequestsInStack);
                    }

                    if (inputProvider.Confirm(Questions.OpenPullRequests))
                    {
                        foreach (var pullRequest in newPullRequests)
                        {
                            gitHubOperations.OpenPullRequest(pullRequest);
                        }
                    }
                }
            }
        }
        else
        {
            outputProvider.Information("No new pull requests to create.");
        }

        return new CreatePullRequestsCommandResponse();
    }

    private static void UpdatePullRequestStackDescriptions(IInputProvider inputProvider, IOutputProvider outputProvider, IGitHubOperations gitHubOperations, IStackConfig stackConfig, List<Config.Stack> stacks, Config.Stack stack, List<GitHubPullRequest> pullRequestsInStack)
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

    private static List<GitHubPullRequest> CreatePullRequests(
        IOutputProvider outputProvider,
        IGitHubOperations gitHubOperations,
        StackStatus status,
        List<GitHubPullRequestCreateAction> pullRequestCreateActions)
    {
        var pullRequests = new List<GitHubPullRequest>();
        foreach (var action in pullRequestCreateActions)
        {
            var branchDetail = status.Branches[action.HeadBranch];
            outputProvider.Information($"Creating pull request for branch {action.HeadBranch.Branch()} to {action.BaseBranch.Branch()}");
            var pullRequest = gitHubOperations.CreatePullRequest(action.HeadBranch, action.BaseBranch, action.Title!, action.BodyFilePath!, action.Draft);

            if (pullRequest is not null)
            {
                outputProvider.Information($"Pull request {pullRequest.GetPullRequestDisplay()} created for branch {action.HeadBranch.Branch()} to {action.BaseBranch.Branch()}");
                pullRequests.Add(pullRequest);
                branchDetail.PullRequest = pullRequest;
            }
        }

        return pullRequests;
    }

    private static void OutputUpdatedStackStatus(IOutputProvider outputProvider, Config.Stack stack, StackStatus status, List<GitHubPullRequestCreateAction> pullRequestCreateActions)
    {
        var branchDisplayItems = new List<string>();
        var parentBranch = stack.SourceBranch;

        foreach (var branch in stack.Branches)
        {
            var branchDetail = status.Branches[branch];
            if (branchDetail.PullRequest is not null && branchDetail.PullRequest.State != GitHubPullRequestStates.Closed)
            {
                branchDisplayItems.Add(StackHelpers.GetBranchAndPullRequestStatusOutput(branch, parentBranch, branchDetail));
            }
            else
            {
                var action = pullRequestCreateActions.FirstOrDefault(a => a.HeadBranch == branch);
                branchDisplayItems.Add($"{StackHelpers.GetBranchStatusOutput(branch, parentBranch, branchDetail)} *NEW* {action?.Title}{(action?.Draft == true ? " (draft)".Muted() : string.Empty)}");
            }
            parentBranch = branch;
        }

        outputProvider.Tree(
            $"{stack.Name.Stack()}: {stack.SourceBranch.Muted()}",
            [.. branchDisplayItems]);
    }

    private static void GetPullRequestInformation(
        IInputProvider inputProvider,
        IOutputProvider outputProvider,
        IGitClient gitClient,
        IFileOperations fileOperations,
        List<GitHubPullRequestCreateAction> pullRequestCreateActions)
    {
        var pullRequestTemplateFileNames = new List<string>(["PULL_REQUEST_TEMPLATE.md", "pull_request_template.md"]);

        var pullRequestTemplatePath = pullRequestTemplateFileNames
            .Select(fileName => Path.Join(gitClient.GetRootOfRepository(), ".github", fileName))
            .FirstOrDefault(fileOperations.Exists);

        if (pullRequestTemplatePath is not null)
        {
            outputProvider.Information($"Found pull request template in repository, this will be used as the default body for each pull request.");
        }

        foreach (var action in pullRequestCreateActions)
        {
            outputProvider.NewLine();
            var pullRequestHeader = $"New pull request from {action.HeadBranch.Branch()} -> {action.BaseBranch.Branch()}";
            outputProvider.Rule(pullRequestHeader);

            action.Title = inputProvider.Text(Questions.PullRequestTitle);
            action.BodyFilePath = Path.Join(fileOperations.GetTempPath(), $"stack-pr-{Guid.NewGuid():N}.md");
            if (pullRequestTemplatePath is not null)
                fileOperations.Copy(pullRequestTemplatePath, action.BodyFilePath, true);

            if (inputProvider.Confirm(Questions.EditPullRequestBody))
            {
                gitClient.OpenFileInEditorAndWaitForClose(action.BodyFilePath);
            }

            action.Draft = inputProvider.Confirm(Questions.CreatePullRequestAsDraft, false);
        }
    }

    record GitHubPullRequestCreateAction(string HeadBranch, string BaseBranch)
    {
        public string? Title { get; set; }
        public string? BodyFilePath { get; set; }
        public bool Draft { get; set; }
    }
}
