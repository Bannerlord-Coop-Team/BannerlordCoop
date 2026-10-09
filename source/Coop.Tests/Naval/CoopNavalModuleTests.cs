#if DEBUG
using Autofac;
using Common.Messaging;
using Common.Network;
using Coop.Naval;
using Coop.Naval.Handlers;
using Coop.Naval.Interfaces;
using Coop.Naval.Notifications.Handlers;
using GameInterface;
using GameInterface.CoopSessionData;
using GameInterface.Services.ObjectManager;
using Moq;
using System.Collections.Generic;
using Xunit;

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
    public void Load_RegistersUncategorizedPatchesForTheAssembly()
    {
        using var container = BuildContainer();

        var registration = Assert.Single(container.Resolve<IEnumerable<HarmonyPatchCategoryRegistration>>());
        Assert.Equal(typeof(CoopNavalModule).Assembly, registration.Assembly);
        Assert.Null(registration.Category);
    }
}
#endif
