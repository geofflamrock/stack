namespace Stack.Commands;

public abstract class GroupCommand(string name, string? description) : System.CommandLine.Command(name, description)
{
}
