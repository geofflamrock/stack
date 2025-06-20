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

public class FileStackConfig(string? configDirectory = null) : IStackConfig
{
    readonly JsonSerializerOptions serializerOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
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
            return new StackData(SchemaVersion.V1, []);
        }
        var jsonString = File.ReadAllText(stacksFile);

        if (IsStackConfigInV2Format(jsonString))
        {
            return new StackData(SchemaVersion.V2, LoadStacksFromV2Format(jsonString));
        }

        // If no schema version, this means v1 format, which we need to convert to v2.
        return new StackData(SchemaVersion.V1, LoadStacksFromV1Format(jsonString));
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
            File.WriteAllText(stacksFile, JsonSerializer.Serialize(stackData.Stacks.Select(MapToV1Format).ToList(), serializerOptions));
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

        File.WriteAllText(stacksFile, JsonSerializer.Serialize(new StackConfigV2([.. stackData.Stacks.Select(MapToV2Format)]), serializerOptions));
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
            var stackConfig = JsonSerializer.Deserialize<StackConfigV1OrV2>(jsonString, serializerOptions);
            return stackConfig?.SchemaVersion == SchemaVersions.V2;
        }
        catch (JsonException)
        {
            return false; // If deserialization fails, it's not in v2 format.
        }
    }

    private List<Stack> LoadStacksFromV2Format(string jsonString)
    {
        var stacksV2 = JsonSerializer.Deserialize<StackConfigV2>(jsonString, serializerOptions);

        if (stacksV2 is null)
        {
            return [];
        }
        return [.. stacksV2.Stacks.Select(MapFromV2Format)];
    }

    private static StackV2 MapToV2Format(Stack stack)
    {
        var branchesV2 = stack.Branches.Select(MapToV2Format).ToList();
        return new StackV2(stack.Name, stack.RemoteUri, stack.SourceBranch, branchesV2, stack.PullRequestDescription);
    }

    private static StackV2Branch MapToV2Format(Branch branch)
    {
        return new StackV2Branch(branch.Name, [.. branch.Children.Select(MapToV2Format)]);
    }

    private List<Stack> LoadStacksFromV1Format(string jsonString)
    {
        var stacksV1 = JsonSerializer.Deserialize<List<StackV1>>(jsonString, serializerOptions);
        if (stacksV1 == null)
        {
            return [];
        }

        return [.. stacksV1.Select(MapFromV1Format)];
    }

    private static Stack MapFromV2Format(StackV2 stackV2)
    {
        var branches = stackV2.Branches.Select(b => new Branch(b.Name, [.. b.Children.Select(MapFromV2Format)])).ToList();
        var stack = new Stack(stackV2.Name, stackV2.RemoteUri, stackV2.SourceBranch, branches);

        if (stackV2.PullRequestDescription is not null)
        {
            stack.SetPullRequestDescription(stackV2.PullRequestDescription);
        }

        return stack;
    }

    private static Branch MapFromV2Format(StackV2Branch branchV2)
    {
        return new Branch(branchV2.Name, [.. branchV2.Children.Select(MapFromV2Format)]);
    }

    private static StackV1 MapToV1Format(Stack stack)
    {
        return new StackV1(stack.Name, stack.RemoteUri, stack.SourceBranch, stack.AllBranchNames, stack.PullRequestDescription);
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

        var stack = new Stack(stackV1.Name, stackV1.RemoteUri, stackV1.SourceBranch, childBranches);
        if (stackV1.PullRequestDescription is not null)
        {
            stack.SetPullRequestDescription(stackV1.PullRequestDescription);
        }
        return stack;
    }
}

public record StackV1(string Name, string RemoteUri, string SourceBranch, List<string> Branches, string? PullRequestDescription);
public record StackV2(string Name, string RemoteUri, string SourceBranch, List<StackV2Branch> Branches, string? PullRequestDescription);
public record StackV2Branch(string Name, List<StackV2Branch> Children);

public record StackConfigV2(List<StackV2> Stacks)
{
    [JsonInclude]
    [JsonPropertyOrder(0)]
    public string SchemaVersion => SchemaVersions.V2;

    [JsonPropertyOrder(1)]
    public List<StackV2> Stacks { get; private set; } = Stacks;
}

public static class SchemaVersions
{
    public const string V2 = "v2";
}

public enum SchemaVersion
{
    V1,
    V2
}

public record StackConfigV1OrV2(string? SchemaVersion);