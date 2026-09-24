using Common;
using GameInterface.Registry.Auto;
using GameInterface.Services.ItemObjects;
using GameInterface.Tests.Bootstrap;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
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
            registry.MarkHandleKnownToClients(originalHandle);

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

            registry.MarkHandleKnownToClients(itemHandle);
            Assert.True(registry.TryRegisterExistingItem(
                item,
                out _,
                out var repeatedHandle,
                out var repeatAnnouncement));
            Assert.Equal(itemHandle, repeatedHandle);
            Assert.False(repeatAnnouncement);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void ItemRegisteredOutsideRegistry_ReturnsHandleThatMustBeAnnounced()
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

            Assert.True(manager.AddExisting("ItemObject_test_item", item));
            Assert.True(manager.TryGetHandle(item, out var originalHandle));

            Assert.True(registry.TryRegisterExistingItem(
                item,
                out _,
                out var itemHandle,
                out var announceHandle));

            Assert.Equal(originalHandle, itemHandle);
            Assert.True(announceHandle);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void CollectIdRemap_DoesNotMarkExternallyRegisteredHandleKnownToClients()
    {
        bool wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        GameBootStrap.Initialize();
        var item = new ItemObject("handle-remap-test-" + Guid.NewGuid().ToString("N"));
        MBObjectManager.Instance.RegisterObject(item);
        try
        {
            var manager = new ObjectManagerService(Mock.Of<ILogger>());
            var registry = new ItemObjectRegistry(
                Mock.Of<ILogger>(),
                Mock.Of<IAutoRegistryFactory>(),
                manager);

            Assert.True(manager.AddExisting("ItemObject_" + item.StringId, item));
            registry.CollectIdRemap(new Dictionary<string, string>());

            Assert.True(registry.TryRegisterExistingItem(
                item,
                out _,
                out _,
                out var announceHandle));
            Assert.True(announceHandle);
        }
        finally
        {
            MBObjectManager.Instance.UnregisterObject(item);
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void MidSessionRescan_DoesNotMarkExternallyRegisteredHandleKnownToClients()
    {
        bool wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        GameBootStrap.Initialize();
        var initialItem = new ItemObject("handle-seed-test-" + Guid.NewGuid().ToString("N"));
        var externalItem = new ItemObject("handle-rescan-test-" + Guid.NewGuid().ToString("N"));
        MBObjectManager.Instance.RegisterObject(initialItem);
        try
        {
            var manager = new ObjectManagerService(Mock.Of<ILogger>());
            var registry = new ItemObjectRegistry(
                Mock.Of<ILogger>(),
                Mock.Of<IAutoRegistryFactory>(),
                manager);

            registry.RegisterAllObjects();

            MBObjectManager.Instance.RegisterObject(externalItem);
            Assert.True(manager.AddExisting("ItemObject_" + externalItem.StringId, externalItem));
            registry.RegisterAllObjects();

            Assert.True(registry.TryRegisterExistingItem(
                externalItem,
                out _,
                out _,
                out var announceHandle));
            Assert.True(announceHandle);
        }
        finally
        {
            MBObjectManager.Instance.UnregisterObject(externalItem);
            MBObjectManager.Instance.UnregisterObject(initialItem);
            ModInformation.IsServer = wasServer;
        }
    }
}
