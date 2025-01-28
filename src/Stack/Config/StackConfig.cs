using System.Text.Json;
using System.Text.Json.Serialization;
using Humanizer;

namespace Stack.Config;

public static class StackConstants
{
    public const string StackMarkerText = "stack-pr-list";
    public const string StackMarkerStart = $"<!-- {StackMarkerText} -->";
    public const string StackMarkerEnd = $"<!-- /{StackMarkerText} -->";
    public const string StackMarkerDescription = $"<!-- The contents of the section between the {StackConstants.StackMarkerText} markers will be replaced with the list of pull requests in the stack. Move this section around as you would like or delete it to not include the list of pull requests.  -->";
}

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
        this.PullRequestDescription = description;
    }

    public string GetDefaultBranchName()
    {
        return $"{Name.Kebaberize()}-{Branches.Count + 1}";
    }
}

public static class StackExtensionMethods
{
    public static bool IsCurrentStack(this Stack stack, string currentBranch)
    {
        return stack.Branches.Contains(currentBranch);
    }

    public static IOrderedEnumerable<Stack> OrderByCurrentStackThenByName(this List<Stack> stacks, string currentBranch)
    {
        return stacks.OrderBy(s => s.IsCurrentStack(currentBranch) ? 0 : 1).ThenBy(s => s.Name);
    }
}

public interface IStackConfig
{
    string GetConfigPath();
    List<Stack> Load();
    void Save(List<Stack> stacks);
}

public class StackConfig : IStackConfig
{
    public string GetConfigPath()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(homeDirectory, "stack", "config.json");
    }

    public List<Stack> Load()
    {
        var stacksFile = GetConfigPath();
        if (!File.Exists(stacksFile))
        {
            return new List<Stack>();
        }
        var jsonString = File.ReadAllText(stacksFile);
        return JsonSerializer.Deserialize<List<Stack>>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    public void Save(List<Stack> stacks)
    {
        var stacksFile = GetConfigPath();
        File.WriteAllText(stacksFile, JsonSerializer.Serialize(stacks, new JsonSerializerOptions { WriteIndented = true }));
    }
}
