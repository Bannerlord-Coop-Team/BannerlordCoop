using GameInterface.Registry.Auto;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class MapEventRegistryTests
{
    [Fact]
    public void RegisterMapEvents_AssignsAndRegistersMissingNetworkIdentity()
    {
        var existing = CreateMapEvent("existing_map_event");
        var missing = CreateMapEvent();
        var objectManager = CreateObjectManager();
        objectManager.Setup(manager => manager.GetUniqueTypeId(missing)).Returns(12);
        var registry = CreateRegistry(objectManager.Object);

        registry.RegisterMapEvents(new[] { existing, missing });

        Assert.Equal("existing_map_event", existing.StringId);
        Assert.Equal("Created_12", missing.StringId);
        objectManager.Verify(
            manager => manager.AddExisting("MapEvent_existing_map_event", existing),
            Times.Once);
        objectManager.Verify(
            manager => manager.AddExisting("MapEvent_Created_12", missing),
            Times.Once);
    }

    [Fact]
    public void RegisterMapEvents_SeedsExistingIdsBeforeAllocatingMissingIdentity()
    {
        var calls = new List<string>();
        var existing = CreateMapEvent("Created_40");
        var missing = CreateMapEvent();
        var objectManager = CreateObjectManager();
        objectManager
            .Setup(manager => manager.AddExisting("MapEvent_Created_40", existing))
            .Callback(() => calls.Add("register existing"))
            .Returns(true);
        objectManager
            .Setup(manager => manager.GetUniqueTypeId(missing))
            .Callback(() => calls.Add("allocate missing"))
            .Returns(42);
        var registry = CreateRegistry(objectManager.Object);

        registry.RegisterMapEvents(new[] { missing, existing });

        Assert.Equal(new[] { "register existing", "allocate missing" }, calls);
        Assert.Equal("Created_42", missing.StringId);
    }

    [Fact]
    public void GetNetworkId_RejectsUnregisteredMapEvent()
    {
        var mapEvent = CreateMapEvent();

        var exception = Assert.Throws<InvalidOperationException>(
            () => MapEventRegistry.GetNetworkId(mapEvent));

        Assert.Contains("must be registered", exception.Message);
    }

    private static Mock<IObjectManager> CreateObjectManager()
    {
        var objectManager = new Mock<IObjectManager>();
        objectManager
            .Setup(manager => manager.AddExisting(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(true);
        return objectManager;
    }

    private static MapEventRegistry CreateRegistry(IObjectManager objectManager)
    {
        return new MapEventRegistry(
            new Mock<ILogger>().Object,
            new Mock<IAutoRegistryFactory>().Object,
            objectManager);
    }

    private static MapEvent CreateMapEvent(string? stringId = null)
    {
        var mapEvent = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
        mapEvent.StringId = stringId;
        return mapEvent;
    }
}
