namespace Stack.Commands;

public abstract class GroupCommand(string name, string? description = null) : System.CommandLine.Command(name, description)
{
}
