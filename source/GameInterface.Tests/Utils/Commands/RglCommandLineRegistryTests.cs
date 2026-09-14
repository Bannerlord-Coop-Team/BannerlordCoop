using GameInterface.Utils.Commands;
using System;
using Xunit;

namespace GameInterface.Tests.Utils.Commands;

public class RglCommandLineRegistryTests
{
    [Fact]
    public void RegisterCommand_AcrossInstances_RegistersNameOnlyOnce()
    {
        string fullName = $"coop.debug.rgl_registry_test.{Guid.NewGuid():N}";
        int registrationCount = 0;
        Action<string> registerCommand = _ => registrationCount++;
        var firstRegistry = new RglCommandLineRegistry(registerCommand);
        var secondRegistry = new RglCommandLineRegistry(registerCommand);

        firstRegistry.RegisterCommand(fullName);
        secondRegistry.RegisterCommand(fullName);

        Assert.Equal(1, registrationCount);
    }

    [Fact]
    public void RegisterCommand_WhenEngineBecomesAvailable_RetriesRegistration()
    {
        string fullName = $"coop.debug.rgl_registry_test.{Guid.NewGuid():N}";
        bool isEngineAvailable = false;
        int registrationCount = 0;
        var registry = new RglCommandLineRegistry(
            _ => registrationCount++,
            () => isEngineAvailable);

        registry.RegisterCommand(fullName);
        isEngineAvailable = true;
        registry.RegisterCommand(fullName);

        Assert.Equal(1, registrationCount);
    }
}
