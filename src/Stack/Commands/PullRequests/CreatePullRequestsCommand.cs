using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class CreatePullRequestsCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to create pull requests for.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }
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
            new GitHubClient(outputProvider, settings.GetGitHubClientSettings()),
            new FileOperations(),
            new StackConfig());

        await handler.Handle(new CreatePullRequestsCommandInputs(settings.Stack));

        return 0;
    }
}

public record CreatePullRequestsCommandInputs(string? Stack)
{
    public static CreatePullRequestsCommandInputs Empty => new((string?)null);
}

public record CreatePullRequestsCommandResponse();

public class CreatePullRequestsCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
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
        var stack = inputProvider.SelectStack(outputProvider, inputs.Stack, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");
        }

        var status = StackHelpers.GetStackStatus(
            stack,
            currentBranch,
            outputProvider,
            gitClient,
            gitHubClient);

        var sourceBranch = stack.SourceBranch;
        var pullRequestCreateActions = new List<PullRequestCreateAction>();

        foreach (var branch in stack.Branches)
        {
            var branchDetail = status.Branches[branch];

            if (branchDetail.IsActive)
            {
                if (!branchDetail.HasPullRequest)
                {
                    pullRequestCreateActions.Add(new PullRequestCreateAction(branch, sourceBranch));
                }

                sourceBranch = branch;
            }
        }

        StackHelpers.OutputStackStatus(stack, status, outputProvider);

        outputProvider.NewLine();

        if (pullRequestCreateActions.Count > 0)
        {
            var selectedPullRequestActions = inputProvider.MultiSelect(
                Questions.SelectPullRequestsToCreate,
                pullRequestCreateActions.ToArray(),
                action => $"{action.Branch} -> {action.BaseBranch}")
                .ToList();

            outputProvider.Information("Select branches to create pull requests for:");

            foreach (var action in selectedPullRequestActions)
            {
                outputProvider.Information($"  {action.Branch} -> {action.BaseBranch}");
            }

            outputProvider.NewLine();

            var pullRequestInformation = GetPullRequestInformation(inputProvider, outputProvider, gitClient, fileOperations, selectedPullRequestActions);

            outputProvider.NewLine();

            OutputUpdatedStackStatus(outputProvider, stack, status, pullRequestInformation);

            outputProvider.NewLine();

            if (inputProvider.Confirm(Questions.ConfirmCreatePullRequests))
            {
                var newPullRequests = CreatePullRequests(outputProvider, gitHubClient, status, pullRequestInformation);

                var pullRequestsInStack = status.Branches.Values
                    .Where(branch => branch.HasPullRequest)
                    .Select(branch => branch.PullRequest!)
                    .ToList();

                if (pullRequestsInStack.Count > 1)
                {
                    UpdatePullRequestStackDescriptions(inputProvider, outputProvider, gitHubClient, stackConfig, stacks, stack, pullRequestsInStack);
                }

                if (inputProvider.Confirm(Questions.OpenPullRequests))
                {
                    foreach (var pullRequest in newPullRequests)
                    {
                        gitHubClient.OpenPullRequest(pullRequest);
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

    private static void UpdatePullRequestStackDescriptions(IInputProvider inputProvider, IOutputProvider outputProvider, IGitHubClient gitHubClient, IStackConfig stackConfig, List<Config.Stack> stacks, Config.Stack stack, List<GitHubPullRequest> pullRequestsInStack)
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
        var prList = pullRequestsInStack
            .Select(pr => $"- {pr.Url}")
            .ToList();
        var prListMarkdown = string.Join(Environment.NewLine, prList);
        var prBodyMarkdown = $"{StackConstants.StackMarkerStart}{Environment.NewLine}{stack.PullRequestDescription}{Environment.NewLine}{Environment.NewLine}{prListMarkdown}{Environment.NewLine}{StackConstants.StackMarkerEnd}";

        foreach (var pullRequest in pullRequestsInStack)
        {
            // Find the existing part of the PR body that has the PR list
            // and replace it with the updated PR list
            var prBody = pullRequest.Body;

            var prListStart = prBody.IndexOf(StackConstants.StackMarkerStart, StringComparison.OrdinalIgnoreCase);
            var prListEnd = prBody.IndexOf(StackConstants.StackMarkerEnd, StringComparison.OrdinalIgnoreCase);

            if (prListStart >= 0 && prListEnd >= 0)
            {
                prBody = prBody.Remove(prListStart, prListEnd - prListStart + StackConstants.StackMarkerEnd.Length);
                prBody = prBody.Insert(prListStart, prBodyMarkdown);

                outputProvider.Information($"Updating pull request {pullRequest.GetPullRequestDisplay()} with stack details");

                gitHubClient.EditPullRequest(pullRequest.Number, prBody);
            }
        }
    }

    private static List<GitHubPullRequest> CreatePullRequests(
        IOutputProvider outputProvider,
        IGitHubClient gitHubClient,
        StackStatus status,
        List<PullRequestInformation> pullRequestCreateActions)
    {
        var pullRequests = new List<GitHubPullRequest>();
        foreach (var action in pullRequestCreateActions)
        {
            var branchDetail = status.Branches[action.HeadBranch];
            outputProvider.Information($"Creating pull request for branch {action.HeadBranch.Branch()} to {action.BaseBranch.Branch()}");
            var pullRequest = gitHubClient.CreatePullRequest(action.HeadBranch, action.BaseBranch, action.Title!, action.BodyFilePath!, action.Draft);

            if (pullRequest is not null)
            {
                outputProvider.Information($"Pull request {pullRequest.GetPullRequestDisplay()} created for branch {action.HeadBranch.Branch()} to {action.BaseBranch.Branch()}");
                pullRequests.Add(pullRequest);
                branchDetail.PullRequest = pullRequest;
            }
        }

        return pullRequests;
    }

    private static void OutputUpdatedStackStatus(IOutputProvider outputProvider, Config.Stack stack, StackStatus status, List<PullRequestInformation> pullRequestCreateActions)
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
                if (action is not null)
                {
                    branchDisplayItems.Add($"{StackHelpers.GetBranchStatusOutput(branch, parentBranch, branchDetail)} {$"*NEW* {action.Title}".Highlighted()}{(action.Draft == true ? " (draft)".Muted() : string.Empty)}");
                }
                else
                {
                    branchDisplayItems.Add(StackHelpers.GetBranchStatusOutput(branch, parentBranch, branchDetail));
                }
            }
            parentBranch = branch;
        }

        outputProvider.Tree(
            $"{stack.Name.Stack()}: {stack.SourceBranch.Muted()}",
            [.. branchDisplayItems]);
    }

    private static List<PullRequestInformation> GetPullRequestInformation(
        IInputProvider inputProvider,
        IOutputProvider outputProvider,
        IGitClient gitClient,
        IFileOperations fileOperations,
        List<PullRequestCreateAction> pullRequestCreateActions)
    {
        var pullRequestActions = new List<PullRequestInformation>();
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
            var pullRequestHeader = $"New pull request from {action.Branch.Branch()} -> {action.BaseBranch.Branch()}";
            outputProvider.Rule(pullRequestHeader);

            var title = inputProvider.Text(Questions.PullRequestTitle);
            var bodyFilePath = Path.Join(fileOperations.GetTempPath(), $"stack-pr-{Guid.NewGuid():N}.md");

            fileOperations.Create(bodyFilePath);

            if (pullRequestTemplatePath is not null)
                fileOperations.Copy(pullRequestTemplatePath, bodyFilePath, true);

            // Add the stack pr list markers to the body
            fileOperations.InsertText(bodyFilePath, 0, $@"{StackConstants.StackMarkerStart}
            
{StackConstants.StackMarkerDescription}

{StackConstants.StackMarkerEnd}");

            if (inputProvider.Confirm(Questions.EditPullRequestBody))
            {
                gitClient.OpenFileInEditorAndWaitForClose(bodyFilePath);
            }

            var draft = inputProvider.Confirm(Questions.CreatePullRequestAsDraft, false);

            pullRequestActions.Add(new PullRequestInformation(action.Branch, action.BaseBranch, title, bodyFilePath, draft));
        }

        return pullRequestActions;
    }

    record PullRequestInformation(string HeadBranch, string BaseBranch, string Title, string BodyFilePath, bool Draft);

    public record PullRequestCreateAction(string Branch, string BaseBranch);
}
