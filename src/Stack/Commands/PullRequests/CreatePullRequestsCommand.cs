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

public class CreatePullRequestsCommand : Command<CreatePullRequestsCommandSettings>
{
    protected override async Task Execute(CreatePullRequestsCommandSettings settings)
    {
        var handler = new CreatePullRequestsCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, settings.GetGitClientSettings()),
            new GitHubClient(StdErrLogger, settings.GetGitHubClientSettings()),
            new FileOperations(),
            new FileStackConfig());

        await handler.Handle(new CreatePullRequestsCommandInputs(settings.Stack));
    }
}

public record CreatePullRequestsCommandInputs(string? Stack)
{
    public static CreatePullRequestsCommandInputs Empty => new((string?)null);
}

public class CreatePullRequestsCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IFileOperations fileOperations,
    IStackConfig stackConfig)
    : CommandHandlerBase<CreatePullRequestsCommandInputs>
{
    public override async Task Handle(CreatePullRequestsCommandInputs inputs)
    {
        await Task.CompletedTask;

        var stackData = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            logger.Information("No stacks found for current repository.");
            return;
        }

        var currentBranch = gitClient.GetCurrentBranch();
        var stack = inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");
        }

        var status = StackHelpers.GetStackStatus(
            stack,
            currentBranch,
            logger,
            gitClient,
            gitHubClient);

        var sourceBranch = stack.SourceBranch;
        var pullRequestCreateActions = new List<PullRequestCreateAction>();

        foreach (var branch in status.Branches)
        {
            if (branch.IsActive)
            {
                if (!branch.HasPullRequest)
                {
                    pullRequestCreateActions.Add(new PullRequestCreateAction(branch.Name, sourceBranch));
                }

                sourceBranch = branch.Name;
            }
        }

        StackHelpers.OutputStackStatus(status, logger);

        logger.NewLine();

        if (pullRequestCreateActions.Count > 0)
        {
            var selectedPullRequestActions = inputProvider.MultiSelect(
                Questions.SelectPullRequestsToCreate,
                pullRequestCreateActions.ToArray(),
                true,
                action => $"{action.Branch} -> {action.BaseBranch}")
                .ToList();

            logger.Information("Select branches to create pull requests for:");

            foreach (var action in selectedPullRequestActions)
            {
                logger.Information($"  {action.Branch} -> {action.BaseBranch}");
            }

            logger.NewLine();

            var pullRequestInformation = GetPullRequestInformation(
                inputProvider,
                logger,
                gitClient,
                gitHubClient,
                fileOperations,
                selectedPullRequestActions);

            logger.NewLine();

            OutputUpdatedStackStatus(logger, stack, status, pullRequestInformation);

            logger.NewLine();

            if (inputProvider.Confirm(Questions.ConfirmCreatePullRequests))
            {
                var newPullRequests = CreatePullRequests(logger, gitHubClient, status, pullRequestInformation);

                // Re-get the status to pick up PRs
                status = StackHelpers.GetStackStatus(
                    stack,
                    currentBranch,
                    logger,
                    gitClient,
                    gitHubClient);

                var pullRequestsInStack = status.Branches
                    .Where(branch => branch.HasPullRequest)
                    .Select(branch => branch.PullRequest!)
                    .ToList();

                if (pullRequestsInStack.Count > 1)
                {
                    UpdatePullRequestStackDescriptions(inputProvider, logger, gitHubClient, stackConfig, stackData, stack, pullRequestsInStack);
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
            logger.Information("No new pull requests to create.");
        }
    }

    private static void UpdatePullRequestStackDescriptions(IInputProvider inputProvider, ILogger logger, IGitHubClient gitHubClient, IStackConfig stackConfig, StackData stacks, Config.Stack stack, List<GitHubPullRequest> pullRequestsInStack)
    {
        if (stack.PullRequestDescription is null)
        {
            StackHelpers.UpdateStackPullRequestDescription(inputProvider, stackConfig, stacks, stack);
        }

        StackHelpers.UpdateStackDescriptionInPullRequests(logger, gitHubClient, stack, pullRequestsInStack);
    }

    private static List<GitHubPullRequest> CreatePullRequests(
        ILogger logger,
        IGitHubClient gitHubClient,
        StackStatus status,
        List<PullRequestInformation> pullRequestCreateActions)
    {
        var pullRequests = new List<GitHubPullRequest>();
        foreach (var action in pullRequestCreateActions)
        {
            var branchDetail = status.Branches.First(b => b.Name == action.HeadBranch);
            logger.Information($"Creating pull request for branch {action.HeadBranch.Branch()} to {action.BaseBranch.Branch()}");
            var pullRequest = gitHubClient.CreatePullRequest(
                action.HeadBranch,
                action.BaseBranch,
                action.Title!,
                action.BodyFilePath!,
                action.Draft);

            if (pullRequest is not null)
            {
                logger.Information($"Pull request {pullRequest.GetPullRequestDisplay()} created for branch {action.HeadBranch.Branch()} to {action.BaseBranch.Branch()}");
                pullRequests.Add(pullRequest);
            }
        }

        return pullRequests;
    }

    private static void OutputUpdatedStackStatus(ILogger logger, Config.Stack stack, StackStatus status, List<PullRequestInformation> pullRequestCreateActions)
    {
        var branchDisplayItems = new List<string>();

        foreach (var branch in status.Branches)
        {
            if (branch.PullRequest is not null && branch.PullRequest.State != GitHubPullRequestStates.Closed)
            {
                branchDisplayItems.Add(StackHelpers.GetBranchAndPullRequestStatusOutput(branch));
            }
            else
            {
                var action = pullRequestCreateActions.FirstOrDefault(a => a.HeadBranch == branch.Name);
                if (action is not null)
                {
                    branchDisplayItems.Add($"{StackHelpers.GetBranchStatusOutput(branch)} {$"*NEW* {action.Title}".Highlighted()}{(action.Draft == true ? " (draft)".Muted() : string.Empty)}");
                }
                else
                {
                    branchDisplayItems.Add(StackHelpers.GetBranchStatusOutput(branch));
                }
            }
        }

        logger.Tree(
            $"{stack.Name.Stack()}: {stack.SourceBranch.Muted()}",
            [.. branchDisplayItems]);
    }

    private static List<PullRequestInformation> GetPullRequestInformation(
        IInputProvider inputProvider,
        ILogger logger,
        IGitClient gitClient,
        IGitHubClient gitHubClient,
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
            logger.Information($"Found pull request template in repository, this will be used as the default body for each pull request.");
        }

        foreach (var action in pullRequestCreateActions)
        {
            logger.NewLine();
            var pullRequestHeader = $"New pull request from {action.Branch.Branch()} -> {action.BaseBranch.Branch()}";
            logger.Rule(pullRequestHeader);

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

            pullRequestActions.Add(new PullRequestInformation(
                action.Branch,
                action.BaseBranch,
                title,
                bodyFilePath,
                draft));
        }

        return pullRequestActions;
    }

    record PullRequestInformation(
        string HeadBranch,
        string BaseBranch,
        string Title,
        string BodyFilePath,
        bool Draft);

    public record PullRequestCreateAction(string Branch, string BaseBranch);
}
