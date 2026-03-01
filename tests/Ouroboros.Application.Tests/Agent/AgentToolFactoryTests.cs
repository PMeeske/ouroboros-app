using FluentAssertions;
using Ouroboros.Application.Agent;
using Xunit;

namespace Ouroboros.Tests.Agent;

[Trait("Category", "Unit")]
public class AgentToolFactoryTests
{
    [Fact]
    public void ToolDescriptors_ShouldContainExpectedTools()
    {
        var names = AgentToolFactory.ToolDescriptors.Select(d => d.Name).ToList();

        names.Should().Contain("read_file");
        names.Should().Contain("write_file");
        names.Should().Contain("edit_file");
        names.Should().Contain("list_dir");
        names.Should().Contain("search_files");
        names.Should().Contain("run_command");
        names.Should().Contain("vector_search");
        names.Should().Contain("think");
        names.Should().Contain("ask_user");
    }

    [Fact]
    public void ToolDescriptors_ShouldHaveDescriptions()
    {
        foreach (var descriptor in AgentToolFactory.ToolDescriptors)
        {
            descriptor.Description.Should().NotBeNullOrWhiteSpace();
            descriptor.ArgsExample.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void GetShellCommand_OnWindows_ShouldUseCmdExe()
    {
        var (shell, args) = AgentToolFactory.GetShellCommand("dotnet build");

        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
        {
            shell.Should().Be("cmd.exe");
            args.Should().Contain("dotnet build");
        }
    }

    [Fact]
    public void ParseToolArg_ValidJson_ShouldExtractProperty()
    {
        var result = AgentToolFactory.ParseToolArg("{\"path\": \"test.cs\"}", "path");

        result.Should().Be("test.cs");
    }

    [Fact]
    public void ParseToolArg_MissingProperty_ShouldReturnNull()
    {
        var result = AgentToolFactory.ParseToolArg("{\"path\": \"test.cs\"}", "name");

        result.Should().BeNull();
    }

    [Fact]
    public void ParseToolArg_InvalidJson_ShouldReturnNull()
    {
        var result = AgentToolFactory.ParseToolArg("not json", "path");

        result.Should().BeNull();
    }

    [Fact]
    public void ParseToolArg_NumericValue_ShouldReturnRawText()
    {
        var result = AgentToolFactory.ParseToolArg("{\"count\": 42}", "count");

        result.Should().Be("42");
    }
}
