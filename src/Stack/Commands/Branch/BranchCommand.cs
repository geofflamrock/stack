namespace Stack.Commands;

public class BranchCommand : GroupCommand
{
    public BranchCommand() : base("branch", "Manage branches within a stack.")
    {
        Add(new AddBranchCommand());
        Add(new NewBranchCommand());
        Add(new RemoveBranchCommand());
    }
}

