using Stack.Commands;
using System.Text.RegularExpressions;

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

    public List<string> AllBranchNames => [.. GetAllBranches().Select(b => b.Name).Distinct()];

    public bool HasSingleTree => GetAllBranchLines().Count == 1;

    public string GetDefaultBranchName()
    {
        var fullBranchNames = Branches.SelectMany(b => b.AllBranchNames).Distinct().ToList();
        return $"{SanitizeBranchName(Name)}-{fullBranchNames.Count + 1}";
    }

    static string SanitizeBranchName(string name)
    {
        // Replace invalid characters (including spaces and special chars) with '-'
        var sanitized = Regex.Replace(name, "[\\s~^:?*\\[\\\\@{{}}]+|[" + Regex.Escape(new string(Path.GetInvalidFileNameChars())) + "]", "-");

        // Remove consecutive dashes
        sanitized = Regex.Replace(sanitized, "-+", "-");

        // Trim leading/trailing dashes and dots
        sanitized = sanitized.Trim('-', '.');

        // Git branch names cannot start with a slash, dot, or end with a dot or slash
        sanitized = sanitized.TrimStart('/', '.').TrimEnd('/', '.');

        // Avoid reserved names
        if (sanitized.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
            sanitized = "branch-" + sanitized;

        return sanitized.ToLower();
    }

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

    public List<List<Branch>> GetAllBranchLines()
    {
        var allLines = new List<List<Branch>>();
        foreach (var branch in Branches)
        {
            allLines.AddRange(branch.GetAllPaths());
        }
        return allLines;
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