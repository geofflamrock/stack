using Stack.Commands;
using System.Text.RegularExpressions;

namespace Stack.Config;

public record Stack(string Name, string RemoteUri, string SourceBranch, List<Branch> Branches)
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

    public void MoveBranch(string branchName, string newParentBranchName, MoveBranchChildrenAction action)
    {
        if (string.IsNullOrWhiteSpace(branchName))
            throw new ArgumentException("Branch name is required.", nameof(branchName));
        if (string.IsNullOrWhiteSpace(newParentBranchName))
            throw new ArgumentException("New parent branch name is required.", nameof(newParentBranchName));

        // Cannot move the source branch
        if (branchName.Equals(SourceBranch, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot move the source branch.");

        // Find the branch to move and its current parent (null = root list)
        var (currentParent, branchToMove) = FindBranchWithParent(branchName)
            ?? throw new InvalidOperationException($"Branch '{branchName}' not found in stack '{Name}'.");

        // Resolve the new parent; SourceBranch means move to root
        var moveToRoot = newParentBranchName.Equals(SourceBranch, StringComparison.OrdinalIgnoreCase);
        Branch? newParent = null;
        if (!moveToRoot)
        {
            newParent = GetAllBranches().FirstOrDefault(b => b.Name.Equals(newParentBranchName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Branch '{newParentBranchName}' not found in stack '{Name}'.");
        }

        // No-op / invalid if the branch is already under the same parent
        var isAlreadyUnderSameParent = (currentParent == null && moveToRoot) ||
                                       (currentParent != null && !moveToRoot && currentParent.Name.Equals(newParent!.Name, StringComparison.OrdinalIgnoreCase));
        if (isAlreadyUnderSameParent)
            throw new InvalidOperationException($"Branch '{branchName}' is already under the selected parent '{newParentBranchName}'.");

        // Prevent moving a branch under itself or any of its descendants
        if (!moveToRoot)
        {
            if (newParent!.Name.Equals(branchToMove.Name, StringComparison.OrdinalIgnoreCase) ||
                IsDescendant(branchToMove, newParent!.Name))
            {
                throw new InvalidOperationException("Cannot move a branch under itself or one of its descendants.");
            }
        }

        // Detach from current parent
        if (currentParent is null)
        {
            // From root
            var existing = Branches.FirstOrDefault(b => b.Name.Equals(branchToMove.Name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                Branches.Remove(existing);
            }
        }
        else
        {
            currentParent.Children.Remove(branchToMove);
        }

        // If keeping children with old parent, reattach children to the old parent/root
        if (action == MoveBranchChildrenAction.KeepChildrenWithOldParent)
        {
            var childrenToKeep = branchToMove.Children.ToList();
            branchToMove.Children.Clear();
            if (currentParent is null)
            {
                Branches.AddRange(childrenToKeep);
            }
            else
            {
                currentParent.Children.AddRange(childrenToKeep);
            }
        }

        // Attach to new parent (or root)
        if (moveToRoot)
        {
            Branches.Add(branchToMove);
        }
        else
        {
            newParent!.Children.Add(branchToMove);
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

    public List<List<Branch>> GetAllBranchLines()
    {
        var allLines = new List<List<Branch>>();
        foreach (var branch in Branches)
        {
            allLines.AddRange(branch.GetAllPaths());
        }
        return allLines;
    }

    private (Branch? parent, Branch node)? FindBranchWithParent(string name)
    {
        // Check root first
        foreach (var root in Branches)
        {
            if (root.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return (null, root);

            var found = FindBranchWithParent(root, name);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static (Branch parent, Branch node)? FindBranchWithParent(Branch parent, string name)
    {
        foreach (var child in parent.Children)
        {
            if (child.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return (parent, child);

            var found = FindBranchWithParent(child, name);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static bool IsDescendant(Branch ancestor, string possibleDescendantName)
    {
        foreach (var child in ancestor.Children)
        {
            if (child.Name.Equals(possibleDescendantName, StringComparison.OrdinalIgnoreCase))
                return true;
            if (IsDescendant(child, possibleDescendantName))
                return true;
        }
        return false;
    }
}

public enum MoveBranchChildrenAction
{
    KeepChildrenWithOldParent,
    MoveChildrenWithBranch
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