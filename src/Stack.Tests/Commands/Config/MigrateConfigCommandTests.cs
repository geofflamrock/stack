using Stack.Config;
using Stack.Commands;
using Stack.Tests.Helpers;
using Xunit.Abstractions;
using FluentAssertions;

namespace Stack.Tests.Commands
{
    public class MigrateConfigCommandTests(ITestOutputHelper testOutputHelper)
    {
        [Fact]
        public async Task WhenSchemaIsV1_AsksForConfirmation_MigratesV1ToV2()
        {
            // Arrange
            using var temporaryDirectory = TemporaryDirectory.Create();
            var fileStackConfig = new FileStackConfig(temporaryDirectory.DirectoryPath);
            var configPath = fileStackConfig.GetConfigPath();

            var stack1 = new TestStackBuilder()
                .WithBranch(b => b.WithName("branch-1").WithChildBranch(b => b.WithName("branch-2")))
                .Build();

            var stack2 = new TestStackBuilder()
                .WithBranch(b => b.WithName("branch-3").WithChildBranch(b => b.WithName("branch-4")))
                .Build();


            fileStackConfig.Save(new StackData(SchemaVersion.V1, [stack1, stack2]));

            var logger = new TestLogger(testOutputHelper);
            var handler = new MigrateConfigCommandHandler(logger, fileStackConfig);

            // Act
            await handler.Handle(new MigrateConfigCommandInputs());

            // Assert
            var migratedData = fileStackConfig.Load();
            migratedData.Should().BeEquivalentTo(new StackData(SchemaVersion.V2, [stack1, stack2]));
        }
    }
}
