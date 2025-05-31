using System.Text.Json.Serialization;
using Humanizer;

namespace Stack.Config;

public record Stack(string Name, string RemoteUri, string SourceBranch, List<Branch> Branches)
{
    public string? PullRequestDescription { get; private set; }

    public void SetPullRequestDescription(string description)
    {
        this.PullRequestDescription = description;
    }

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

    public List<string> AllBranchNames
    {
        get
        {
            var branches = new List<string>();
            foreach (var branch in Branches)
            {
                branches.AddRange(branch.AllBranchNames);
            }
            return [.. branches.Distinct()];
        }
    }

    public bool HasSingleTree
    {
        get
        {
            if (Branches.Count == 0)
            {
                return true;
            }

            if (Branches.Count > 1)
            {
                return false;
            }

            return Branches.First().HasSingleTree;
        }
    }

    public string GetDefaultBranchName()
    {
        var fullBranchNames = Branches.SelectMany(b => b.AllBranchNames).Distinct().ToList();
        return $"{Name.Kebaberize()}-{fullBranchNames.Count + 1}";
    }

    public void RemoveBranch(string branchName)
    {
        if (Branches.Count == 0)
        {
            return;
        }

        foreach (var branch in Branches)
        {
            if (branch.Name.Equals(branchName, StringComparison.OrdinalIgnoreCase))
            {
                var childrenToMove = branch.Children;
                Branches.Remove(branch);
                branch.Children.AddRange(childrenToMove);
                return;
            }

            if (RemoveBranch(branch, branchName))
            {
                return;
            }
        }
    }

    static bool RemoveBranch(Branch branch, string branchName)
    {
        var childBranch = branch.Children.FirstOrDefault(c => c.Name.Equals(branchName, StringComparison.OrdinalIgnoreCase));
        if (childBranch != null)
        {
            // Get all the children of the branch to be removed,
            // and add them to the parent of the branch being removed.
            var childrenToMove = childBranch.Children;
            branch.Children.Remove(childBranch);
            branch.Children.AddRange(childrenToMove);
            return true;
        }

        foreach (var child in branch.Children)
        {
            if (RemoveBranch(child, branchName))
            {
                return true;
            }
        }

        return false;
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

    public bool HasSingleTree
    {
        get
        {
            if (Children.Count == 0)
            {
                return true;
            }

            if (Children.Count > 1)
            {
                return false;
            }

            return Children.First().HasSingleTree;
        }
    }
}