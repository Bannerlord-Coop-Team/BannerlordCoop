using Common;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using Xunit;
using ObjectManagerService = GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Tests.Services;

[Collection(ModInformationRoleCollection.Name)]
public class ObjectManagerHandleTests
{
    [Fact]
    public void ServerRegistration_AssignsMonotonicHandlesWithoutReuse()
    {
        bool wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            var manager = CreateManager();
            var first = new object();
            var second = new object();

            Assert.True(manager.AddExisting("first", first));
            Assert.True(manager.TryGetHandle(first, out var firstHandle));
            Assert.True(manager.Remove(first));
            Assert.True(manager.AddExisting("second", second));
            Assert.True(manager.TryGetHandle(second, out var secondHandle));

            Assert.Equal(1u, firstHandle);
            Assert.Equal(2u, secondHandle);
            Assert.False(manager.TryGetObject<object>(firstHandle, out _));
            Assert.True(manager.TryGetObject(secondHandle, out object resolved));
            Assert.Same(second, resolved);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void SameObjectReregistration_PreservesHandle()
    {
        bool wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            var manager = CreateManager();
            var value = new object();

            Assert.True(manager.AddExisting("first", value));
            Assert.True(manager.TryGetHandle(value, out var originalHandle));
            Assert.True(manager.Remove(value));
            Assert.True(manager.AddExisting("first", value));
            Assert.True(manager.TryGetHandle(value, out var restoredHandle));

            Assert.Equal(originalHandle, restoredHandle);
            Assert.True(manager.TryGetObject(restoredHandle, out object resolved));
            Assert.Same(value, resolved);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void ClientJoinMap_AdoptsServerHandles()
    {
        bool wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;
        try
        {
            var manager = CreateManager();
            manager.SetJoinHandleMap(new Dictionary<string, uint>
            {
                ["first"] = 41,
                ["second"] = 42,
            });
            var first = new object();
            var second = new object();

            Assert.True(manager.AddExisting("first", first));
            Assert.True(manager.AddExisting("second", second));
            Assert.True(manager.TryGetHandle(first, out var firstHandle));
            Assert.True(manager.TryGetHandle(second, out var secondHandle));

            Assert.Equal(41u, firstHandle);
            Assert.Equal(42u, secondHandle);
            Assert.Equal(2, manager.GetHandleMap().Count);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void ExplicitZeroOrDuplicateHandle_IsRejectedWithoutLeakingId()
    {
        var manager = CreateManager();
        var first = new object();
        var duplicate = new object();

        Assert.False(manager.AddExisting("zero", first, 0));
        Assert.False(manager.Contains("zero"));
        Assert.True(manager.AddExisting("first", first, 7));
        Assert.False(manager.AddExisting("duplicate", duplicate, 7));
        Assert.False(manager.Contains("duplicate"));
    }

    [Fact]
    public void ClientJoinMap_RejectsReservedZeroHandle()
    {
        bool wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;
        try
        {
            var manager = CreateManager();
            manager.SetJoinHandleMap(new Dictionary<string, uint> { ["invalid"] = 0 });

            Assert.Throws<InvalidOperationException>(() => manager.AddExisting("invalid", new object()));
            Assert.False(manager.Contains("invalid"));
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    private static ObjectManagerService CreateManager() => new(Mock.Of<ILogger>());
}
