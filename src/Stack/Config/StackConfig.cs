using System.Text.Json;

namespace Stack.Config;

internal record Stack(string Name, string RemoteUri, string SourceBranch, List<string> Branches);

internal static class StackConfig
{
    public static string GetConfigFileLocation()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(homeDirectory, ".stacks.json");
    }

    public static List<Stack> Load()
    {
        var stacksFile = GetConfigFileLocation();
        if (!File.Exists(stacksFile))
        {
            return new List<Stack>();
        }
        var jsonString = File.ReadAllText(stacksFile);
        return JsonSerializer.Deserialize<List<Stack>>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    public static void Save(List<Stack> stacks)
    {
        var stacksFile = GetConfigFileLocation();
        File.WriteAllText(stacksFile, JsonSerializer.Serialize(stacks, new JsonSerializerOptions { WriteIndented = true }));
    }
}
