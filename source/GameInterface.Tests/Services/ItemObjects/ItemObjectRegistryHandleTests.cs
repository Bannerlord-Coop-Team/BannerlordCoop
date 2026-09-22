using Common;
using GameInterface.Registry.Auto;
using GameInterface.Services.ItemObjects;
using Moq;
using Serilog;
using TaleWorlds.Core;
using Xunit;
using ObjectManagerService = GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Tests.Services.ItemObjects;

[Collection(ModInformationRoleCollection.Name)]
public class ItemObjectRegistryHandleTests
{
    [Fact]
    public void ReplacementItem_ReusesExistingHandleWithoutAnnouncement()
    {
        bool wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            var manager = new ObjectManagerService(Mock.Of<ILogger>());
            var registry = new ItemObjectRegistry(
                Mock.Of<ILogger>(),
                Mock.Of<IAutoRegistryFactory>(),
                manager);
            var original = new ItemObject("test_item");
            var replacement = new ItemObject("test_item");

            Assert.True(manager.AddExisting("ItemObject_test_item", original));
            Assert.True(manager.TryGetHandle(original, out var originalHandle));

            Assert.True(registry.TryRegisterExistingItem(
                replacement,
                out _,
                out var replacementHandle,
                out var announceHandle));

            Assert.Equal(originalHandle, replacementHandle);
            Assert.False(announceHandle);
            Assert.True(manager.TryGetObject(replacementHandle, out ItemObject resolved));
            Assert.Same(replacement, resolved);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void NewItem_ReturnsHandleThatMustBeAnnounced()
    {
        bool wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            var manager = new ObjectManagerService(Mock.Of<ILogger>());
            var registry = new ItemObjectRegistry(
                Mock.Of<ILogger>(),
                Mock.Of<IAutoRegistryFactory>(),
                manager);
            var item = new ItemObject("test_item");

            Assert.True(registry.TryRegisterExistingItem(
                item,
                out _,
                out var itemHandle,
                out var announceHandle));

            Assert.NotEqual(0u, itemHandle);
            Assert.True(announceHandle);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
