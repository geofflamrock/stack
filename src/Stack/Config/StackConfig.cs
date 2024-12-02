using System.Text.Json;

namespace Stack.Config;

public static class StackExtensionMethods
{
    public static bool IsCurrentStack(this Models.Stack stack, string currentBranch)
    {
        return stack.Branches.Contains(currentBranch);
    }

    public static IOrderedEnumerable<Models.Stack> OrderByCurrentStackThenByName(this List<Models.Stack> stacks, string currentBranch)
    {
        return stacks.OrderBy(s => s.IsCurrentStack(currentBranch) ? 0 : 1).ThenBy(s => s.Name);
    }
}

public interface IStackConfig
{
    string GetConfigPath();
    List<Models.Stack> Load();
    void Save(List<Models.Stack> stacks);
}

public class StackConfig : IStackConfig
{
    public string GetConfigPath()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(homeDirectory, "stack", "config.json");
    }

    public List<Models.Stack> Load()
    {
        var stacksFile = GetConfigPath();
        if (!File.Exists(stacksFile))
        {
            return new List<Models.Stack>();
        }
        var jsonString = File.ReadAllText(stacksFile);
        return JsonSerializer.Deserialize<List<Models.Stack>>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    public void Save(List<Models.Stack> stacks)
    {
        var stacksFile = GetConfigPath();
        File.WriteAllText(stacksFile, JsonSerializer.Serialize(stacks, new JsonSerializerOptions { WriteIndented = true }));
    }
}
