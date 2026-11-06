#if COOP_NAVAL
using Autofac;
using Common.Messaging;
using Common.Network;
using Coop.Naval;
using Coop.Naval.CoopSession.Handlers;
using Coop.Naval.CoopSession.Interfaces;
using Coop.Naval.Notifications.Handlers;
using GameInterface;
using GameInterface.AutoSync;
using GameInterface.CoopSessionData;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Interfaces;
using Moq;
using NavalDLC.CampaignBehaviors;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using Xunit;
using static GameInterface.GameInterface;

namespace Coop.Tests.Naval;

public class CoopNavalModuleTests
{
    private static IContainer BuildContainer()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(Mock.Of<IMessageBroker>());
        builder.RegisterInstance(Mock.Of<IObjectManager>());
        builder.RegisterInstance(Mock.Of<INetwork>());
        builder.RegisterInstance(Mock.Of<ICoopSessionProvider>());
        builder.RegisterInstance(Mock.Of<IPlayerMessageInterface>());
        builder.RegisterInstance(Mock.Of<IPlayerManager>());
        builder.RegisterInstance(Mock.Of<ILogger>());
        builder.RegisterInstance(Mock.Of<IAutoRegistryFactory>());
        builder.RegisterType<AutoSyncRegistry>().SingleInstance();
        builder.RegisterModule<CoopNavalModule>();
        return builder.Build();
    }

    [Fact]
    public void Load_RegistersAndActivatesHandlers()
    {
        using var container = BuildContainer();

        Assert.NotNull(container.Resolve<NavalInitializationHandler>());
        Assert.NotNull(container.Resolve<NavalNotificationsHandler>());
    }

    [Fact]
    public void Load_RegistersGameAbstractionsByInterface()
    {
        using var container = BuildContainer();

        Assert.IsType<SessionNavalPlayerDataInterface>(container.Resolve<ISessionNavalPlayerDataInterface>());
    }

    [Fact]
    public void Load_ActivatesNavalBuildingSyncTargets()
    {
        using var container = BuildContainer();

        var registration = container.Resolve<AutoSyncRegistry>().Registrations[typeof(Building)];
        Assert.Single(registration.TargetMethods, method =>
            method.DeclaringType == typeof(NavalNimbleSurgeCampaignBehaviour) &&
            method.Name == nameof(NavalNimbleSurgeCampaignBehaviour.OnSettlementEntered));
    }

    [Fact]
    public void Load_RegistersNavalCategoryPatchesForTheAssembly()
    {
        using var container = BuildContainer();

        var registration = Assert.Single(container.Resolve<IEnumerable<HarmonyPatchCategoryRegistration>>());
        Assert.Equal(typeof(CoopNavalModule).Assembly, registration.Assembly);
        Assert.Equal(HARMONY_NAVAL_CATEGORY, registration.Category);
    }
}
#endif
