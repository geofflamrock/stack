using FluentAssertions;
using NSubstitute;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Stack.Commands;
using Stack.Config;
using Stack.Git;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Stack;

public class ListStacksCommandTests
{
    // [Fact]
    // public async Task ExecuteAsync_WhenMultipleStacksExistForTheRemote_ListsStacks()
    // {
    //     // Arrange
    //     var console = new TestConsole();
    //     var gitOperations = Substitute.For<IGitOperations>();
    //     var stackConfig = Substitute.For<IStackConfig>();
    //     var command = new ListStacksCommand(console, gitOperations, stackConfig);

    //     var remoteUri = Some.HttpsUri().ToString();
    //     var aDifferentRemoteUri = Some.HttpsUri().ToString();

    //     gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);

    //     var stacks = new List<Config.Stack>
    //     {
    //         new("Stack1", remoteUri, "main", ["branch-1"]),
    //         new("Stack2", remoteUri, "main", ["branch-2", "branch-3"]),
    //         new("Stack3", aDifferentRemoteUri, "main", ["branch-1"]),
    //     };

    //     stackConfig.Load().Returns(stacks);

    //     // Act
    //     await command.ExecuteAsync(new CommandContext([], Substitute.For<IRemainingArguments>(), "list", null), new ListStacksCommandSettings());

    //     // Assert
    //     console.Output.Should().Contain("Stack1 (main) 1 branch");
    //     console.Output.Should().Contain("Stack2 (main) 2 branches");
    // }

    // [Fact]
    // public async Task ExecuteAsync_WhenMultipleStacksExistForADifferentRemote_ReturnsEmptyMessage()
    // {
    //     // Arrange
    //     var console = new TestConsole();
    //     var gitOperations = Substitute.For<IGitOperations>();
    //     var stackConfig = Substitute.For<IStackConfig>();
    //     var command = new ListStacksCommand(console, gitOperations, stackConfig);

    //     var remoteUri = Some.HttpsUri().ToString();
    //     var differentRemoteUri = Some.HttpsUri().ToString();

    //     gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);

    //     var stacks = new List<Config.Stack>
    //     {
    //         new("Stack1", differentRemoteUri, "main", ["branch-1"]),
    //         new("Stack2", differentRemoteUri, "main", ["branch-2", "branch-3"]),
    //     };

    //     stackConfig.Load().Returns(stacks);

    //     // Act
    //     await command.ExecuteAsync(new CommandContext([], Substitute.For<IRemainingArguments>(), "list", null), new ListStacksCommandSettings());

    //     // Assert
    //     console.Output.Should().Contain("No stacks found for current repository.");
    // }
}
