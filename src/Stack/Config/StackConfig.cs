using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stack.Config;

public record StackData(SchemaVersion SchemaVersion, List<Stack> Stacks);

public interface IStackConfig
{
    string GetConfigPath();
    StackData Load();
    void Save(StackData stackData);
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true)]
[JsonSerializable(typeof(StackConfigSchemaVersion))]
[JsonSerializable(typeof(StackConfigV2))]
[JsonSerializable(typeof(List<StackV1>))]
internal partial class StackConfigJsonSerializerContext : JsonSerializerContext
{
}

public class FileStackConfig(string? configDirectory = null) : IStackConfig
{
    readonly string? configDirectory = configDirectory;

    public string GetConfigPath()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(configDirectory ?? homeDirectory, "stack", "config.json");
    }

    public StackData Load()
    {
        var stacksFile = GetConfigPath();
        if (!File.Exists(stacksFile))
        {
            return new StackData(SchemaVersion.V2, []);
        }
        var jsonString = File.ReadAllText(stacksFile);

        if (IsStackConfigInV2Format(jsonString))
        {
            return new StackData(SchemaVersion.V2, LoadStacksFromV2Format(jsonString));
        }

        // If no schema version, this means v1 format - migrate to v2 format and re-save before returning
        var stacksV1 = LoadStacksFromV1Format(jsonString);
        var stacks = new StackData(SchemaVersion.V2, stacksV1);
        Save(stacks);
        return stacks;
    }

    public void Save(StackData stackData)
    {
        var stacksFile = GetConfigPath();
        if (!Directory.Exists(Path.GetDirectoryName(stacksFile)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(stacksFile)!);
        }

        if (stackData.SchemaVersion == SchemaVersion.V1)
        {
            if (stackData.Stacks.Any(s => !s.HasSingleTree))
            {
                throw new InvalidOperationException("Cannot save in v1 format if any stack has multiple trees.");
            }

            // If all stacks have a single tree and we are still in v1 format continue to preference this.
            File.WriteAllText(stacksFile, JsonSerializer.Serialize(stackData.Stacks.Select(MapToV1Format).ToList(), StackConfigJsonSerializerContext.Default.ListStackV1));
            return;
        }

        // Else we are writing in v2 format
        var existingConfigFileIsInV1Format = false;

        if (File.Exists(stacksFile))
        {
            existingConfigFileIsInV1Format = !IsStackConfigInV2Format(File.ReadAllText(stacksFile));
        }

        // If we are currently in v1 format take a backup.
        if (existingConfigFileIsInV1Format)
        {
            var backupFile = GetV1ConfigBackupFilePath();
            if (File.Exists(backupFile))
            {
                File.Delete(backupFile);
            }
            File.Move(stacksFile, backupFile);
        }

        File.WriteAllText(stacksFile, JsonSerializer.Serialize(new StackConfigV2([.. stackData.Stacks.Select(MapToV2Format)]), StackConfigJsonSerializerContext.Default.StackConfigV2));
    }

    public string GetV1ConfigBackupFilePath()
    {
        var stacksFile = GetConfigPath();
        return Path.Combine(Path.GetDirectoryName(stacksFile)!, "config.v1-backup.json");
    }

    private bool IsStackConfigInV2Format(string jsonString)
    {
        try
        {
            var stackConfig = JsonSerializer.Deserialize(jsonString, StackConfigJsonSerializerContext.Default.StackConfigSchemaVersion);
            return stackConfig?.SchemaVersion == SchemaVersions.V2;
        }
        catch (JsonException)
        {
            return false; // If deserialization fails, it's not in v2 format.
        }
    }

    private List<Stack> LoadStacksFromV2Format(string jsonString)
    {
        var stacksV2 = JsonSerializer.Deserialize(jsonString, StackConfigJsonSerializerContext.Default.StackConfigV2);

        if (stacksV2 is null)
        {
            return [];
        }
        return [.. stacksV2.Stacks.Select(MapFromV2Format)];
    }

    private static StackV2 MapToV2Format(Stack stack)
    {
        var branchesV2 = stack.Branches.Select(MapToV2Format).ToList();
        return new StackV2(stack.Name, stack.RemoteUri, stack.SourceBranch, branchesV2);
    }

    private static StackV2Branch MapToV2Format(Branch branch)
    {
        return new StackV2Branch(branch.Name, [.. branch.Children.Select(MapToV2Format)]);
    }

    private List<Stack> LoadStacksFromV1Format(string jsonString)
    {
        var stacksV1 = JsonSerializer.Deserialize(jsonString, StackConfigJsonSerializerContext.Default.ListStackV1);
        if (stacksV1 == null)
        {
            return [];
        }

        return [.. stacksV1.Select(MapFromV1Format)];
    }

    private static Stack MapFromV2Format(StackV2 stackV2)
    {
        var branches = stackV2.Branches.Select(b => new Branch(b.Name, [.. b.Children.Select(MapFromV2Format)])).ToList();
        return new Stack(stackV2.Name, stackV2.RemoteUri, stackV2.SourceBranch, branches);
    }

    private static Branch MapFromV2Format(StackV2Branch branchV2)
    {
        return new Branch(branchV2.Name, [.. branchV2.Children.Select(MapFromV2Format)]);
    }

    private static StackV1 MapToV1Format(Stack stack)
    {
        return new StackV1(stack.Name, stack.RemoteUri, stack.SourceBranch, stack.AllBranchNames);
    }

    private static Stack MapFromV1Format(StackV1 stackV1)
    {
        // In v1, the branches are a flat list, but this actually represents a tree structure
        // where each branch is the child of the previous one.
        var childBranches = new List<Branch>();
        Branch? currentParent = null;
        foreach (var branch in stackV1.Branches)
        {
            var newBranch = new Branch(branch, []);
            if (currentParent == null)
            {
                childBranches.Add(newBranch);
            }
            else
            {
                currentParent.Children.Add(newBranch);
            }
            currentParent = newBranch;
        }

        return new Stack(stackV1.Name, stackV1.RemoteUri, stackV1.SourceBranch, childBranches);
    }
}

public record StackV1(string Name, string RemoteUri, string SourceBranch, List<string> Branches);
public record StackV2(string Name, string RemoteUri, string SourceBranch, List<StackV2Branch> Branches);
public record StackV2Branch(string Name, List<StackV2Branch> Children);

public record StackConfigV2(List<StackV2> Stacks)
{
    [JsonPropertyOrder(0)]
    public int SchemaVersion => SchemaVersions.V2;

    [JsonPropertyOrder(1)]
    public List<StackV2> Stacks { get; private set; } = Stacks;
}

public static class SchemaVersions
{
    public const int V2 = 2;
}

public enum SchemaVersion
{
    V1,
    V2
}

public record StackConfigSchemaVersion(int? SchemaVersion);