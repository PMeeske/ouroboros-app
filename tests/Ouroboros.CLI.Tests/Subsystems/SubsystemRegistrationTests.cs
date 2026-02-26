using Microsoft.Extensions.DependencyInjection;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Subsystems;

namespace Ouroboros.Tests.CLI.Subsystems;

[Trait("Category", "Unit")]
public class SubsystemRegistrationTests
{
    [Fact]
    public void SubsystemRegistration_IsStaticClass()
    {
        typeof(SubsystemRegistration).IsAbstract.Should().BeTrue();
        typeof(SubsystemRegistration).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void AddAgentSubsystems_RegistersConfig()
    {
        var services = new ServiceCollection();
        var config = new OuroborosConfig();

        services.AddAgentSubsystems(config);

        var provider = services.BuildServiceProvider();
        provider.GetService<OuroborosConfig>().Should().BeSameAs(config);
    }

    [Fact]
    public void AddAgentSubsystems_RegistersVoiceSubsystem()
    {
        var services = new ServiceCollection();
        var config = new OuroborosConfig();

        services.AddAgentSubsystems(config);

        var provider = services.BuildServiceProvider();
        provider.GetService<IVoiceSubsystem>().Should().NotBeNull();
    }

    [Fact]
    public void AddAgentSubsystems_RegistersModelSubsystem()
    {
        var services = new ServiceCollection();
        var config = new OuroborosConfig();

        services.AddAgentSubsystems(config);

        var provider = services.BuildServiceProvider();
        provider.GetService<IModelSubsystem>().Should().NotBeNull();
    }

    [Fact]
    public void AddAgentSubsystems_RegistersToolSubsystem()
    {
        var services = new ServiceCollection();
        var config = new OuroborosConfig();

        services.AddAgentSubsystems(config);

        var provider = services.BuildServiceProvider();
        provider.GetService<IToolSubsystem>().Should().NotBeNull();
    }

    [Fact]
    public void AddAgentSubsystems_RegistersMemorySubsystem()
    {
        var services = new ServiceCollection();
        var config = new OuroborosConfig();

        services.AddAgentSubsystems(config);

        var provider = services.BuildServiceProvider();
        provider.GetService<IMemorySubsystem>().Should().NotBeNull();
    }

    [Fact]
    public void AddAgentSubsystems_RegistersAuthSubsystem()
    {
        var services = new ServiceCollection();
        var config = new OuroborosConfig();

        services.AddAgentSubsystems(config);

        var provider = services.BuildServiceProvider();
        provider.GetService<IAuthSubsystem>().Should().NotBeNull();
    }

    [Fact]
    public void AddAgentSubsystems_RegistersLocalizationSubsystem()
    {
        var services = new ServiceCollection();
        var config = new OuroborosConfig();

        services.AddAgentSubsystems(config);

        var provider = services.BuildServiceProvider();
        provider.GetService<ILocalizationSubsystem>().Should().NotBeNull();
    }

    [Fact]
    public void AddAgentSubsystems_RegistersLanguageSubsystem()
    {
        var services = new ServiceCollection();
        var config = new OuroborosConfig();

        services.AddAgentSubsystems(config);

        var provider = services.BuildServiceProvider();
        provider.GetService<ILanguageSubsystem>().Should().NotBeNull();
    }

    [Fact]
    public void AddOuroborosAgent_RegistersAgent()
    {
        var services = new ServiceCollection();
        var config = new OuroborosConfig();

        // Need subsystems registered first for agent construction
        services.AddAgentSubsystems(config);
        services.AddOuroborosAgent();

        var descriptors = services.Where(d => d.ServiceType.Name == "OuroborosAgent").ToList();
        descriptors.Should().NotBeEmpty();
    }

    [Fact]
    public void AddOuroboros_CombinesSubsystemsAndAgent()
    {
        var services = new ServiceCollection();
        var config = new OuroborosConfig();

        services.AddOuroboros(config);

        var descriptors = services.Where(d => d.ServiceType.Name == "OuroborosAgent").ToList();
        descriptors.Should().NotBeEmpty();
    }

    [Fact]
    public void AddAgentSubsystems_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var config = new OuroborosConfig();

        var result = services.AddAgentSubsystems(config);

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddOuroborosAgent_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddOuroborosAgent();

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddOuroboros_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var config = new OuroborosConfig();

        var result = services.AddOuroboros(config);

        result.Should().BeSameAs(services);
    }
}
