namespace Stack.Commands;

public static class HumanizeEnumExtensionMethods
{
    public static string Humanize(this BranchAction action)
    {
        return action switch
        {
            BranchAction.Add => "Add an existing branch",
            BranchAction.Create => "Create a new branch",
            BranchAction.None => "Do not add or create a branch",
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }

    public static string Humanize(this RemoveBranchChildAction action)
    {
        return action switch
        {
            RemoveBranchChildAction.RemoveChildren => "Remove children branches",
            RemoveBranchChildAction.MoveChildrenToParent => "Move children branches to parent branch",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    public static string Humanize(this MoveBranchChildAction action)
    {
        return action switch
        {
            MoveBranchChildAction.MoveChildren => "Move children branches with the branch being moved",
            MoveBranchChildAction.ReParentChildren => "Re-parent children branches to the previous location",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }
}


