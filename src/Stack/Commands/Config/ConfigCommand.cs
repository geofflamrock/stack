namespace Stack.Commands;

public class ConfigCommand : GroupCommand
{
    public ConfigCommand() : base("config", "Manage stack configuration.")
    {
        Add(new OpenConfigCommand());
    }
}
