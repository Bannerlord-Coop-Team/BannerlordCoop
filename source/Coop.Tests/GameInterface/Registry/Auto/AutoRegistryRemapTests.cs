using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace Coop.Tests.GameInterface.Registry.Auto;

public class AutoRegistryRemapTests
{
    // Minimal AutoRegistryBase that registers a fixed list, to exercise the join-time id override on a client.
    // typeof(object).Name is "Object", so RegisterExistingObject derives "Object_{ownerId}".
    private class TestRegistry : AutoRegistryBase<object>
    {
        public readonly List<(string OwnerId, object Instance)> ToRegister = new();

        public TestRegistry(IObjectManager objectManager)
            : base(Mock.Of<ILogger>(), Mock.Of<IAutoRegistryFactory>(), objectManager) { }

        public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();
        public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();
        public override void OnClientCreated(object obj, string id) { }
        public override void OnClientDestroyed(object obj, string id) { }
        public override void OnServerCreated(object obj, string id) { }
        public override void OnServerDestroyed(object obj, string id) { }

        public override void RegisterAllObjects()
        {
            foreach (var (ownerId, instance) in ToRegister)
            {
                if (!IsCollectingIdRemaps && objectManager.Contains(instance)) continue;

                RegisterExistingObject(ownerId, instance);
            }
        }
    }

    [Fact]
    public void RegisterAllObjectsWithRemap_RegistersUnderServerId_WhenDerivedIdIsInMap()
    {
        // A live-created attachment the joining client would re-derive as "Object_lord_1_8" but the server
        // tracks under "Object_Created_2835".
        var objectManager = new ObjectManager(Mock.Of<ILogger>());
        var attachment = new object();
        var registry = new TestRegistry(objectManager);
        registry.ToRegister.Add(("lord_1_8", attachment));

        registry.RegisterAllObjectsWithRemap(new Dictionary<string, string>
        {
            ["Object_lord_1_8"] = "Object_Created_2835",
        });

        // It is registered once, under the server's id, never under the re-derived id.
        Assert.True(objectManager.TryGetObject<object>("Object_Created_2835", out var found));
        Assert.Same(attachment, found);
        Assert.False(objectManager.TryGetObject<object>("Object_lord_1_8", out _));
    }

    [Fact]
    public void RegisterAllObjectsWithRemap_RegistersUnderDerivedId_WhenNotInMap()
    {
        // A save-loaded attachment whose derived id matches the server's: no map entry, register normally.
        var objectManager = new ObjectManager(Mock.Of<ILogger>());
        var attachment = new object();
        var registry = new TestRegistry(objectManager);
        registry.ToRegister.Add(("lord_1_8", attachment));

        registry.RegisterAllObjectsWithRemap(new Dictionary<string, string>());

        Assert.True(objectManager.TryGetObject<object>("Object_lord_1_8", out var found));
        Assert.Same(attachment, found);
    }

    [Fact]
    public void CollectIdRemap_VisitsAlreadyRegisteredLiveObject()
    {
        var objectManager = new ObjectManager(Mock.Of<ILogger>());
        var attachment = new object();
        objectManager.AddExisting("Object_Created_2835", attachment);
        var registry = new TestRegistry(objectManager);
        registry.ToRegister.Add(("CharacterObject_1", attachment));
        var map = new Dictionary<string, string>();

        registry.CollectIdRemap(map);

        Assert.Equal("Object_Created_2835", map["Object_CharacterObject_1"]);
    }
}
