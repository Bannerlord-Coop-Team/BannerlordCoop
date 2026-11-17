using Common.Messaging;
using GameInterface.AutoSync;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Registry.Messages;
using GameInterface.Services.ItemModifiers;
using GameInterface.Services.ItemObjects;
using GameInterface.Services.Monsters;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using Moq;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;
using ObjectManagerService = GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Tests.Registry;

public class RegistryManagerTests
{
    [Fact]
    public void RegisterAllGameObjects_RegistersBeforePublishingCompletion()
    {
        var registrationOrder = new MockSequence();
        var autoRegistryFactory = new Mock<IAutoRegistryFactory>(MockBehavior.Strict);
        var messageBroker = new Mock<IMessageBroker>(MockBehavior.Strict);
        autoRegistryFactory
            .InSequence(registrationOrder)
            .Setup(factory => factory.RegisterAll());
        messageBroker
            .InSequence(registrationOrder)
            .Setup(broker => broker.Publish(It.IsAny<object>(), It.IsAny<AllGameObjectsRegistered>()));
        var manager = CreateManager(autoRegistryFactory.Object, messageBroker.Object);

        manager.RegisterAllGameObjects();

        autoRegistryFactory.VerifyAll();
        messageBroker.VerifyAll();
    }

    [Fact]
    public void RegisterMissionItemObjects_RegistersItemsOnly()
    {
        GameBootStrap.Initialize();
        var item = new ItemObject("mission_item_" + Guid.NewGuid().ToString("N"));
        var monster = new Monster { StringId = "mission_monster_" + Guid.NewGuid().ToString("N") };
        MBObjectManager.Instance.RegisterObject(item);
        MBObjectManager.Instance.RegisterObject(monster);
        try
        {
            var objectManager = new ObjectManagerService(Mock.Of<ILogger>());
            using var factory = new AutoRegistryFactory(
                Mock.Of<IRegistryCollection>(), Mock.Of<IMessageBroker>(), Mock.Of<Common.Network.INetwork>(),
                Mock.Of<IAutoSyncPatchCollector>(), objectManager, Mock.Of<Common.Serialization.ISerializableTypeMapper>());
            var itemRegistry = new ItemObjectRegistry(Mock.Of<ILogger>(), factory, objectManager);
            var modifierRegistry = new ItemModifierRegistry(Mock.Of<ILogger>(), factory, objectManager);
            _ = new MonsterRegistry(Mock.Of<ILogger>(), factory, objectManager);
            var manager = new RegistryManager(objectManager, Mock.Of<IRegistryCollection>(),
                Mock.Of<IMessageBroker>(), factory, Mock.Of<IAutoSyncPatchCollector>(), itemRegistry, modifierRegistry);

            manager.RegisterMissionItemObjects();

            Assert.True(objectManager.TryGetId(item, out var id));
            Assert.Equal("ItemObject_" + item.StringId, id);
            Assert.False(objectManager.TryGetId(monster, out _));
        }
        finally
        {
            MBObjectManager.Instance.UnregisterObject(item);
            MBObjectManager.Instance.UnregisterObject(monster);
        }
    }

    private static RegistryManager CreateManager(
        IAutoRegistryFactory autoRegistryFactory,
        IMessageBroker messageBroker)
    {
        return new RegistryManager(
            Mock.Of<IObjectManager>(),
            Mock.Of<IRegistryCollection>(),
            messageBroker,
            autoRegistryFactory,
            Mock.Of<IAutoSyncPatchCollector>(),
            null,
            null);
    }
}
