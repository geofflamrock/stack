namespace Stack.Commands;

public abstract class GroupCommand(string name, string? description, IServiceProvider serviceProvider) : System.CommandLine.Command(name, description)
{
    protected IServiceProvider ServiceProvider = serviceProvider;
}
