namespace Stack.Model;

public static class StackExtensionMethods
{
    public static bool IsCurrentStack(this Stack stack, string currentBranch)
    {
        return stack.SourceBranch.Equals(currentBranch, StringComparison.OrdinalIgnoreCase) ||
               stack.AllBranchNames.Contains(currentBranch);
    }

    public static IOrderedEnumerable<Stack> OrderByCurrentStackThenByName(this List<Stack> stacks, string currentBranch)
    {
        return stacks.OrderBy(s => s.IsCurrentStack(currentBranch) ? 0 : 1).ThenBy(s => s.Name);
    }

    public static Branch? GetDeepestChildBranchFromFirstTree(this Stack stack)
    {
        if (stack.Branches.Count == 0)
        {
            return null;
        }

        return GetDeepestChildBranchFromFirstTree(stack.Branches.First());
    }

    static Branch GetDeepestChildBranchFromFirstTree(Branch branch)
    {
        if (branch.Children.Count == 0)
        {
            return branch;
        }

        return GetDeepestChildBranchFromFirstTree(branch.Children.First());
    }
}
