using Autofac;
using Common.Serialization;
using GameInterface.AutoSync;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.Monsters;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using Common.Messaging;
using Common.Network;
using Moq;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests;

public class MonsterSerializationTest : IDisposable
{
    private readonly IContainer container;
    private readonly IBinaryPackageFactory factory;
    private readonly IObjectManager objectManager;

    public MonsterSerializationTest()
    {
        GameBootStrap.Initialize();
        var builder = new ContainerBuilder();
        builder.RegisterModule<SerializationTestModule>();
        container = builder.Build();
        factory = container.Resolve<IBinaryPackageFactory>();
        objectManager = container.Resolve<IObjectManager>();
    }

    public void Dispose() => container.Dispose();

    [Theory]
    [InlineData("horse")]
    [InlineData("camel")]
    public void Monster_RoundTrip_ReturnsRegisteredInstance(string stringId)
    {
        var monster = CreateMonster(stringId);
        Assert.True(objectManager.AddExisting("Monster_" + stringId, monster));

        var package = RoundTrip<MonsterBinaryPackage>(monster);

        Assert.Equal("Monster_" + stringId, package.StringId);
        Assert.Same(monster, package.Unpack<Monster>(factory));
        Assert.Same(monster, package.Unpack<Monster>(factory));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Monster_missing")]
    public void Monster_UnresolvedReference_ReturnsNull(string id)
    {
        var package = new MonsterBinaryPackage(CreateMonster("unregistered"), factory) { StringId = id };

        Assert.Null(package.Unpack<Monster>(factory));
        Assert.Null(package.Unpack<Monster>(factory));
    }

    [Fact]
    public void Monster_UnregisteredRoundTrip_DoesNotReturnAnEmptyMonster()
    {
        var package = RoundTrip<MonsterBinaryPackage>(CreateMonster("unregistered"));

        Assert.Null(package.StringId);
        Assert.Null(package.Unpack<Monster>(factory));
    }

    [Theory]
    [InlineData("horse")]
    [InlineData("camel")]
    public void Item_Fallback_UnpacksHorseFieldsAndPreservesMonsterIdentity(string stringId)
    {
        var monster = CreateMonster(stringId);
        Assert.True(objectManager.AddExisting("Monster_" + stringId, monster));
        var horse = new HorseComponent { Monster = monster, BodyLength = 37, ChargeDamage = 19 };
        var item = new ItemObject("mount_" + stringId) { ItemComponent = horse };
        Assert.True(objectManager.AddExisting("ItemObject_" + item.StringId, item));
        var package = RoundTrip<ItemObjectBinaryPackage>(item);
        Assert.True(objectManager.Remove(item));

        var unpacked = package.Unpack<ItemObject>(factory);

        Assert.NotSame(item, unpacked);
        Assert.NotSame(horse, unpacked.HorseComponent);
        Assert.Same(monster, unpacked.HorseComponent.Monster);
        Assert.Equal(37, unpacked.HorseComponent.BodyLength);
        Assert.Equal(19, unpacked.HorseComponent.ChargeDamage);
    }

    [Theory]
    [InlineData("horse")]
    [InlineData("camel")]
    public void Item_ExistingReference_SkipsNestedHorseUnpack(string stringId)
    {
        var monster = CreateMonster(stringId);
        Assert.True(objectManager.AddExisting("Monster_" + stringId, monster));
        var horse = new HorseComponent { Monster = monster, BodyLength = 37 };
        var item = new ItemObject("mount_" + stringId) { ItemComponent = horse };
        Assert.True(objectManager.AddExisting("ItemObject_" + item.StringId, item));
        var package = RoundTrip<ItemObjectBinaryPackage>(item);
        horse.BodyLength = 42;
        Assert.True(objectManager.Remove(monster));

        var unpacked = package.Unpack<ItemObject>(factory);

        Assert.Same(item, unpacked);
        Assert.Same(horse, unpacked.HorseComponent);
        Assert.Same(monster, unpacked.HorseComponent.Monster);
        Assert.Equal(42, unpacked.HorseComponent.BodyLength);
    }

    [Fact]
    public void Horse_UnresolvedMonster_ReturnsNullReference()
    {
        var monster = CreateMonster("horse");
        Assert.True(objectManager.AddExisting("Monster_horse", monster));
        var package = RoundTrip<HorseComponentBinaryPackage>(new HorseComponent { Monster = monster });
        Assert.True(objectManager.Remove(monster));

        Assert.Null(package.Unpack<HorseComponent>(factory).Monster);
    }

    [Fact]
    public void ActiveRegistry_RegisterAllGameObjects_GivesMonstersStableTypedIds()
    {
        var monster = CreateMonster("issue3737_" + Guid.NewGuid().ToString("N"));
        MBObjectManager.Instance.RegisterObject(monster);
        try
        {
            using var activeFactory = new AutoRegistryFactory(
                Mock.Of<IRegistryCollection>(), Mock.Of<IMessageBroker>(), Mock.Of<INetwork>(),
                Mock.Of<IAutoSyncPatchCollector>(), objectManager, Mock.Of<ISerializableTypeMapper>());
            _ = new MonsterRegistry(Mock.Of<ILogger>(), activeFactory, objectManager);
            var manager = new RegistryManager(objectManager, Mock.Of<IRegistryCollection>(),
                Mock.Of<IMessageBroker>(), activeFactory, Mock.Of<IAutoSyncPatchCollector>());

            manager.RegisterAllGameObjects();
            Assert.True(activeFactory.IsManaged(typeof(Monster)));
            Assert.True(objectManager.TryGetId(monster, out var id));
            Assert.Equal("Monster_" + monster.StringId, id);
            manager.RegisterAllGameObjects();
            Assert.True(objectManager.TryGetId(monster, out var repeatedId));
            Assert.Equal(id, repeatedId);
            Assert.True(objectManager.TryGetObject<Monster>(id, out var resolved));
            Assert.Same(monster, resolved);
        }
        finally
        {
            MBObjectManager.Instance.UnregisterObject(monster);
        }
    }

    private static Monster CreateMonster(string stringId) =>
        new Monster { StringId = stringId };

    private TPackage RoundTrip<TPackage>(object value) where TPackage : IBinaryPackage =>
        Assert.IsType<TPackage>(BinaryPackageSerializer.Deserialize(
            BinaryPackageSerializer.Serialize(factory.GetBinaryPackage(value))));
}
