using FluentAssertions;
using NSubstitute;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Stack.Commands;
using Stack.Config;
using Stack.Git;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Stack;

public class NewStackCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WhenAllOptionsProvided_CreatesStack()
    {
        // Arrange
        var console = new TestConsole();
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var command = new NewStackCommand(console, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate(Arg.Any<GitOperationSettings>()).Returns(["branch-1", "branch-2"]);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        // Act
        await command.ExecuteAsync(
            new CommandContext([], Substitute.For<IRemainingArguments>(), "new", null),
            new NewStackCommandSettings
            {
                Name = "Stack1",
                SourceBranch = "branch-1",
                BranchName = "new-branch"
            });

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["new-branch"])
        });

        gitOperations.Received().ChangeBranch("new-branch", Arg.Any<GitOperationSettings>());
    }
}
