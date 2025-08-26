namespace Stack.Commands;

public class ConfigCommand : GroupCommand
{
    public ConfigCommand(OpenConfigCommand openConfigCommand) : base("config", "Manage stack configuration.")
    {
        Add(openConfigCommand);
    }
}
