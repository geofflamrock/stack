using FluentAssertions;
using NSubstitute;
using Stack.Commands.Helpers;
using Stack.Git;
using Stack.Infrastructure;
using Xunit.Abstractions;

namespace Stack.Tests.Helpers;

public class StackHelpersTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void UpdateStack_WhenThereAreConflictsMergingBranches_AndUpdateIsContinued_TheUpdateCompletesSuccessfully()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var inputProvider = Substitute.For<IInputProvider>();

        var stack = new Config.Stack("Stack1", Some.HttpsUri().ToString(), sourceBranch, [branch1, branch2]);
        var branchDetail1 = new BranchDetail
        {
            Status = new BranchStatus(true, true, true, false, 0, 0, 0, 0, null)
        };
        var branchDetail2 = new BranchDetail
        {
            Status = new BranchStatus(true, true, true, false, 0, 0, 0, 0, null)
        };

        var stackStatus = new StackStatus(new Dictionary<string, BranchDetail>
        {
            { branch1, branchDetail1 },
            { branch2, branchDetail2 },
        });

        inputProvider
            .Select(
                Questions.ContinueOrAbortMerge,
                Arg.Any<MergeConflictAction[]>(),
                Arg.Any<Func<MergeConflictAction, string>>())
            .Returns(MergeConflictAction.Continue);

        gitClient
            .When(g => g.MergeFromLocalSourceBranch(sourceBranch))
            .Throws(new ConflictException());

        // Act
        StackHelpers.UpdateStack(
            stack,
            stackStatus,
            UpdateStrategy.Merge,
            gitClient,
            inputProvider,
            logger
        );

        // Assert
        gitClient.Received().ChangeBranch(branch2);
        gitClient.Received().MergeFromLocalSourceBranch(branch1);
    }

    [Fact]
    public void UpdateStack_WhenThereAreConflictsMergingBranches_AndUpdateIsAborted_AnExceptionIsThrown()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var inputProvider = Substitute.For<IInputProvider>();
        var gitClient = Substitute.For<IGitClient>();
        var logger = new TestLogger(testOutputHelper);

        var stack = new Config.Stack("Stack1", Some.HttpsUri().ToString(), sourceBranch, [branch1, branch2]);
        var branchDetail1 = new BranchDetail
        {
            Status = new BranchStatus(true, true, true, false, 0, 0, 0, 0, null)
        };
        var branchDetail2 = new BranchDetail
        {
            Status = new BranchStatus(true, true, true, false, 0, 0, 0, 0, null)
        };

        var stackStatus = new StackStatus(new Dictionary<string, BranchDetail>
        {
            { branch1, branchDetail1 },
            { branch2, branchDetail2 },
        });

        gitClient
            .When(g => g.MergeFromLocalSourceBranch(sourceBranch))
            .Throws(new ConflictException());

        inputProvider
            .Select(
                Questions.ContinueOrAbortMerge,
                Arg.Any<MergeConflictAction[]>(),
                Arg.Any<Func<MergeConflictAction, string>>())
            .Returns(MergeConflictAction.Abort);

        // Act
        var updateAction = () => StackHelpers.UpdateStack(
            stack,
            stackStatus,
            UpdateStrategy.Merge,
            gitClient,
            inputProvider,
            logger
        );

        // Assert
        updateAction.Should().Throw<Exception>().WithMessage("Merge aborted due to conflicts.");
        gitClient.Received().AbortMerge();
        gitClient.DidNotReceive().ChangeBranch(branch2);
        gitClient.DidNotReceive().MergeFromLocalSourceBranch(branch1);
    }
}