using Autofac;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Armies;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using System;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.ObjectManager;
public class ArmyRegistryTests : IDisposable
{
    readonly IContainer container;
    readonly IDisposable containerProvider;
    public ArmyRegistryTests()
    {
        GameBootStrap.Initialize();

        ContainerBuilder builder = new ContainerBuilder();
        builder.RegisterType<MessageBroker>().As<IMessageBroker>().SingleInstance();
        builder.RegisterModule<GameInterfaceModule>();
        container = builder.Build();

        containerProvider = ContainerProvider.UseContainerThreadSafe(container);
    }

    public void Dispose()
    {
        containerProvider.Dispose();
    }

    [Fact]
    public void RegisterArmy()
    {
        var objectManager = container.Resolve<IObjectManager>();

        var army = ObjectHelper.SkipConstructor<Army>();

        objectManager.AddNewObject(army, out string newId);

        Assert.Equal(army.GetStringId(), newId);

        objectManager.TryGetObject<Army>(newId, out var army2);

        Assert.Equal(army, army2);
    }
}
