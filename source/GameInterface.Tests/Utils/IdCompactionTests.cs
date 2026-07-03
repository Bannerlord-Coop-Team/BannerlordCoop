using GameInterface.Services.ObjectManager;
using GameInterface.Utils.NetworkEvents;
using Moq;
using Serilog;
using Xunit;

namespace GameInterface.Tests.Utils;

/// <summary>
/// Wire-id compaction: <see cref="ObjectManager.Compact"/> strips the redundant "{TypeName}_" prefix
/// on send, the AutoSync message ctors apply it, and <see cref="ObjectManager.TryGetObject{T}"/>
/// re-resolves a compacted id back to the registered object (preferring the prefixed key so a bare id
/// can't resolve to a colliding un-prefixed key).
/// </summary>
public class IdCompactionTests
{
    // Local stand-ins so the test needs no game types; only their Name matters for the prefix.
    private sealed class MobileParty { }
    private sealed class Settlement { }

    private static ObjectManager NewObjectManager() => new ObjectManager(Mock.Of<ILogger>());

    [Fact]
    public void Compact_StripsMatchingTypePrefix()
    {
        Assert.Equal("looters1_1", ObjectManager.Compact("MobileParty_looters1_1", typeof(MobileParty)));
        Assert.Equal("town_ES1", ObjectManager.Compact("Settlement_town_ES1", typeof(Settlement)));
    }

    [Fact]
    public void Compact_LeavesNonMatchingPrefixUntouched()
    {
        // Concrete type differs from the wire type: no strip, so the full id round-trips as-is.
        Assert.Equal("Settlement_town_ES1", ObjectManager.Compact("Settlement_town_ES1", typeof(MobileParty)));
    }

    [Fact]
    public void Compact_LeavesAlreadyCompactIdUntouched()
    {
        Assert.Equal("looters1_1", ObjectManager.Compact("looters1_1", typeof(MobileParty)));
    }

    [Fact]
    public void Compact_HandlesNullAndEmpty()
    {
        Assert.Null(ObjectManager.Compact(null, typeof(MobileParty)));
        Assert.Equal(string.Empty, ObjectManager.Compact(string.Empty, typeof(MobileParty)));
    }

    [Fact]
    public void CompactedId_ResolvesBackToTheRegisteredObject()
    {
        var objectManager = NewObjectManager();
        var party = new MobileParty();
        objectManager.AddExisting("MobileParty_looters1_1", party);

        var wireId = ObjectManager.Compact("MobileParty_looters1_1", typeof(MobileParty));

        Assert.True(objectManager.TryGetObject<MobileParty>(wireId, out var resolved));
        Assert.Same(party, resolved);
    }

    [Fact]
    public void CompactedId_PrefersPrefixedKeyOverCollidingBareKey()
    {
        // A bare id can collide with an un-prefixed key registered for a different object; the resolver
        // must still return the type-correct object stored under "{TypeName}_{id}".
        var objectManager = NewObjectManager();
        var party = new MobileParty();
        objectManager.AddExisting("MobileParty_looters1_1", party);
        objectManager.AddExisting("looters1_1", new Settlement()); // colliding un-prefixed key

        Assert.True(objectManager.TryGetObject<MobileParty>("looters1_1", out var resolved));
        Assert.Same(party, resolved);
    }

    [Fact]
    public void ValueEventCtor_CompactsInstanceId()
    {
        Assert.Equal("looters1_1", new TestValueEvent("MobileParty_looters1_1").InstanceId);
    }

    [Fact]
    public void ReferenceEventCtor_CompactsInstanceIdAndValueId()
    {
        var message = new TestReferenceEvent("MobileParty_looters1_1", "Settlement_town_ES1");
        Assert.Equal("looters1_1", message.InstanceId);
        Assert.Equal("town_ES1", message.ValueId);
    }

    private record TestValueEvent : GenericNetworkEvent<MobileParty, byte[]>
    {
        public override string InstanceId { get; set; } = null!; // set by the base ctor
        public TestValueEvent(string instanceId) : base(instanceId) { }
    }

    private record TestReferenceEvent : GenericNetworkReferenceEvent<MobileParty, Settlement>
    {
        public override string InstanceId { get; set; } = null!; // set by the base ctor
        public override string ValueId { get; set; } = null!; // set by the base ctor
        public TestReferenceEvent(string instanceId, string valueId) : base(instanceId, valueId) { }
    }
}
