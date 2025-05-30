using Stack.Config;
using Stack.Infrastructure;

namespace Stack.Commands;

public class MigrateConfigCommandSettings : CommandSettingsBase
{
}

public class MigrateConfigCommand : Command<MigrateConfigCommandSettings>
{
    protected override async Task Execute(MigrateConfigCommandSettings settings)
    {
        var handler = new MigrateConfigCommandHandler(StdErrLogger, new FileStackConfig());
        await handler.Handle(new MigrateConfigCommandInputs());
    }
}

public record MigrateConfigCommandInputs();

public class MigrateConfigCommandHandler(ILogger logger, IStackConfig stackConfig) : CommandHandlerBase<MigrateConfigCommandInputs>
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

        // Save as v2 (this will create a backup of the v1 file)
        var v2Data = new StackData(SchemaVersion.V2, stackData.Stacks);
        stackConfig.Save(v2Data);
        var backupFilePath = $"{configFilePath}.v1-backup.json";
        logger.Information($"Migration to v2 format completed successfully. Backup of the original config has been saved to '{backupFilePath}'.");

    }
}