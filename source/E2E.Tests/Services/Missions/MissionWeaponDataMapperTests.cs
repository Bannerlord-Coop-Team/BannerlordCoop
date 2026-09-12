using GameInterface.Services.ObjectManager;
using Missions.Data;
using Moq;
using ProtoBuf;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class MissionWeaponDataMapperTests
{
    private const string ItemId = "ItemObject_test_sword";
    private const string ModifierId = "ItemModifier_test_fine";

    [Fact]
    public void ReceivedModifiedWeapon_ResolvesReceiverCanonicalModifier()
    {
        var senderObjects = NewObjectManager();
        var receiverObjects = NewObjectManager();
        var senderItem = new ItemObject("test_sword");
        var receiverItem = new ItemObject("test_sword");
        var senderModifier = new ItemModifier { StringId = "test_fine" };
        var receiverModifier = new ItemModifier { StringId = "test_fine" };
        Assert.True(senderObjects.AddExisting(ItemId, senderItem));
        Assert.True(senderObjects.AddExisting(ModifierId, senderModifier));
        Assert.True(receiverObjects.AddExisting(ItemId, receiverItem));
        Assert.True(receiverObjects.AddExisting(ModifierId, receiverModifier));

        var senderMapper = new MissionWeaponDataMapper(senderObjects);
        Assert.True(senderMapper.TryPack(
            new MissionWeapon(senderItem, senderModifier, null, 4),
            out MissionWeaponData packed));

        MissionWeaponData received = Serializer.DeepClone(packed);
        var receiverMapper = new MissionWeaponDataMapper(receiverObjects);
        Assert.True(receiverMapper.TryResolve(received, out MissionWeapon resolved));

        Assert.Equal(ModifierId, received.ItemModifierId);
        Assert.Same(receiverItem, resolved.Item);
        Assert.Same(receiverModifier, resolved.ItemModifier);
        Assert.NotSame(senderModifier, resolved.ItemModifier);
    }

    [Fact]
    public void ReceivedUnmodifiedWeapon_PreservesNullModifier()
    {
        var senderObjects = NewObjectManager();
        var receiverObjects = NewObjectManager();
        var senderItem = new ItemObject("test_sword");
        var receiverItem = new ItemObject("test_sword");
        Assert.True(senderObjects.AddExisting(ItemId, senderItem));
        Assert.True(receiverObjects.AddExisting(ItemId, receiverItem));

        var senderMapper = new MissionWeaponDataMapper(senderObjects);
        Assert.True(senderMapper.TryPack(
            new MissionWeapon(senderItem, null, null, 2),
            out MissionWeaponData packed));

        MissionWeaponData received = Serializer.DeepClone(packed);
        var receiverMapper = new MissionWeaponDataMapper(receiverObjects);
        Assert.True(receiverMapper.TryResolve(received, out MissionWeapon resolved));

        Assert.Null(received.ItemModifierId);
        Assert.Same(receiverItem, resolved.Item);
        Assert.Null(resolved.ItemModifier);
    }

    [Fact]
    public void MissingReceivedModifier_DoesNotCreateDuplicateModifier()
    {
        var objectManager = NewObjectManager();
        var item = new ItemObject("test_sword");
        Assert.True(objectManager.AddExisting(ItemId, item));
        var mapper = new MissionWeaponDataMapper(objectManager);
        var data = new MissionWeaponData(
            ItemId,
            ModifierId,
            null,
            0,
            0,
            null);

        Assert.False(mapper.TryResolve(data, out _));
        Assert.False(objectManager.Contains(ModifierId));
    }

    private static ObjectManager NewObjectManager() =>
        new ObjectManager(Mock.Of<ILogger>());
}
