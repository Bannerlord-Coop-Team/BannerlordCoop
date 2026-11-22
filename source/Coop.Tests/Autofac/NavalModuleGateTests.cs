using Coop.Core;
using Autofac;
using Autofac.Builder;
using Common;
using Common.Commands;
using Coop.Core.Client;
using Coop.Core.Server;
using Coop.Core.Server.Services.Instances.Handlers;
using GameInterface;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Ships;
using GameInterface.Utils;
using Missions;
using Missions.Agents.Handlers;
using Missions.Battles;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Coop.Tests.Autofac;

/// <summary>
/// Covers that the naval assemblies contribute no services or Harmony patches unless NavalDLC co-op is active.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public sealed class NavalModuleGateTests : System.IDisposable
{
    private static readonly string[] NavalAssemblyNames = { "Coop.Naval", "Missions.Naval" };

    private readonly bool previousNavalDlc = ModInformation.IsNavalEnabled;

    // Off means no patch category from a naval assembly, so GameInterface.PatchAll applies none of their patches.
    [Fact]
    public void ClientContainer_WithNavalInactive_RegistersNoNavalPatchesOrLauncher()
    {
        ModInformation.IsNavalEnabled = false;

        using var container = BuildClientContainer();

        Assert.Empty(NavalPatchRegistrations(container));
        Assert.False(container.IsRegistered<ICoopNavalBattleLauncher>());
    }

#if COOP_NAVAL
    // The naval assemblies sit next to GameInterface.dll in a naval test build, so on means both modules load.
    [Fact]
    public void ClientContainer_WithNavalActive_RegistersBothNavalPatchSets()
    {
        ModInformation.IsNavalEnabled = true;

        using var container = BuildClientContainer();

        Assert.Equal(NavalAssemblyNames.OrderBy(name => name),
            NavalPatchRegistrations(container).Select(r => r.Assembly.GetName().Name).OrderBy(name => name));
        Assert.True(container.IsRegistered<ICoopNavalBattleLauncher>());
        Assert.True(container.IsRegistered<INavalBattleServicesFactory>());
    }

    // The server applies hit rewards through NavalSkillLevellingManager, so it needs the Missions.Naval patches and commands too.
    [Fact]
    public void ServerContainer_WithNavalActive_RegistersBothNavalPatchSetsAndServerCommands()
    {
        ModInformation.IsNavalEnabled = true;

        var builder = new ContainerBuilder();
        builder.RegisterModule<ServerModule>();
        builder.RegisterModule<GameInterfaceModule>();
        builder.RegisterModule<NavalAssemblyModule>();
        using var container = builder.Build(ContainerBuildOptions.IgnoreStartableComponents);

        Assert.Equal(NavalAssemblyNames.OrderBy(name => name),
            NavalPatchRegistrations(container).Select(r => r.Assembly.GetName().Name).OrderBy(name => name));
        Assert.Contains(container.Resolve<IEnumerable<ICoopCommand>>(),
            command => $"{command.Prefix}.{command.Name}" == "coop.debug.naval.server_hp");
    }
#endif

    // The game's server module reads the startup decision, so off leaves the naval-only server handler out.
    [Fact]
    public void DefaultServerModule_WithNavalInactive_LeavesTheShipFateHandlerOut()
    {
        ModInformation.IsNavalEnabled = false;

        using var container = BuildServerContainer(new ServerModule(), new GameInterfaceModule());

        Assert.False(container.IsRegistered<ServerBattleShipFateHandler>());
    }

    // The flag moves the server handler together with the GameInterface services it takes.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ServerModule_GatesTheShipFateHandlerWithItsServices(bool isNavalEnabled)
    {
        ModInformation.IsNavalEnabled = isNavalEnabled;

        using var container = BuildServerContainer(new ServerModule(), new GameInterfaceModule());

        Assert.Equal(isNavalEnabled, container.IsRegistered<ServerBattleShipFateHandler>());
        Assert.Equal(isNavalEnabled, container.IsRegistered<ServerNavalNpcFleetAttemptHandler>());
        Assert.Equal(isNavalEnabled, container.IsRegistered<IBattleShipFateLedger>());
        Assert.Equal(isNavalEnabled, container.IsRegistered<INavalNpcFleetAttemptTracker>());
    }

    // The game's client modules read the startup decision, so off leaves the water clock and naval battle factory out.
    [Fact]
    public void DefaultClientModules_WithNavalInactive_LeaveTheNavalMissionServicesOut()
    {
        ModInformation.IsNavalEnabled = false;

        using var container = BuildClientRegistrations(new ClientModule(), new GameInterfaceModule());

        Assert.False(container.IsRegistered<IWaterClock>());
        Assert.False(container.IsRegistered<INavalBattleServicesFactory>());
    }

    // The flag moves the water clock; the ship registry and deck probe serve land battles too.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClientModules_GateTheWaterClockOnly(bool isNavalEnabled)
    {
        ModInformation.IsNavalEnabled = isNavalEnabled;

        using var container = BuildClientRegistrations(new ClientModule(), new GameInterfaceModule());

        Assert.Equal(isNavalEnabled, container.IsRegistered<IWaterClock>());
        Assert.True(container.IsRegistered<INetworkShipRegistry>());
        Assert.True(container.IsRegistered<INavalDeckMovementProbe>());
    }

    // Builds a client container's registrations without activating anything.
    private static IContainer BuildClientRegistrations(ClientModule clientModule, GameInterfaceModule gameInterfaceModule)
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule(clientModule);
        builder.RegisterModule(gameInterfaceModule);
        return builder.Build(ContainerBuildOptions.IgnoreStartableComponents);
    }

    // Builds a server container's registrations without activating anything.
    private static IContainer BuildServerContainer(ServerModule serverModule, GameInterfaceModule gameInterfaceModule)
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule(serverModule);
        builder.RegisterModule(gameInterfaceModule);
        return builder.Build(ContainerBuildOptions.IgnoreStartableComponents);
    }

    // Builds the client container the way the game does, which also loads MissionModule.
    private static IContainer BuildClientContainer()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<ClientModule>();
        builder.RegisterModule<GameInterfaceModule>();
        builder.RegisterModule<NavalAssemblyModule>();
        return builder.Build();
    }

    // The patch registrations contributed by Coop.Naval or Missions.Naval.
    private static IEnumerable<HarmonyPatchCategoryRegistration> NavalPatchRegistrations(IContainer container) =>
        container.Resolve<IEnumerable<HarmonyPatchCategoryRegistration>>()
            .Where(r => NavalAssemblyNames.Contains(r.Assembly.GetName().Name));

    public void Dispose()
    {
        ModInformation.IsNavalEnabled = previousNavalDlc;
    }
}
