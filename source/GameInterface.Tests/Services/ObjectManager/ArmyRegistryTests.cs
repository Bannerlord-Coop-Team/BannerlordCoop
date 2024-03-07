using Autofac;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Tests.Mocks;
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
    private readonly PatchBootstrap bootstrap;
    private IContainer Container => bootstrap.Container;
    public ArmyRegistryTests()
    {
        bootstrap = new PatchBootstrap();
    }

    public void Dispose() => bootstrap.Dispose();

    [Fact]
    public void RegisterArmy()
    {
        var objectManager = Container.Resolve<IObjectManager>();

        var army = ObjectHelper.SkipConstructor<Army>();

        objectManager.AddNewObject(army, out string newId);

        Assert.Equal(army.GetStringId(), newId);

        objectManager.TryGetNonMBObject<Army>(newId, out var army2);

        Assert.Equal(army, army2);
    }
}
