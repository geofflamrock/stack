using System.CommandLine;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Infrastructure;

namespace Stack.Commands;

public class MigrateConfigCommand : Command
{
    public MigrateConfigCommand() : base("migrate", "Migrate the configuration file from v1 to v2 format (preview).")
    {
        Add(CommonOptions.Confirm);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var handler = new MigrateConfigCommandHandler(InputProvider, StdErrLogger, new FileStackConfig());
        await handler.Handle(new MigrateConfigCommandInputs(
            parseResult.GetValue(CommonOptions.Confirm)));
    }
}

public record MigrateConfigCommandInputs(bool Confirm);

public class MigrateConfigCommandHandler(IInputProvider inputProvider, ILogger logger, IStackConfig stackConfig) : CommandHandlerBase<MigrateConfigCommandInputs>
{
    public override async Task Handle(MigrateConfigCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stackData = stackConfig.Load();
        var configFilePath = stackConfig.GetConfigPath();
        if (stackData.SchemaVersion == SchemaVersion.V2)
        {
            logger.Information("Config file is already in v2 format. No migration needed.");
            return;
        }

        if (inputs.Confirm || inputProvider.Confirm(Questions.ConfirmMigrateConfig))
        {
            // Save as v2 (this will create a backup of the v1 file)
            var v2Data = new StackData(SchemaVersion.V2, stackData.Stacks);
            stackConfig.Save(v2Data);
            var backupFilePath = $"{configFilePath}.v1-backup.json";
            logger.Information($"Migration to v2 format completed successfully. Backup of the original config has been saved to '{backupFilePath}'.");
        }
    }
}