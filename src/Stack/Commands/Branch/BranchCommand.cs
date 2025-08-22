namespace Stack.Commands;

public class BranchCommand : GroupCommand
{
    public BranchCommand(
        AddBranchCommand addBranchCommand,
        NewBranchCommand newBranchCommand,
        RemoveBranchCommand removeBranchCommand) : base("branch", "Manage branches within a stack.")
    {
        Add(addBranchCommand);
        Add(newBranchCommand);
        Add(removeBranchCommand);
    }
}

