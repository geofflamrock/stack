using System;
using NSubstitute;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands;
using Stack.Config;
using Stack.Git;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Stack;

public class ListStacksCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCalled_ListsStacks()
    {
        // Arrange
        var console = Substitute.For<IAnsiConsole>();
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var command = new ListStacksCommand(console, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        var stacks = new List<Config.Stack>
        {
            new Config.Stack("Stack1", remoteUri, "main", ["branch-1"]),
            new Config.Stack("Stack2", remoteUri, "main", ["branch-2", "branch-3"]),
        };

        stackConfig.Load().Returns(stacks);

        // Act
        await command.ExecuteAsync(new CommandContext([], Substitute.For<IRemainingArguments>(), "list", null), new ListStacksCommandSettings());

        // Assert
        console.Received().Write("[yellow]Stack1[/] [grey](main)[/] 1 branch");
        console.Received().Write("[yellow]Stack2[/] [grey](main)[/] 2 branches");
    }
}
