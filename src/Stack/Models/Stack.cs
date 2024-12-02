using System.Text.Json.Serialization;
using Humanizer;

namespace Stack.Models;

public class Stack(string Name, string RemoteUri, string SourceBranch, List<string> Branches)
{
    public string Name { get; private set; } = Name;
    public string RemoteUri { get; private set; } = RemoteUri;
    public string SourceBranch { get; private set; } = SourceBranch;
    public List<string> Branches { get; private set; } = Branches;

    [JsonInclude]
    public string? PullRequestDescription { get; private set; }

    public void SetPullRequestDescription(string description)
    {
        PullRequestDescription = description;
    }

    public string GetDefaultBranchName()
    {
        return $"{Name.Kebaberize()}-{Branches.Count + 1}";
    }
}
