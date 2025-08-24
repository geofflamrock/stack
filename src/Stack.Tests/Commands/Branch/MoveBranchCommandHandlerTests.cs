using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;
using Xunit.Abstractions;

namespace Stack.Tests.Commands.Branch;

public class MoveBranchCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenNoInputsProvided_AsksForInputs_MovesBranchAndSaves()
    {
        var source = Some.BranchName();
        var A = Some.BranchName();
        var B = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(s => s
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(source)
                .WithBranch(b => b.WithName(A).WithChildBranch(c => c.WithName(B))))
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var git = Substitute.For<IGitClient>();
        git.GetRemoteUri().Returns(remoteUri);
        git.GetCurrentBranch().Returns(source);

        var handler = new MoveBranchCommandHandler(inputProvider, logger, git, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(B);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>()).Returns(source);
        inputProvider.Select(Questions.MoveBranchChildrenAction, Arg.Any<global::Stack.Config.MoveBranchChildrenAction[]>(), Arg.Any<Func<global::Stack.Config.MoveBranchChildrenAction, string>>())
            .Returns(global::Stack.Config.MoveBranchChildrenAction.KeepChildrenWithOldParent);

        await handler.Handle(MoveBranchCommandInputs.Empty);

        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, source, [ new Config.Branch(A, []), new Config.Branch(B, []) ])
        });
    }

    [Fact]
    public async Task WhenAllInputsProvided_DoesNotPrompt_MovesBranch()
    {
        var source = Some.BranchName();
        var A = Some.BranchName();
        var B = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(s => s
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(source)
                .WithBranch(b => b.WithName(A).WithChildBranch(c => c.WithName(B))))
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var git = Substitute.For<IGitClient>();
        git.GetRemoteUri().Returns(remoteUri);
        git.GetCurrentBranch().Returns(source);

        var handler = new MoveBranchCommandHandler(inputProvider, logger, git, stackConfig);

        await handler.Handle(new MoveBranchCommandInputs("Stack1", B, source, global::Stack.Config.MoveBranchChildrenAction.MoveChildrenWithBranch));

        inputProvider.ReceivedCalls().Should().BeEmpty();
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, source, [ new Config.Branch(A, [ new Config.Branch(B, []) ]) ])
        });
    }
}
