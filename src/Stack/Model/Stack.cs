using System.ComponentModel;
using Stack.Commands;

namespace Stack.Model;

public record Stack(string Name, string SourceBranch, List<Branch> Branches)
{
    public List<Branch> GetAllBranches()
    {
        var branchesToReturn = new List<Branch>();
        foreach (var branch in Branches)
        {
            branchesToReturn.Add(branch);
            branchesToReturn.AddRange(GetAllBranches(branch));
        }

        return branchesToReturn;
    }

    static List<Branch> GetAllBranches(Branch branch)
    {
        var branchesToReturn = new List<Branch>();
        foreach (var child in branch.Children)
        {
            branchesToReturn.Add(child);
            branchesToReturn.AddRange(GetAllBranches(child));
        }

        return branchesToReturn;
    }

    public List<string> AllBranchNames => [.. GetAllBranches().Select(b => b.Name).Distinct()];

    public bool HasSingleTree => GetAllBranchLines().Count == 1;

    public void RemoveBranch(string branchName, RemoveBranchChildAction action)
    {
        if (Branches.Count == 0)
        {
            return;
        }

        foreach (var branch in Branches)
        {
            if (branch.Name.Equals(branchName, StringComparison.OrdinalIgnoreCase))
            {
                Branches.Remove(branch);

                if (action == RemoveBranchChildAction.MoveChildrenToParent)
                {
                    Branches.AddRange(branch.Children);
                }
                return;
            }

            if (RemoveBranch(branch, branchName, action))
            {
                return;
            }
        }
    }

    static bool RemoveBranch(Branch branch, string branchName, RemoveBranchChildAction action)
    {
        var childBranch = branch.Children.FirstOrDefault(c => c.Name.Equals(branchName, StringComparison.OrdinalIgnoreCase));
        if (childBranch != null)
        {
            branch.Children.Remove(childBranch);
            if (action == RemoveBranchChildAction.MoveChildrenToParent)
            {
                // Get all the children of the branch to be removed,
                // and add them to the parent of the branch being removed.
                branch.Children.AddRange(childBranch.Children);
            }
            return true;
        }

        foreach (var child in branch.Children)
        {
            if (RemoveBranch(child, branchName, action))
            {
                return true;
            }
        }

        return false;
    }

    public void MoveBranch(string branchName, string newParentBranchName, MoveBranchChildAction childAction)
    {
        // First, find and extract the branch being moved
        var (branchToMove, originalParentName, childrenToReParent) = ExtractBranch(branchName, childAction);
        if (branchToMove is null)
        {
            throw new InvalidOperationException($"Branch '{branchName}' not found in stack.");
        }

        // Add the moved branch to the new parent location
        AddBranchToParent(branchToMove, newParentBranchName);

        // Re-parent children to their original location if needed
        if (childrenToReParent.Count > 0)
        {
            foreach (var child in childrenToReParent)
            {
                AddBranchToParent(child, originalParentName);
            }
        }
    }

    private (Branch? branchToMove, string originalParentName, List<Branch> childrenToReParent) ExtractBranch(string branchName, MoveBranchChildAction childAction)
    {
        // Check root level branches
        for (int i = 0; i < Branches.Count; i++)
        {
            var branch = Branches[i];
            if (branch.Name.Equals(branchName, StringComparison.OrdinalIgnoreCase))
            {
                Branches.RemoveAt(i);

                if (childAction == MoveBranchChildAction.ReParentChildren)
                {
                    // Children should be re-parented to the source branch (root level)
                    var childrenToReParent = branch.Children.ToList();
                    return (new Branch(branch.Name, new List<Branch>()), SourceBranch, childrenToReParent);
                }
                else
                {
                    return (branch, SourceBranch, new List<Branch>());
                }
            }
        }

        // Check nested branches
        foreach (var branch in Branches)
        {
            var result = ExtractBranchFromChildren(branch, branchName, childAction);
            if (result.branchToMove is not null)
            {
                return result;
            }
        }

        return (null, string.Empty, new List<Branch>());
    }

    private static (Branch? branchToMove, string originalParentName, List<Branch> childrenToReParent) ExtractBranchFromChildren(Branch parentBranch, string branchName, MoveBranchChildAction childAction)
    {
        for (int i = 0; i < parentBranch.Children.Count; i++)
        {
            var childBranch = parentBranch.Children[i];
            if (childBranch.Name.Equals(branchName, StringComparison.OrdinalIgnoreCase))
            {
                parentBranch.Children.RemoveAt(i);

                if (childAction == MoveBranchChildAction.ReParentChildren)
                {
                    // Children should be re-parented to the original parent
                    var childrenToReParent = childBranch.Children.ToList();
                    return (new Branch(childBranch.Name, new List<Branch>()), parentBranch.Name, childrenToReParent);
                }
                else
                {
                    return (childBranch, parentBranch.Name, new List<Branch>());
                }
            }
        }

        foreach (var child in parentBranch.Children)
        {
            var result = ExtractBranchFromChildren(child, branchName, childAction);
            if (result.branchToMove is not null)
            {
                return result;
            }
        }

        return (null, string.Empty, new List<Branch>());
    }

    private void AddBranchToParent(Branch branchToMove, string parentBranchName)
    {
        // If the parent is the source branch, add to root level
        if (parentBranchName.Equals(SourceBranch, StringComparison.OrdinalIgnoreCase))
        {
            Branches.Add(branchToMove);
            return;
        }

        // Find the parent branch and add as child
        foreach (var branch in Branches)
        {
            if (AddBranchToParentInChildren(branch, branchToMove, parentBranchName))
            {
                return;
            }
        }

        throw new InvalidOperationException($"Parent branch '{parentBranchName}' not found in stack.");
    }

    private static bool AddBranchToParentInChildren(Branch currentBranch, Branch branchToMove, string parentBranchName)
    {
        if (currentBranch.Name.Equals(parentBranchName, StringComparison.OrdinalIgnoreCase))
        {
            currentBranch.Children.Add(branchToMove);
            return true;
        }

        foreach (var child in currentBranch.Children)
        {
            if (AddBranchToParentInChildren(child, branchToMove, parentBranchName))
            {
                return true;
            }
        }

        return false;
    }

    public List<List<Branch>> GetAllBranchLines()
    {
        var allLines = new List<List<Branch>>();
        foreach (var branch in Branches)
        {
            allLines.AddRange(branch.GetAllPaths());
        }
        return allLines;
    }

    public Stack ChangeName(string newName)
    {
        return this with { Name = newName };
    }

    public Branch? FindBranch(string branchName)
    {
        foreach (var branch in Branches)
        {
            if (branch.Name.Equals(branchName, StringComparison.OrdinalIgnoreCase))
            {
                return branch;
            }

            var found = FindBranchRecursive(branch, branchName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    static Branch? FindBranchRecursive(Branch branch, string branchName)
    {
        foreach (var child in branch.Children)
        {
            if (child.Name.Equals(branchName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            var found = FindBranchRecursive(child, branchName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}

public record Branch(string Name, List<Branch> Children)
{
    public List<string> AllBranchNames
    {
        get
        {
            var branches = new List<string> { Name };
            foreach (var child in Children)
            {
                branches.AddRange(child.AllBranchNames);
            }
            return [.. branches.Distinct()];
        }
    }

    public List<List<Branch>> GetAllPaths()
    {
        var result = new List<List<Branch>>();
        if (Children.Count == 0)
        {
            result.Add([this]);
        }
        else
        {
            foreach (var child in Children)
            {
                foreach (var path in child.GetAllPaths())
                {
                    var newPath = new List<Branch> { this };
                    newPath.AddRange(path);
                    result.Add(newPath);
                }
            }
        }
        return result;
    }
}

public enum RemoveBranchChildAction
{
    [Description("Move children branches to parent branch")]
    MoveChildrenToParent,

    [Description("Remove children branches")]
    RemoveChildren
}