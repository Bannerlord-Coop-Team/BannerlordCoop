#if DEBUG
using Missions.Battles;
using Missions.Messages;
using ProtoBuf;
using System;
using System.Linq;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalLabSessionStoreTests
{
    private static NavalLabManifest Manifest(Guid? incarnation = null, Guid[]? combatants = null, Guid[]? ships = null)
    {
        var id = incarnation ?? Guid.NewGuid();
        return new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            combatants ?? Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(),
            ships ?? new[] { Guid.NewGuid(), Guid.NewGuid() });
    }

    [Fact]
    public void Manifest_RejectsEmptyOrRepeatedIndividuals()
    {
        Assert.Throws<ArgumentException>(() => Manifest(combatants: new Guid[10]));
        Assert.Throws<ArgumentException>(() => Manifest(combatants: Enumerable.Repeat(Guid.NewGuid(), 10).ToArray()));
        Assert.Throws<ArgumentException>(() => Manifest(ships: new[] { Guid.Empty, Guid.NewGuid() }));
    }

    [Fact]
    public void Manifest_CannotTagALiveMapEvent()
    {
        var valid = Manifest();
        Assert.Throws<ArgumentException>(() => new NavalLabManifest("mapEvent1", valid.IncarnationId,
            valid.Controllers, valid.Combatants, valid.Ships));
    }

    [Fact]
    public void Manifest_ClonesIdentityArraysOnReadAndConstruction()
    {
        var combatants = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();
        var expected = combatants[0];
        var manifest = Manifest(combatants: combatants);
        combatants[0] = Guid.Empty;
        manifest.Combatants[0] = Guid.Empty;
        manifest.Controllers[0] = "replacement";
        Assert.Equal(expected, manifest.Combatants[0]);
        Assert.Equal("A", manifest.Controllers[0]);
    }

    [Fact]
    public void Install_RejectsDifferentContentsForTheSameIncarnation()
    {
        var store = new NavalLabSessionStore();
        var original = Manifest();
        store.Install(original);
        store.Install(original);
        Assert.Throws<InvalidOperationException>(() => store.Install(Manifest(original.IncarnationId)));
        Assert.Same(original, store.Current);
        Assert.False(store.Contains("mapEvent1"));
        Assert.False(store.IsParticipant(original.InstanceId, "C"));
    }

    [Fact]
    public void BeginOperation_DeduplicatesAndRejectsConflictingContents()
    {
        var store = new NavalLabSessionStore();
        var id = Guid.NewGuid();
        Assert.True(store.BeginOperation(id, "helm:0"));
        Assert.False(store.BeginOperation(id, "helm:0"));
        Assert.Throws<InvalidOperationException>(() => store.BeginOperation(id, "helm:1"));
        Assert.Throws<ArgumentException>(() => store.BeginOperation(Guid.Empty, "helm:0"));
    }

    [Fact]
    public void BeginOperation_HasABoundedCumulativeBudget()
    {
        var store = new NavalLabSessionStore();
        for (int i = 0; i < 64; i++) Assert.True(store.BeginOperation(Guid.NewGuid(), "helm"));
        Assert.Throws<InvalidOperationException>(() => store.BeginOperation(Guid.NewGuid(), "helm"));
    }

    [Fact]
    public void EmergencyOperations_ReserveExactlyOneHoldAndStopBeyondOrdinaryBudget()
    {
        var store = new NavalLabSessionStore();
        for (int i = 0; i < 64; i++) store.BeginOperation(Guid.NewGuid(), "helm");
        foreach (var kind in new[] { "hold", "stop" })
        {
            var id = Guid.NewGuid();
            Assert.True(store.BeginEmergencyOperation(id, kind));
            Assert.False(store.BeginEmergencyOperation(id, kind));
            Assert.Throws<InvalidOperationException>(() => store.BeginEmergencyOperation(Guid.NewGuid(), kind));
            store.RecordReceipt(id, "A", "applied");
        }
        Assert.Throws<ArgumentException>(() => store.BeginEmergencyOperation(Guid.NewGuid(), "helm"));
        Assert.Throws<InvalidOperationException>(() => store.BeginOperation(Guid.NewGuid(), "helm"));
    }

    [Fact]
    public void RecordReceipt_DoesNotReplaceATerminalOutcome()
    {
        var store = new NavalLabSessionStore();
        var id = Guid.NewGuid();
        store.BeginOperation(id, "helm");
        store.RecordReceipt(id, "A", "applied");
        store.RecordReceipt(id, "A", "applied");
        Assert.Throws<InvalidOperationException>(() => store.RecordReceipt(id, "A", "failed"));
    }

    [Fact]
    public void CampaignWriteBlocker_PreservesTheFirstFailure()
    {
        var store = new NavalLabSessionStore();
        store.RejectCampaignWrite("casualty");
        store.RejectCampaignWrite("result");
        Assert.Equal("casualty", store.CampaignWriteBlocker);
    }

    [Fact]
    public void NetworkStart_RoundTripsAllDistinctIdentities()
    {
        var manifest = Manifest();
        var copy = Serializer.DeepClone(new NetworkNavalLabStart(manifest.InstanceId, manifest.IncarnationId,
            manifest.Controllers, manifest.Combatants, manifest.Ships));
        Assert.Equal(manifest.InstanceId, copy.InstanceId);
        Assert.Equal(manifest.IncarnationId, copy.IncarnationId);
        Assert.Equal(manifest.Controllers, copy.Controllers);
        Assert.Equal(manifest.Combatants, copy.Combatants);
        Assert.Equal(manifest.Ships, copy.Ships);
    }

    [Fact]
    public void CasualtyWire_PreservesFixtureIdentityWithoutChangingLegacyDefault()
    {
        var original = new NetworkRequestBattleCasualty("party", "troop", true, Manifest().InstanceId);
        var copy = Serializer.DeepClone(original);
        Assert.Equal(original.InstanceId, copy.InstanceId);
        Assert.Equal(original.MapEventPartyId, copy.MapEventPartyId);
        Assert.True(copy.Wounded);
        Assert.Null(Serializer.DeepClone(new NetworkRequestBattleCasualty("party", "troop", false)).InstanceId);
    }
    [Fact]
    public void ExistingOperation_IsQueryableBeforeCurrentReadinessValidationWithoutReallocation()
    {
        var store = new NavalLabSessionStore();
        var id = Guid.NewGuid();
        Assert.False(store.TryInspectOperation(id, "probe:1:0.2:True", out _));
        store.BeginOperation(id, "probe:1:0.2:True");
        store.RecordReceipt(id, "A", "applied");
        Assert.True(store.TryInspectOperation(id, "probe:1:0.2:True", out var receipt));
        Assert.NotNull(receipt);
        Assert.Throws<InvalidOperationException>(() => store.TryInspectOperation(id, "probe:0:0.2:True", out _));
        Assert.False(store.BeginOperation(id, "probe:1:0.2:True"));
    }

}
#endif
