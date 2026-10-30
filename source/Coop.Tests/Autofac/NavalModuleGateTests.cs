using Autofac;
using Common;
using Coop.Core.Client;
using Coop.Core.Server;
using Coop.Core.Server.Services.Instances.Handlers;
using GameInterface;
using GameInterface.Services.MapEvents;
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

    private readonly bool previousNavalDlc = ModInformation.IsNavalDlcActive;

    // Off means no patch category from a naval assembly, so GameInterface.PatchAll applies none of their patches.
    [Fact]
    public void ClientContainer_WithNavalInactive_RegistersNoNavalPatchesOrLauncher()
    {
        ModInformation.IsNavalDlcActive = false;

        using var container = BuildClientContainer();

        Assert.Empty(NavalPatchRegistrations(container));
        Assert.False(container.IsRegistered<ICoopNavalBattleLauncher>());
    }

#if COOP_NAVAL
    // The naval assemblies sit next to GameInterface.dll in a naval test build, so on means both modules load.
    [Fact]
    public void ClientContainer_WithNavalActive_RegistersBothNavalPatchSets()
    {
        ModInformation.IsNavalDlcActive = true;

        using var container = BuildClientContainer();

        Assert.Equal(NavalAssemblyNames.OrderBy(name => name),
            NavalPatchRegistrations(container).Select(r => r.Assembly.GetName().Name).OrderBy(name => name));
        Assert.True(container.IsRegistered<ICoopNavalBattleLauncher>());
    }
#endif

    // The game's server module reads the startup decision, so off leaves the naval-only server handler out.
    [Fact]
    public void DefaultServerModule_WithNavalInactive_LeavesTheShipFateHandlerOut()
    {
        ModInformation.IsNavalDlcActive = false;

        using var container = BuildServerContainer(new ServerModule(), new GameInterfaceModule());

        Assert.False(container.IsRegistered<ServerBattleShipFateHandler>());
    }

    // The explicit decision moves the server handler together with the GameInterface services it takes.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ServerModule_GatesTheShipFateHandlerWithItsServices(bool navalServicesEnabled)
    {
        using var container = BuildServerContainer(
            new ServerModule(navalServicesEnabled), new GameInterfaceModule(navalServicesEnabled));

        Assert.Equal(navalServicesEnabled, container.IsRegistered<ServerBattleShipFateHandler>());
        Assert.Equal(navalServicesEnabled, container.IsRegistered<global::GameInterface.Services.Ships.IBattleShipFateLedger>());
    }

    // Builds a server container's registrations without activating anything.
    private static IContainer BuildServerContainer(ServerModule serverModule, GameInterfaceModule gameInterfaceModule)
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule(serverModule);
        builder.RegisterModule(gameInterfaceModule);
        return builder.Build(global::Autofac.Builder.ContainerBuildOptions.IgnoreStartableComponents);
    }

    // Builds the client container the way the game does, which also loads MissionModule.
    private static IContainer BuildClientContainer()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<ClientModule>();
        builder.RegisterModule<GameInterfaceModule>();
        return builder.Build();
    }

    // The patch registrations contributed by Coop.Naval or Missions.Naval.
    private static IEnumerable<HarmonyPatchCategoryRegistration> NavalPatchRegistrations(IContainer container) =>
        container.Resolve<IEnumerable<HarmonyPatchCategoryRegistration>>()
            .Where(r => NavalAssemblyNames.Contains(r.Assembly.GetName().Name));

    public void Dispose()
    {
        ModInformation.IsNavalDlcActive = previousNavalDlc;
    }
}
