using System.Text.Json;

namespace Stack.Config;

internal record Stack(string Name, string RemoteUri, string SourceBranch, List<string> Branches);

internal static class StackExtensionMethods
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

internal static class StackConfig
{
    public static string GetConfigPath()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(homeDirectory, "stack", "config.json");
    }

    public static List<Stack> Load()
    {
        var stacksFile = GetConfigPath();
        if (!File.Exists(stacksFile))
        {
            return new List<Stack>();
        }
        var jsonString = File.ReadAllText(stacksFile);
        return JsonSerializer.Deserialize<List<Stack>>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    public static void Save(List<Stack> stacks)
    {
        var stacksFile = GetConfigPath();
        File.WriteAllText(stacksFile, JsonSerializer.Serialize(stacks, new JsonSerializerOptions { WriteIndented = true }));
    }
}
