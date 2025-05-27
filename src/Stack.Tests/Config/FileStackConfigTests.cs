using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Stack.Config;
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
        stackData.Should().BeEquivalentTo(new StackData(SchemaVersion.V1, []));
    }

    [Fact]
    public void Load_WhenConfigFileIsInV1Format_LoadsCorrectly()
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
        var prDescription = "Test PR description";

        // Create a V1 format config file directly as JSON
        var v1Json = $@"[
    {{
        ""Name"": ""{stackName}"",
        ""RemoteUri"": ""{remoteUri}"",
        ""SourceBranch"": ""{sourceBranch}"",
        ""Branches"": [""{branch1}"", ""{branch2}""],
        ""PullRequestDescription"": ""{prDescription}""
    }}
]";
        File.WriteAllText(configPath, v1Json);

        var fileStackConfig = new FileStackConfig(tempDirectory.DirectoryPath);
        var expectedStack = new Config.Stack(
            stackName,
            remoteUri,
            sourceBranch,
            [
                new(branch1,
                    [
                        new(branch2, [])
                    ])
            ]);
        expectedStack.SetPullRequestDescription(prDescription);

        // Act
        var stackData = fileStackConfig.Load();

        // Assert
        stackData.Should().BeEquivalentTo(new StackData(SchemaVersion.V1, [expectedStack]));
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
        var prDescription = "Test PR description";

        // Create a V2 format config file with multiple trees directly as JSON
        var v2Json = $@"{{
    ""SchemaVersion"": ""v2"",
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
            ],
            ""PullRequestDescription"": ""{prDescription}""
        }}
    ]
}}";
        File.WriteAllText(configPath, v2Json);

        var fileStackConfig = new FileStackConfig(tempDirectory.DirectoryPath);

        var expectedStack = new Config.Stack(
            stackName,
            remoteUri,
            sourceBranch,
            [
                new(branch1, [new(branch2, [])]),
                    new(branch3, [])
            ]);
        expectedStack.SetPullRequestDescription(prDescription);

        // Act
        var stackData = fileStackConfig.Load();

        // Assert
        stackData.Should().BeEquivalentTo(new StackData(SchemaVersion.V2, [expectedStack]));
    }

    [Fact]
    public void Save_WhenConfigFileIsInV1Format_AndAllStacksHaveASingleTree_SavesCorrectlyInV1Format()
    {
        // Arrange
        using var tempDirectory = TemporaryDirectory.Create();
        var configPath = Path.Combine(tempDirectory.DirectoryPath, "stack", "config.json");
        var stackName = Some.Name();
        var remoteUri = Some.HttpsUri().ToString();
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var prDescription = "Test PR description";

        // Create an empty config file
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, "[]");

        // Create a Stack object with a simple tree structure that should be saved in V1 format
        var stack = new Config.Stack(
            stackName,
            remoteUri,
            sourceBranch,
            [
                new(branch1,
                    [
                        new(branch2, [])
                    ])
            ]);
        stack.SetPullRequestDescription(prDescription);

        // Act
        var fileStackConfig = new FileStackConfig(tempDirectory.DirectoryPath);
        fileStackConfig.Save(new StackData(SchemaVersion.V1, [stack]));

        // Assert
        var savedJson = File.ReadAllText(configPath);

        // Format the expected JSON string with proper indentation
        var expectedJson = $@"[
    {{
        ""Name"": ""{stackName}"",
        ""RemoteUri"": ""{remoteUri}"",
        ""SourceBranch"": ""{sourceBranch}"",
        ""Branches"": [
            ""{branch1}"",
            ""{branch2}""
        ],
        ""PullRequestDescription"": ""{prDescription}""
    }}
]";

        // Normalize both JSON strings to remove whitespace differences
        var normalizedSavedJson = NormalizeJsonString(savedJson);
        var normalizedExpectedJson = NormalizeJsonString(expectedJson);

        normalizedSavedJson.Should().Be(normalizedExpectedJson);
    }

    [Fact]
    public void Save_WhenConfigFileIsInV1Format_AndSomeStacksHaveMultipleBranchTrees_SavesInV2FormatAndCreatesABackupFile()
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
        var prDescription = "Test PR description";

        // Create an empty config file in v1 format
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var v1Json = $@"[
    {{
        ""Name"": ""{stackName}"",
        ""RemoteUri"": ""{remoteUri}"",
        ""SourceBranch"": ""{sourceBranch}"",
        ""Branches"": [""{branch1}"", ""{branch2}""],
        ""PullRequestDescription"": ""{prDescription}""
    }}
]";
        File.WriteAllText(configPath, v1Json);

        var stack = new Config.Stack(
            stackName,
            remoteUri,
            sourceBranch,
            [
                new(branch1, [new(branch2, [])]),
                    new(branch3, [])
            ]);
        stack.SetPullRequestDescription(prDescription);

        var fileStackConfig = new FileStackConfig(tempDirectory.DirectoryPath);

        // Act
        fileStackConfig.Save(new StackData(SchemaVersion.V2, [stack]));

        // Assert
        var savedJson = File.ReadAllText(configPath);
        var expectedJson = $@"{{
    ""SchemaVersion"": ""v2"",
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
            ],
            ""PullRequestDescription"": ""{prDescription}""
        }}
    ]
}}";

        // Normalize both JSON strings to remove whitespace differences
        var normalizedSavedJson = NormalizeJsonString(savedJson);
        var normalizedExpectedJson = NormalizeJsonString(expectedJson);

        normalizedSavedJson.Should().Be(normalizedExpectedJson);

        // Original backup should be in V1 format
        File.Exists(configPath + ".bak").Should().BeTrue();
        var backupJson = File.ReadAllText(configPath + ".bak");
        backupJson.Should().Be(v1Json);
    }

    private static string NormalizeJsonString(string json)
    {
        // Parse and reserialize to normalize formatting
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);
        return JsonSerializer.Serialize(jsonElement, new JsonSerializerOptions { WriteIndented = false });
    }
}
