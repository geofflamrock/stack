using System.CommandLine;
using Microsoft.Extensions.Logging;
using MoreLinq;
using Spectre.Console;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public class CreatePullRequestsCommand : Command
{
    private readonly CreatePullRequestsCommandHandler handler;

    public CreatePullRequestsCommand(
        CreatePullRequestsCommandHandler handler,
        CliExecutionContext executionContext,
        IInputProvider inputProvider,
        IOutputProvider outputProvider,
        ILogger<CreatePullRequestsCommand> logger)
        : base("create", "Create pull requests for a stack.", executionContext, inputProvider, outputProvider, logger)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await handler.Handle(
            new CreatePullRequestsCommandInputs(
                parseResult.GetValue(CommonOptions.Stack)),
            cancellationToken);
    }
}

public record CreatePullRequestsCommandInputs(string? Stack)
{
    public static CreatePullRequestsCommandInputs Empty => new((string?)null);
}

public class CreatePullRequestsCommandHandler(
    IInputProvider inputProvider,
    ILogger<CreatePullRequestsCommandHandler> logger,
    IOutputProvider outputProvider,
    IDisplayProvider displayProvider,
    IGitClientFactory gitClientFactory,
    CliExecutionContext executionContext,
    IGitHubClient gitHubClient,
    IFileOperations fileOperations,
    IStackRepository repository)
    : CommandHandlerBase<CreatePullRequestsCommandInputs>
{
    public override async Task Handle(CreatePullRequestsCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var gitClient = gitClientFactory.Create(executionContext.WorkingDirectory);
        var stacksForRemote = repository.GetStacks();

        if (stacksForRemote.Count == 0)
        {
            logger.NoStacksForRepository();
            return;
        }

        var currentBranch = gitClient.GetCurrentBranch();
        var stack = await inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch, cancellationToken);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");
        }

        var status = StackHelpers.GetStackStatus(
            stack,
            currentBranch,
            logger,
            gitClient,
            gitHubClient,
            true);

        var pullRequestCreateActions = new List<PullRequestCreateAction>();

        var allBranchLines = status.GetAllBranchLines();

        foreach (var branchLine in allBranchLines)
        {
            var sourceBranch = stack.SourceBranch;

            foreach (var branch in branchLine)
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
        }

        await StackHelpers.OutputStackStatus(status, outputProvider, cancellationToken);

        await outputProvider.WriteNewLine(cancellationToken);

        if (pullRequestCreateActions.Count > 0)
        {
            var selectedPullRequestActions = (await inputProvider.MultiSelect(
                Questions.SelectPullRequestsToCreate,
                pullRequestCreateActions.ToArray(),
                true,
                cancellationToken,
                action => $"{action.Branch} -> {action.BaseBranch}")).ToList();

            logger.Question(Questions.SelectPullRequestsToCreate);

            foreach (var action in selectedPullRequestActions)
            {
                logger.PullRequestSelected(action.Branch, action.BaseBranch);
            }

            await outputProvider.WriteNewLine(cancellationToken);

            var pullRequestInformation = await GetPullRequestInformation(
                inputProvider,
                logger,
                outputProvider,
                displayProvider,
                gitClient,
                fileOperations,
                selectedPullRequestActions,
                cancellationToken);

            await outputProvider.WriteNewLine(cancellationToken);

            await OutputUpdatedStackStatus(outputProvider, status, pullRequestInformation, cancellationToken);

            await outputProvider.WriteNewLine(cancellationToken);

            if (await inputProvider.Confirm(Questions.ConfirmCreatePullRequests, cancellationToken))
            {
                var newPullRequests = CreatePullRequests(logger, gitHubClient, status, pullRequestInformation);

                var pullRequestsInStack = status.GetAllBranches()
                    .Where(branch => branch.HasPullRequest)
                    .Select(branch => branch.PullRequest!)
                    .Concat(newPullRequests)
                    .ToList();

                if (pullRequestsInStack.Count > 1)
                {
                    StackHelpers.UpdateStackPullRequestList(logger, gitHubClient, stack, pullRequestsInStack);
                }

                if (await inputProvider.Confirm(Questions.OpenPullRequests, cancellationToken))
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
            logger.NoPullRequestsToCreate();
        }
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
            logger.CreatingPullRequest(action.HeadBranch, action.BaseBranch);
            var pullRequest = gitHubClient.CreatePullRequest(
                action.HeadBranch,
                action.BaseBranch,
                action.Title!,
                action.BodyFilePath!,
                action.Draft);

            if (pullRequest is not null)
            {
                logger.PullRequestCreated(pullRequest.GetPullRequestDisplay(), action.HeadBranch, action.BaseBranch);
                pullRequests.Add(pullRequest);
            }
        }

        return pullRequests;
    }

    private static async Task OutputUpdatedStackStatus(
        IOutputProvider outputProvider,
        StackStatus status,
        List<PullRequestInformation> pullRequestInformation,
        CancellationToken cancellationToken)
    {
        await StackHelpers.OutputStackStatus(
            status,
            outputProvider,
            cancellationToken,
            (branch) =>
            {
                var pr = pullRequestInformation.FirstOrDefault(pr => pr.HeadBranch == branch.Name);

                if (pr is not null)
                {
                    return $" {$"*NEW* {pr.Title}".Highlighted()}{(pr.Draft == true ? " (draft)".Muted() : string.Empty)}";
                }

                return null;
            });
    }

    private static async Task<List<PullRequestInformation>> GetPullRequestInformation(
        IInputProvider inputProvider,
        ILogger logger,
        IOutputProvider outputProvider,
        IDisplayProvider displayProvider,
        IGitClient gitClient,
        IFileOperations fileOperations,
        List<PullRequestCreateAction> pullRequestCreateActions,
        CancellationToken cancellationToken)
    {
        var pullRequestActions = new List<PullRequestInformation>();
        var pullRequestTemplateFileNames = new List<string>(["PULL_REQUEST_TEMPLATE.md", "pull_request_template.md"]);

        var pullRequestTemplatePath = pullRequestTemplateFileNames
            .Select(fileName => Path.Join(gitClient.GetRootOfRepository(), ".github", fileName))
            .FirstOrDefault(fileOperations.Exists);

        if (pullRequestTemplatePath is not null)
        {
            logger.FoundPullRequestTemplate(pullRequestTemplatePath);
        }

        foreach (var action in pullRequestCreateActions)
        {
            await outputProvider.WriteNewLine(cancellationToken);
            await outputProvider.WriteHeader($"New pull request from {action.Branch.Branch()} -> {action.BaseBranch.Branch()}", cancellationToken);

            var title = inputProvider.Text(Questions.PullRequestTitle, cancellationToken).Result;
            var bodyFilePath = Path.Join(fileOperations.GetTempPath(), $"stack-pr-{Guid.NewGuid():N}.md");

            fileOperations.Create(bodyFilePath);

            if (pullRequestTemplatePath is not null)
                fileOperations.Copy(pullRequestTemplatePath, bodyFilePath, true);

            // Add the stack pr list markers to the body
            fileOperations.InsertText(bodyFilePath, 0, $@"{StackConstants.StackMarkerStart}
            
{StackConstants.StackMarkerDescription}

{StackConstants.StackMarkerEnd}");

            var draft = await inputProvider.Confirm(Questions.CreatePullRequestAsDraft, cancellationToken, false);

            pullRequestActions.Add(new PullRequestInformation(
                action.Branch,
                action.BaseBranch,
                title,
                bodyFilePath,
                draft));
        }

        return pullRequestActions;
    }

    public record PullRequestInformation(
        string HeadBranch,
        string BaseBranch,
        string Title,
        string BodyFilePath,
        bool Draft);

    public record PullRequestCreateAction(string Branch, string BaseBranch);
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Pull request selected: {HeadBranch} -> {BaseBranch}")]
    public static partial void PullRequestSelected(this ILogger logger, string headBranch, string baseBranch);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating pull request for branch {HeadBranch} to {BaseBranch}")]
    public static partial void CreatingPullRequest(this ILogger logger, string headBranch, string baseBranch);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pull request \"{PullRequest}\" created for branch {HeadBranch} to {BaseBranch}")]
    public static partial void PullRequestCreated(this ILogger logger, string pullRequest, string headBranch, string baseBranch);

    [LoggerMessage(Level = LogLevel.Information, Message = "No new pull requests to create.")]
    public static partial void NoPullRequestsToCreate(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found pull request template at \"{TemplatePath}\", this will be used as the default body for each pull request.")]
    public static partial void FoundPullRequestTemplate(this ILogger logger, string templatePath);
}