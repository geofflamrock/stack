using Stack.Config;
using Stack.Commands;
using Stack.Tests.Helpers;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using Stack.Infrastructure;
using Stack.Commands.Helpers;

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

            var inputProvider = Substitute.For<IInputProvider>();
            inputProvider.Confirm(Questions.ConfirmMigrateConfig).Returns(true);
            var logger = new TestLogger(testOutputHelper);
            var handler = new MigrateConfigCommandHandler(inputProvider, logger, fileStackConfig);

            // Act
            await handler.Handle(new MigrateConfigCommandInputs(false));

            // Assert
            var migratedData = fileStackConfig.Load();
            migratedData.Should().BeEquivalentTo(new StackData(SchemaVersion.V2, [stack1, stack2]));
        }

        [Fact]
        public async Task WhenSchemaIsV1_WhenConfirmProvided_MigratesV1ToV2_DoesNotAskForConfirmation()
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

            var inputProvider = Substitute.For<IInputProvider>();
            var logger = new TestLogger(testOutputHelper);
            var handler = new MigrateConfigCommandHandler(inputProvider, logger, fileStackConfig);

            // Act
            await handler.Handle(new MigrateConfigCommandInputs(true));

            // Assert
            var migratedData = fileStackConfig.Load();
            migratedData.Should().BeEquivalentTo(new StackData(SchemaVersion.V2, [stack1, stack2]));

            inputProvider.DidNotReceive().Confirm(Questions.ConfirmMigrateConfig);
        }

        [Fact]
        public async Task WhenSchemaIsV2_DoesNotPerformMigration()
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

            fileStackConfig.Save(new StackData(SchemaVersion.V2, [stack1, stack2]));

            var inputProvider = Substitute.For<IInputProvider>();
            var logger = new TestLogger(testOutputHelper);
            var handler = new MigrateConfigCommandHandler(inputProvider, logger, fileStackConfig);

            // Act
            await handler.Handle(new MigrateConfigCommandInputs(false));

            // Assert
            var migratedData = fileStackConfig.Load();
            migratedData.Should().BeEquivalentTo(new StackData(SchemaVersion.V2, [stack1, stack2]));
        }

        [Fact]
        public async Task MigrateConfigCommand_WhenConfirmProvided_MigratesV1ToV2_DoesNotAskForConfirmation()
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

            var inputProvider = Substitute.For<IInputProvider>();
            var logger = new TestLogger(testOutputHelper);
            var handler = new MigrateConfigCommandHandler(inputProvider, logger, fileStackConfig);

            // Act
            await handler.Handle(new MigrateConfigCommandInputs(true));

            // Assert
            var migratedData = fileStackConfig.Load();
            migratedData.Should().BeEquivalentTo(new StackData(SchemaVersion.V2, [stack1, stack2]));

            inputProvider.DidNotReceive().Confirm(Questions.ConfirmMigrateConfig);
        }

        [Fact]
        public async Task WhenSchemaIsV2_DoesNotPerformMigration()
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

            fileStackConfig.Save(new StackData(SchemaVersion.V2, [stack1, stack2]));

            var inputProvider = Substitute.For<IInputProvider>();
            var logger = new TestLogger(testOutputHelper);
            var handler = new MigrateConfigCommandHandler(inputProvider, logger, fileStackConfig);

            // Act
            await handler.Handle(new MigrateConfigCommandInputs(false));

            // Assert
            var migratedData = fileStackConfig.Load();
            migratedData.Should().BeEquivalentTo(new StackData(SchemaVersion.V2, [stack1, stack2]));
        }
    }
}
