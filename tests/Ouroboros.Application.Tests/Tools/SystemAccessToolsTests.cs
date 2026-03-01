// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ouroboros.Application.Tools;
using Ouroboros.Application.Tools.SystemTools;
using Xunit;

namespace Ouroboros.Tests.Tools;

[Trait("Category", "Unit")]
public class SystemAccessToolsTests
{
    // ======================================================================
    // CreateAllTools
    // ======================================================================

    [Fact]
    public void CreateAllTools_ShouldReturnAllExpectedTools()
    {
        // Act
        var tools = SystemAccessTools.CreateAllTools().ToList();

        // Assert
        tools.Should().NotBeEmpty();
        var names = tools.Select(t => t.Name).ToList();
        names.Should().Contain("file_system");
        names.Should().Contain("list_directory");
        names.Should().Contain("read_file");
        names.Should().Contain("write_file");
        names.Should().Contain("search_files");
        names.Should().Contain("list_processes");
        names.Should().Contain("system_info");
        names.Should().Contain("environment");
        names.Should().Contain("shell");
    }

    // ======================================================================
    // FileSystemTool
    // ======================================================================

    [Fact]
    public async Task FileSystem_ExistsAction_ForExistingFile_ShouldReturnTrue()
    {
        // Arrange
        var tool = new FileSystemTool();
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            var result = await tool.InvokeAsync($$"""{"action":"exists","path":"{{tempFile.Replace("\\", "\\\\")}}"}""");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be("True");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task FileSystem_ExistsAction_ForNonExistentPath_ShouldReturnFalse()
    {
        // Arrange
        var tool = new FileSystemTool();

        // Act
        var result = await tool.InvokeAsync("""{"action":"exists","path":"C:\\does_not_exist_xyz_123.tmp"}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("False");
    }

    [Fact]
    public async Task FileSystem_InfoAction_ForFile_ShouldReturnFileInfo()
    {
        // Arrange
        var tool = new FileSystemTool();
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "test content");

        try
        {
            // Act
            var result = await tool.InvokeAsync($$"""{"action":"info","path":"{{tempFile.Replace("\\", "\\\\")}}"}""");

            // Assert
            result.IsSuccess.Should().BeTrue();
            var json = JsonDocument.Parse(result.Value);
            json.RootElement.GetProperty("type").GetString().Should().Be("file");
            json.RootElement.GetProperty("size").GetInt64().Should().BeGreaterThan(0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task FileSystem_InfoAction_ForDirectory_ShouldReturnDirectoryInfo()
    {
        // Arrange
        var tool = new FileSystemTool();
        var tempDir = Path.Combine(Path.GetTempPath(), $"ouro_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            var result = await tool.InvokeAsync($$"""{"action":"info","path":"{{tempDir.Replace("\\", "\\\\")}}"}""");

            // Assert
            result.IsSuccess.Should().BeTrue();
            var json = JsonDocument.Parse(result.Value);
            json.RootElement.GetProperty("type").GetString().Should().Be("directory");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FileSystem_InfoAction_ForNonExistent_ShouldReturnFailure()
    {
        // Arrange
        var tool = new FileSystemTool();

        // Act
        var result = await tool.InvokeAsync("""{"action":"info","path":"C:\\nonexistent_xyz_1234567"}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("not found");
    }

    [Fact]
    public async Task FileSystem_SizeAction_ForFile_ShouldReturnSize()
    {
        // Arrange
        var tool = new FileSystemTool();
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "hello world");

        try
        {
            // Act
            var result = await tool.InvokeAsync($$"""{"action":"size","path":"{{tempFile.Replace("\\", "\\\\")}}"}""");

            // Assert
            result.IsSuccess.Should().BeTrue();
            long.Parse(result.Value).Should().BeGreaterThan(0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task FileSystem_ModifiedAction_ForFile_ShouldReturnIsoTimestamp()
    {
        // Arrange
        var tool = new FileSystemTool();
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            var result = await tool.InvokeAsync($$"""{"action":"modified","path":"{{tempFile.Replace("\\", "\\\\")}}"}""");

            // Assert
            result.IsSuccess.Should().BeTrue();
            // Should be ISO 8601 parseable
            DateTime.TryParse(result.Value, out var dt).Should().BeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task FileSystem_UnknownAction_ShouldReturnFailure()
    {
        // Arrange
        var tool = new FileSystemTool();

        // Act
        var result = await tool.InvokeAsync("""{"action":"delete","path":"C:\\temp"}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("Unknown action");
    }

    [Fact]
    public async Task FileSystem_MalformedJson_ShouldReturnFailure()
    {
        // Arrange
        var tool = new FileSystemTool();

        // Act
        var result = await tool.InvokeAsync("not json");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    // ======================================================================
    // DirectoryListTool
    // ======================================================================

    [Fact]
    public async Task DirectoryList_ForExistingDir_ShouldListContents()
    {
        // Arrange
        var tool = new DirectoryListTool();
        var tempDir = Path.Combine(Path.GetTempPath(), $"ouro_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "test.txt"), "content");
        Directory.CreateDirectory(Path.Combine(tempDir, "subdir"));

        try
        {
            // Act
            var result = await tool.InvokeAsync(tempDir);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain("[DIR]");
            result.Value.Should().Contain("[FILE]");
            result.Value.Should().Contain("test.txt");
            result.Value.Should().Contain("subdir");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DirectoryList_ForNonExistentDir_ShouldReturnFailure()
    {
        // Arrange
        var tool = new DirectoryListTool();

        // Act
        var result = await tool.InvokeAsync("C:\\nonexistent_dir_xyz_9999");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("not found");
    }

    [Fact]
    public async Task DirectoryList_WithEmptyInput_ShouldUseCurrentDirectory()
    {
        // Arrange
        var tool = new DirectoryListTool();

        // Act
        var result = await tool.InvokeAsync("   ");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Total:");
    }

    // ======================================================================
    // FileReadTool
    // ======================================================================

    [Fact]
    public async Task FileRead_WithPlainPathInput_ShouldReadFile()
    {
        // Arrange
        var tool = new FileReadTool();
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "line1\nline2\nline3");

        try
        {
            // Act
            var result = await tool.InvokeAsync(tempFile);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain("line1");
            result.Value.Should().Contain("line3");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task FileRead_WithJsonInput_ShouldParsePathAndLines()
    {
        // Arrange
        var tool = new FileReadTool();
        var tempFile = Path.GetTempFileName();
        var lines = Enumerable.Range(0, 100).Select(i => $"Line {i}");
        await File.WriteAllLinesAsync(tempFile, lines);

        try
        {
            // Act
            var result = await tool.InvokeAsync($$"""{"path":"{{tempFile.Replace("\\", "\\\\")}}","lines":10}""");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain("Line 0");
            result.Value.Should().Contain("truncated");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task FileRead_ForNonExistentFile_ShouldReturnFailure()
    {
        // Arrange
        var tool = new FileReadTool();

        // Act
        var result = await tool.InvokeAsync("/nonexistent_file_xyz.txt");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("not found");
    }

    // ======================================================================
    // FileWriteTool
    // ======================================================================

    [Fact]
    public async Task FileWrite_ShouldCreateNewFile()
    {
        // Arrange
        var tool = new FileWriteTool();
        var tempPath = Path.Combine(Path.GetTempPath(), $"ouro_write_{Guid.NewGuid():N}.txt");

        try
        {
            // Act
            var result = await tool.InvokeAsync($$"""{"path":"{{tempPath.Replace("\\", "\\\\")}}","content":"hello world"}""");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain("Written");
            File.Exists(tempPath).Should().BeTrue();
            (await File.ReadAllTextAsync(tempPath)).Should().Be("hello world");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task FileWrite_WithAppendTrue_ShouldAppendToFile()
    {
        // Arrange
        var tool = new FileWriteTool();
        var tempPath = Path.Combine(Path.GetTempPath(), $"ouro_append_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempPath, "original");

        try
        {
            // Act
            var result = await tool.InvokeAsync($$"""{"path":"{{tempPath.Replace("\\", "\\\\")}}","content":" appended","append":true}""");

            // Assert
            result.IsSuccess.Should().BeTrue();
            (await File.ReadAllTextAsync(tempPath)).Should().Be("original appended");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task FileWrite_ShouldCreateDirectoryIfNeeded()
    {
        // Arrange
        var tool = new FileWriteTool();
        var tempDir = Path.Combine(Path.GetTempPath(), $"ouro_new_dir_{Guid.NewGuid():N}");
        var tempPath = Path.Combine(tempDir, "file.txt");

        try
        {
            // Act
            var result = await tool.InvokeAsync($$"""{"path":"{{tempPath.Replace("\\", "\\\\")}}","content":"nested"}""");

            // Assert
            result.IsSuccess.Should().BeTrue();
            Directory.Exists(tempDir).Should().BeTrue();
            File.Exists(tempPath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // ======================================================================
    // FileSearchTool
    // ======================================================================

    [Fact]
    public async Task FileSearch_WithPattern_ShouldFindMatchingFiles()
    {
        // Arrange
        var tool = new FileSearchTool();
        var tempDir = Path.Combine(Path.GetTempPath(), $"ouro_search_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "test.cs"), "class Foo {}");
        File.WriteAllText(Path.Combine(tempDir, "test.txt"), "not code");

        try
        {
            // Act
            var result = await tool.InvokeAsync($$"""{"path":"{{tempDir.Replace("\\", "\\\\")}}","pattern":"*.cs","recursive":false}""");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain("test.cs");
            result.Value.Should().NotContain("test.txt");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FileSearch_WithContainsFilter_ShouldFindFilesWithContent()
    {
        // Arrange
        var tool = new FileSearchTool();
        var tempDir = Path.Combine(Path.GetTempPath(), $"ouro_search2_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "match.txt"), "This file has the keyword OUROBOROS in it");
        File.WriteAllText(Path.Combine(tempDir, "nomatch.txt"), "This file does not");

        try
        {
            // Act
            var result = await tool.InvokeAsync($$"""{"path":"{{tempDir.Replace("\\", "\\\\")}}","pattern":"*","recursive":false,"contains":"OUROBOROS"}""");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain("match.txt");
            result.Value.Should().NotContain("nomatch.txt");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // ======================================================================
    // SystemInfoTool
    // ======================================================================

    [Fact]
    public async Task SystemInfo_ShouldReturnMachineInformation()
    {
        // Arrange
        var tool = new SystemInfoTool();

        // Act
        var result = await tool.InvokeAsync("");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Computer Name");
        result.Value.Should().Contain("OS:");
        result.Value.Should().Contain("Processors:");
        result.Value.Should().Contain(".NET Version:");
    }

    // ======================================================================
    // EnvironmentTool
    // ======================================================================

    [Fact]
    public async Task Environment_ListAction_ShouldReturnVariables()
    {
        // Arrange
        var tool = new EnvironmentTool();

        // Act
        var result = await tool.InvokeAsync("list");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("="); // Key=Value pairs
    }

    [Fact]
    public async Task Environment_GetAction_ShouldReturnVariable()
    {
        // Arrange
        var tool = new EnvironmentTool();
        Environment.SetEnvironmentVariable("OURO_TEST_VAR", "test_value_123");

        try
        {
            // Act
            var result = await tool.InvokeAsync("""{"action":"get","name":"OURO_TEST_VAR"}""");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be("test_value_123");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OURO_TEST_VAR", null);
        }
    }

    [Fact]
    public async Task Environment_GetAction_ForUnsetVar_ShouldReturnNotSet()
    {
        // Arrange
        var tool = new EnvironmentTool();

        // Act
        var result = await tool.InvokeAsync("""{"action":"get","name":"OURO_NONEXISTENT_VAR_XYZ"}""");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("not set");
    }

    [Fact]
    public async Task Environment_UnknownAction_ShouldReturnFailure()
    {
        // Arrange
        var tool = new EnvironmentTool();

        // Act
        var result = await tool.InvokeAsync("""{"action":"delete","name":"PATH"}""");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("Unknown action");
    }

    [Fact]
    public async Task Environment_WithEmptyInput_ShouldListAll()
    {
        // Arrange
        var tool = new EnvironmentTool();

        // Act
        var result = await tool.InvokeAsync("  ");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("=");
    }

    // ======================================================================
    // ProcessKillTool — edge cases
    // ======================================================================

    [Fact]
    public async Task ProcessKill_WithNonExistentName_ShouldReturnFailure()
    {
        // Arrange
        var tool = new ProcessKillTool();

        // Act
        var result = await tool.InvokeAsync("nonexistent_process_xyz_9999");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("No process found");
    }
}
