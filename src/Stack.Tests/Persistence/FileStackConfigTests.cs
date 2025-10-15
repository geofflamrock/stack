using System.Text.Json;
using FluentAssertions;
using Stack.Persistence;
using Stack.Tests.Helpers;

// Deliberately using a different namespace here to avoid needing to 
// use a fully-qualified name in all other tests.
namespace Stack.Tests;

public class FileStackConfigTests
{
    [Fact]
    public void Load_WhenConfigFileDoesNotExist_ReturnsEmptyList()
    {
        // Arrange
        using var tempDirectory = TemporaryDirectory.Create();

        var fileStackConfig = new FileStackConfig(tempDirectory.DirectoryPath);

        // Act
        var stackData = fileStackConfig.Load();

        // Assert
        stackData.Should().BeEquivalentTo(new StackData([]));
    }

    [Fact]
    public void Load_WhenConfigFileIsInV1Format_LoadsCorrectly_MigratesAndSavesFileInV2Format()
    {
        // Arrange
        using var tempDirectory = TemporaryDirectory.Create();

        var configPath = Path.Combine(tempDirectory.DirectoryPath, "stack", "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var stackName = Some.Name();
        var remoteUri = Some.HttpsUri().ToString();
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        // Create a V1 format config file directly as JSON
        var v1Json = $@"[
    {{
        ""Name"": ""{stackName}"",
        ""RemoteUri"": ""{remoteUri}"",
        ""SourceBranch"": ""{sourceBranch}"",
        ""Branches"": [""{branch1}"", ""{branch2}""]
    }}
]";
        File.WriteAllText(configPath, v1Json);

        var fileStackConfig = new FileStackConfig(tempDirectory.DirectoryPath);
        var expectedStack = new Model.Stack(
            stackName,
            remoteUri,
            sourceBranch,
            [
                new(branch1,
                    [
                        new(branch2, [])
                    ])
            ]);

        // Act
        var stackData = fileStackConfig.Load();

        // Assert
        stackData.Should().BeEquivalentTo(new StackData([expectedStack]));
        var savedJson = File.ReadAllText(configPath);
        var expectedJson = $@"{{
    ""SchemaVersion"": 2,
    ""Stacks"": [
        {{
            ""Name"": ""{stackName}"",
            ""RemoteUri"": ""{remoteUri}"",
            ""SourceBranch"": ""{sourceBranch}"",
            ""Branches"": [
                {{
                    ""Name"": ""{branch1}"",
                    ""Children"": [
                        {{
                            ""Name"": ""{branch2}"",
                            ""Children"": []
                        }}
                    ]
                }}
            ]
        }}
    ]
}}";

        // Normalize both JSON strings to remove whitespace differences
        var normalizedSavedJson = NormalizeJsonString(savedJson);
        var normalizedExpectedJson = NormalizeJsonString(expectedJson);

        normalizedSavedJson.Should().Be(normalizedExpectedJson);

        // Original backup should be in V1 format
        var backupPath = fileStackConfig.GetV1ConfigBackupFilePath();
        File.Exists(backupPath).Should().BeTrue();
        var backupJson = File.ReadAllText(backupPath);
        backupJson.Should().Be(v1Json);
    }

    [Fact]
    public void Load_WhenConfigFileIsInV2Format_LoadsCorrectly()
    {
        // Arrange
        using var tempDirectory = TemporaryDirectory.Create();

        var configPath = Path.Combine(tempDirectory.DirectoryPath, "stack", "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var stackName = Some.Name();
        var remoteUri = Some.HttpsUri().ToString();
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var branch3 = Some.BranchName();

        // Create a V2 format config file with multiple trees directly as JSON
        var v2Json = $@"{{
    ""SchemaVersion"": 2,
    ""Stacks"": [
        {{
            ""Name"": ""{stackName}"",
            ""RemoteUri"": ""{remoteUri}"",
            ""SourceBranch"": ""{sourceBranch}"",
            ""Branches"": [
                {{
                    ""Name"": ""{branch1}"",
                    ""Children"": [
                        {{
                            ""Name"": ""{branch2}"",
                            ""Children"": []
                        }}
                    ]
                }},
                {{
                    ""Name"": ""{branch3}"",
                    ""Children"": []
                }}
            ]
        }}
    ]
}}";
        File.WriteAllText(configPath, v2Json);

        var fileStackConfig = new FileStackConfig(tempDirectory.DirectoryPath);

        var expectedStack = new Model.Stack(
            stackName,
            remoteUri,
            sourceBranch,
            [
                new(branch1, [new(branch2, [])]),
                    new(branch3, [])
            ]);

        // Act
        var stackData = fileStackConfig.Load();

        // Assert
        stackData.Should().BeEquivalentTo(new StackData([expectedStack]));
    }

    [Fact]
    public void Save_WhenConfigFileIsInV1Format_AndStackIsChangedToHaveTreeStructure_SavingInV2FormatCreatesABackupFile()
    {
        // Arrange
        using var tempDirectory = TemporaryDirectory.Create();

        var configPath = Path.Combine(tempDirectory.DirectoryPath, "stack", "config.json");
        var stackName = Some.Name();
        var remoteUri = Some.HttpsUri().ToString();
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var branch3 = Some.BranchName();

        // Create an empty config file in v1 format
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var v1Json = $@"[
    {{
        ""Name"": ""{stackName}"",
        ""RemoteUri"": ""{remoteUri}"",
        ""SourceBranch"": ""{sourceBranch}"",
        ""Branches"": [""{branch1}"", ""{branch2}""]
    }}
]";
        File.WriteAllText(configPath, v1Json);

        var stack = new Model.Stack(
            stackName,
            remoteUri,
            sourceBranch,
            [
                new(branch1, [new(branch2, [])]),
                new(branch3, [])
            ]);

        var fileStackConfig = new FileStackConfig(tempDirectory.DirectoryPath);

        // Act
        fileStackConfig.Save(new StackData([stack]));

        // Assert
        var savedJson = File.ReadAllText(configPath);
        var expectedJson = $@"{{
    ""SchemaVersion"": 2,
    ""Stacks"": [
        {{
            ""Name"": ""{stackName}"",
            ""RemoteUri"": ""{remoteUri}"",
            ""SourceBranch"": ""{sourceBranch}"",
            ""Branches"": [
                {{
                    ""Name"": ""{branch1}"",
                    ""Children"": [
                        {{
                            ""Name"": ""{branch2}"",
                            ""Children"": []
                        }}
                    ]
                }},
                {{
                    ""Name"": ""{branch3}"",
                    ""Children"": []
                }}
            ]
        }}
    ]
}}";

        // Normalize both JSON strings to remove whitespace differences
        var normalizedSavedJson = NormalizeJsonString(savedJson);
        var normalizedExpectedJson = NormalizeJsonString(expectedJson);

        normalizedSavedJson.Should().Be(normalizedExpectedJson);

        // Original backup should be in V1 format
        var backupPath = fileStackConfig.GetV1ConfigBackupFilePath();
        File.Exists(backupPath).Should().BeTrue();
        var backupJson = File.ReadAllText(backupPath);
        backupJson.Should().Be(v1Json);
    }

    private static string NormalizeJsonString(string json)
    {
        // Parse and reserialize to normalize formatting
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);
        return JsonSerializer.Serialize(jsonElement, new JsonSerializerOptions { WriteIndented = false });
    }
}
