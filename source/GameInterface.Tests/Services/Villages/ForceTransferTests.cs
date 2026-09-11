using Common.Messaging;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Interfaces;
using System;
using Xunit;

namespace GameInterface.Tests.Services.Villages;

public class ForceTransferTests
{
    [Theory]
    [InlineData(100f, 20)]
    [InlineData(219f, 32)]
    [InlineData(200f, 30)]
    [InlineData(0f, 20)]
    public void ComputeSuppliesRewardUnits_AppliesFloor(float hearth, int expected)
    {
        Assert.Equal(expected, VillageHostileActionInterface.ComputeSuppliesRewardUnits(hearth));
    }

    [Theory]
    [InlineData(60f, 20, 20)]
    [InlineData(30f, 20, 10)]
    [InlineData(120f, 32, 64)]
    public void ComputeSuppliesItemCount_ScalesWithProduction(float production, int rewardUnits, int expected)
    {
        Assert.Equal(expected, VillageHostileActionInterface.ComputeSuppliesItemCount(production, rewardUnits));
    }

    [Theory]
    [InlineData(219f, false, 3, 8)]
    [InlineData(219f, true, 3, 11)]
    [InlineData(30f, false, 0, 1)]
    [InlineData(0f, false, 0, 0)]
    public void ComputeVolunteerCount_RoundsUpHearth(float hearth, bool hasPerk, int notables, int expected)
    {
        Assert.Equal(expected, VillageHostileActionInterface.ComputeVolunteerCount(hearth, hasPerk, notables));
    }

    [Fact]
    public void TryValidateSuppliesTake_ExactTake_Accepts()
    {
        var pool = new[] { Item("grain", 5), Item("fish", 3) };
        var bought = new[] { (Item("grain", 5), 0), (Item("fish", 2), 0) };

        Assert.True(VillageHostileActionInterface.TryValidateSuppliesTake(pool, bought, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateSuppliesTake_OverTake_Rejects()
    {
        var pool = new[] { Item("grain", 5) };
        var bought = new[] { (Item("grain", 6), 0) };

        Assert.False(VillageHostileActionInterface.TryValidateSuppliesTake(pool, bought, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidateSuppliesTake_SplitEntriesSum_RejectsWhenOver()
    {
        var pool = new[] { Item("grain", 5) };
        var bought = new[] { (Item("grain", 3), 0), (Item("grain", 3), 0) };

        Assert.False(VillageHostileActionInterface.TryValidateSuppliesTake(pool, bought, out _));
    }

    [Fact]
    public void TryValidateSuppliesTake_UnknownItem_Rejects()
    {
        var pool = new[] { Item("grain", 5) };
        var bought = new[] { (Item("iron", 1), 0) };

        Assert.False(VillageHostileActionInterface.TryValidateSuppliesTake(pool, bought, out _));
    }

    [Fact]
    public void TryValidateSuppliesTake_NoTake_Accepts()
    {
        var pool = new[] { Item("grain", 5) };

        Assert.True(VillageHostileActionInterface.TryValidateSuppliesTake(pool, Array.Empty<(ItemRosterElementData, int)>(), out _));
    }

    [Fact]
    public void TryValidateVolunteersTake_ExactTake_Accepts()
    {
        var delta = Delta(("imperial_recruit", 8));

        Assert.True(VillageHostileActionInterface.TryValidateVolunteersTake("imperial_recruit", 8, delta, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateVolunteersTake_OverTake_Rejects()
    {
        var delta = Delta(("imperial_recruit", 9));

        Assert.False(VillageHostileActionInterface.TryValidateVolunteersTake("imperial_recruit", 8, delta, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidateVolunteersTake_NegativeDelta_AllowsAbandoningOwnTroops()
    {
        var delta = new TroopRosterData(new[]
        {
            new TroopRosterElementData("imperial_recruit", 8, 0, 0),
            new TroopRosterElementData("imperial_recruit", -2, 0, 0),
        });

        Assert.True(VillageHostileActionInterface.TryValidateVolunteersTake("imperial_recruit", 8, delta, out _));
    }

    [Fact]
    public void TryValidateVolunteersTake_MissingPool_Rejects()
    {
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersTake(null, 0, Delta(("imperial_recruit", 1)), out _));
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersTake("imperial_recruit", 0, Delta(("imperial_recruit", 1)), out _));
    }

    [Fact]
    public void IsForceTransferPoolValid_SuppliesNeedsItems()
    {
        Assert.True(VillageHostileActionInterface.IsForceTransferPoolValid(
            VillageHostileAction.ForceSupplies, new[] { Item("grain", 5) }, null, 0));
        Assert.False(VillageHostileActionInterface.IsForceTransferPoolValid(
            VillageHostileAction.ForceSupplies, null, null, 0));
        Assert.False(VillageHostileActionInterface.IsForceTransferPoolValid(
            VillageHostileAction.ForceSupplies, Array.Empty<ItemRosterElementData>(), null, 0));
        Assert.False(VillageHostileActionInterface.IsForceTransferPoolValid(
            VillageHostileAction.ForceSupplies, new[] { Item("grain", 0) }, null, 0));
    }

    [Fact]
    public void IsForceTransferPoolValid_VolunteersNeedsTroopAndCount()
    {
        Assert.True(VillageHostileActionInterface.IsForceTransferPoolValid(
            VillageHostileAction.ForceVolunteers, null, "imperial_recruit", 8));
        Assert.False(VillageHostileActionInterface.IsForceTransferPoolValid(
            VillageHostileAction.ForceVolunteers, null, "imperial_recruit", 0));
        Assert.False(VillageHostileActionInterface.IsForceTransferPoolValid(
            VillageHostileAction.ForceVolunteers, null, null, 8));
    }

    [Fact]
    public void ForceTransferPool_ConsumeOnce_SecondConsumeFails()
    {
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        var pool = subject.AuthorizeForceTransfer(
            VillageHostileAction.ForceVolunteers, "party", "settlement", null, "imperial_recruit", 8);

        Assert.True(subject.TryConsumeForceTransfer(pool.RequestId, "party", out var consumed));
        Assert.Equal(pool.RequestId, consumed.RequestId);
        Assert.False(subject.TryConsumeForceTransfer(pool.RequestId, "party", out _));
    }

    [Fact]
    public void ForceTransferPool_WrongParty_ConsumeFailsAndPreservesEntry()
    {
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        var pool = subject.AuthorizeForceTransfer(
            VillageHostileAction.ForceVolunteers, "party", "settlement", null, "imperial_recruit", 8);

        Assert.False(subject.TryConsumeForceTransfer(pool.RequestId, "other-party", out _));
        Assert.True(subject.TryConsumeForceTransfer(pool.RequestId, "party", out _));
    }

    [Fact]
    public void ForceTransferPool_Expired_PruneDropsIt()
    {
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        var pool = subject.AuthorizeForceTransfer(
            VillageHostileAction.ForceVolunteers, "party", "settlement", null, "imperial_recruit", 8);

        subject.PruneExpiredForceTransfers(DateTime.UtcNow + TimeSpan.FromMinutes(6));

        Assert.False(subject.TryConsumeForceTransfer(pool.RequestId, "party", out _));
    }

    [Fact]
    public void ForceTransferPool_BeforeExpiry_PruneKeepsIt()
    {
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        var pool = subject.AuthorizeForceTransfer(
            VillageHostileAction.ForceVolunteers, "party", "settlement", null, "imperial_recruit", 8);

        subject.PruneExpiredForceTransfers(DateTime.UtcNow + TimeSpan.FromMinutes(4));

        Assert.True(subject.TryConsumeForceTransfer(pool.RequestId, "party", out _));
    }

    [Fact]
    public void HasPendingForceTransferForParty_TrueUntilConsumedOrExpired()
    {
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        Assert.False(subject.HasPendingForceTransferForParty("party"));

        var pool = subject.AuthorizeForceTransfer(
            VillageHostileAction.ForceVolunteers, "party", "settlement", null, "imperial_recruit", 8);
        Assert.True(subject.HasPendingForceTransferForParty("party"));
        Assert.False(subject.HasPendingForceTransferForParty("other-party"));

        Assert.True(subject.TryConsumeForceTransfer(pool.RequestId, "party", out _));
        Assert.False(subject.HasPendingForceTransferForParty("party"));
    }

    [Fact]
    public void ForceTransferPool_UnknownRequest_ConsumeFails()
    {
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());

        Assert.False(subject.TryConsumeForceTransfer("missing", "party", out _));
    }

    [Fact]
    public void DeferredForceScreen_TakeEmpty_ReturnsFalse()
    {
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());

        Assert.False(subject.TryTakeParkedForceTransferScreen(DateTime.UtcNow, out _, out _));
    }

    [Fact]
    public void DeferredForceScreen_ParkThenTake_ReturnsPoolOnce()
    {
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        var pool = DeferredPool("req-1");
        subject.ParkForceTransferScreen(pool);

        Assert.True(subject.TryTakeParkedForceTransferScreen(DateTime.UtcNow, out var taken, out _));
        Assert.Equal("req-1", taken.RequestId);
        Assert.False(subject.TryTakeParkedForceTransferScreen(DateTime.UtcNow, out _, out _));
    }

    [Fact]
    public void DeferredForceScreen_ParkReplacesExistingSlot()
    {
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        subject.ParkForceTransferScreen(DeferredPool("req-1"));
        subject.ParkForceTransferScreen(DeferredPool("req-2"));

        Assert.True(subject.TryTakeParkedForceTransferScreen(DateTime.UtcNow, out var taken, out _));
        Assert.Equal("req-2", taken.RequestId);
    }

    [Fact]
    public void DeferredForceScreen_ExpiredTake_ReturnsFalseAndClears()
    {
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        var start = DateTime.UtcNow;
        subject.ParkForceTransferScreen(DeferredPool("req-1"), start);

        Assert.False(subject.TryTakeParkedForceTransferScreen(start + TimeSpan.FromMinutes(6), out _, out _));
        Assert.False(subject.TryTakeParkedForceTransferScreen(start + TimeSpan.FromMinutes(6), out _, out _));
    }

    [Fact]
    public void DeferredForceScreen_BeforeExpiry_TakeSucceeds()
    {
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        var start = DateTime.UtcNow;
        subject.ParkForceTransferScreen(DeferredPool("req-1"), start);

        Assert.True(subject.TryTakeParkedForceTransferScreen(start + TimeSpan.FromMinutes(4), out var taken, out _));
        Assert.Equal("req-1", taken.RequestId);
    }

    [Fact]
    public void ForceTransferPool_PeekDoesNotConsume_RejectPreservesPool()
    {
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        var pool = subject.AuthorizeForceTransfer(
            VillageHostileAction.ForceSupplies, "party", "settlement",
            new[] { Item("grain", 5) }, null, 0);

        Assert.True(subject.TryPeekForceTransfer(pool.RequestId, "party", out var peeked));
        Assert.Equal(pool.RequestId, peeked.RequestId);
        Assert.False(subject.TryPeekForceTransfer(pool.RequestId, "other-party", out _));
        Assert.False(subject.TryPeekForceTransfer("missing", "party", out _));

        // A failed validation is pure: the pool survives for the consume.
        Assert.False(VillageHostileActionInterface.TryValidateSuppliesTakeAndRemainder(
            peeked.SuppliesItems,
            new[] { (Item("grain", 6), 0) },
            Array.Empty<(string, string, int)>(),
            out _));
        Assert.True(subject.TryConsumeForceTransfer(pool.RequestId, "party", out _));
    }

    [Fact]
    public void IsParkedPoolFresh_EnforcesDrainMargin()
    {
        var start = DateTime.UtcNow;

        Assert.True(VillageHostileActionInterface.IsParkedPoolFresh(start, start + TimeSpan.FromMinutes(3)));
        Assert.True(VillageHostileActionInterface.IsParkedPoolFresh(start, start + TimeSpan.FromMinutes(4)));
        Assert.False(VillageHostileActionInterface.IsParkedPoolFresh(start, start + TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(1))));
        Assert.False(VillageHostileActionInterface.IsParkedPoolFresh(start, start + TimeSpan.FromMinutes(6)));
    }

    private static ForceTransferPoolData DeferredPool(string requestId)
    {
        return new ForceTransferPoolData(
            VillageHostileAction.ForceSupplies,
            "party",
            "settlement",
            requestId,
            Array.Empty<ItemRosterElementData>(),
            null,
            0);
    }

    [Fact]
    public void TryValidateSuppliesLeftRemainder_PartialOrFullRemainder_Accepts()
    {
        var pool = new[] { Item("grain", 5) };

        Assert.True(VillageHostileActionInterface.TryValidateSuppliesLeftRemainder(
            pool, new[] { ("grain", (string)null, 2) }, out var error));
        Assert.Null(error);
        Assert.True(VillageHostileActionInterface.TryValidateSuppliesLeftRemainder(
            pool, new[] { ("grain", (string)null, 5) }, out _));
        Assert.True(VillageHostileActionInterface.TryValidateSuppliesLeftRemainder(
            pool, Array.Empty<(string, string, int)>(), out _));
        Assert.True(VillageHostileActionInterface.TryValidateSuppliesLeftRemainder(
            pool, null, out _));
    }

    [Fact]
    public void TryValidateSuppliesLeftRemainder_ExcessOrUnknown_Rejects()
    {
        var pool = new[] { Item("grain", 5) };

        Assert.False(VillageHostileActionInterface.TryValidateSuppliesLeftRemainder(
            pool, new[] { ("grain", (string)null, 6) }, out _));
        Assert.False(VillageHostileActionInterface.TryValidateSuppliesLeftRemainder(
            pool, new[] { ("iron", (string)null, 1) }, out _));
        Assert.False(VillageHostileActionInterface.TryValidateSuppliesLeftRemainder(
            pool, new[] { ("grain", (string)null, -1) }, out _));
    }

    [Fact]
    public void TryValidateSuppliesLeftRemainder_ModifierMismatch_Rejects()
    {
        var pool = new[] { Item("grain", 5) };

        Assert.False(VillageHostileActionInterface.TryValidateSuppliesLeftRemainder(
            pool, new[] { ("grain", "shiny", 1) }, out _));
    }

    [Fact]
    public void TryValidateSuppliesLeftRemainder_ModifiedPool_AcceptsMatchingModifier()
    {
        var pool = new[] { new ItemRosterElementData(new ItemObjectData("grain", "shiny", false), 5) };

        Assert.True(VillageHostileActionInterface.TryValidateSuppliesLeftRemainder(
            pool, new[] { ("grain", "shiny", 5) }, out _));
        Assert.False(VillageHostileActionInterface.TryValidateSuppliesLeftRemainder(
            pool, new[] { ("grain", (string)null, 5) }, out _));
    }

    [Fact]
    public void TryValidateVolunteersCommit_FullAndPartialTake_Accepts()
    {
        Assert.True(VolunteersCommit(
            "imperial_recruit", 8,
            Delta(("imperial_recruit", 8)),
            Delta(("imperial_recruit", -8)),
            out var error));
        Assert.Null(error);
        Assert.True(VolunteersCommit(
            "imperial_recruit", 8,
            Delta(("imperial_recruit", 5)),
            Delta(("imperial_recruit", -5)),
            out _));
    }

    [Fact]
    public void TryValidateVolunteersCommit_LeftRemainderOutsidePool_Rejects()
    {
        // Gaining phantom troops on the dummy left side (lost on apply, or fabricated).
        Assert.False(VolunteersCommit(
            "imperial_recruit", 8,
            EmptyDelta(),
            Delta(("imperial_recruit", 2)),
            out _));
        Assert.False(VolunteersCommit(
            "imperial_recruit", 8,
            EmptyDelta(),
            Delta(("vlandian_recruit", 1)),
            out _));
    }

    [Theory]
    [InlineData(1, 0, 0, 0, 0)] // taken prisoners
    [InlineData(0, 1, 0, 0, 0)] // recruited prisoners
    [InlineData(0, 0, 100, 0, 0)] // gold
    [InlineData(0, 0, 0, 1, 0)] // influence
    [InlineData(0, 0, 0, 0, 1)] // morale
    public void TryValidateVolunteersCommit_SideChannels_Reject(
        int taken, int recruited, int gold, int influence, int morale)
    {
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersCommit(
            "imperial_recruit", 8,
            Delta(("imperial_recruit", 8)),
            Delta(("imperial_recruit", -8)),
            EmptyDelta(),
            EmptyDelta(),
            taken, recruited, gold, influence, morale, false, null, null, out _));
    }

    [Fact]
    public void TryValidateVolunteersCommit_PrisonerActionsOrDonation_Rejects()
    {
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersCommit(
            "imperial_recruit", 8,
            Delta(("imperial_recruit", 8)),
            Delta(("imperial_recruit", -8)),
            EmptyDelta(), EmptyDelta(),
            0, 0, 0, 0, 0, true, null, null, out _));
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersCommit(
            "imperial_recruit", 8,
            Delta(("imperial_recruit", 8)),
            Delta(("imperial_recruit", -8)),
            EmptyDelta(), EmptyDelta(),
            0, 0, 0, 0, 0, false, "town_ES1", null, out _));
    }

    [Fact]
    public void TryValidateVolunteersCommit_PrisonerDeltas_RejectsGains()
    {
        // Taking prisoners through the volunteers screen is rejected.
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersCommit(
            "imperial_recruit", 8,
            Delta(("imperial_recruit", 8)),
            Delta(("imperial_recruit", -8)),
            EmptyDelta(),
            Delta(("imperial_recruit", 1)),
            0, 0, 0, 0, 0, false, null, null, out _));
        // A non-empty left prisoner delta is rejected.
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersCommit(
            "imperial_recruit", 8,
            Delta(("imperial_recruit", 8)),
            Delta(("imperial_recruit", -8)),
            Delta(("imperial_recruit", -1)),
            EmptyDelta(),
            0, 0, 0, 0, 0, false, null, null, out _));
    }

    private static bool VolunteersCommit(
        string troopId, int count, TroopRosterData rightDelta, TroopRosterData leftDelta, out string error)
    {
        return VillageHostileActionInterface.TryValidateVolunteersCommit(
            troopId, count, rightDelta, leftDelta,
            EmptyDelta(), EmptyDelta(),
            0, 0, 0, 0, 0, false, null, null, out error);
    }

    [Fact]
    public void TryValidateVolunteersTake_OtherTroopGain_Rejects()
    {
        var delta = new TroopRosterData(new[]
        {
            new TroopRosterElementData("imperial_recruit", 8, 0, 0),
            new TroopRosterElementData("vlandian_recruit", 1, 0, 0),
        });

        Assert.False(VillageHostileActionInterface.TryValidateVolunteersTake(
            "imperial_recruit", 8, delta, out _));
    }

    [Fact]
    public void TryValidateVolunteersTake_UpgradeCoveredGain_Accepts()
    {
        var delta = new TroopRosterData(new[]
        {
            new TroopRosterElementData("imperial_recruit", 7, 0, 0),
            new TroopRosterElementData("imperial_sergeant", 1, 0, 0),
        });
        var upgrades = new[] { (fromId: "imperial_recruit", toId: "imperial_sergeant", number: 1) };

        Assert.True(VillageHostileActionInterface.TryValidateVolunteersTake(
            "imperial_recruit", 8, delta, out _, upgrades));
    }

    [Fact]
    public void TryValidateVolunteersCommit_OtherTroopGain_Rejects()
    {
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersCommit(
            "imperial_recruit", 8,
            Delta(("imperial_recruit", 8), ("vlandian_recruit", 1)),
            Delta(("imperial_recruit", -8)),
            EmptyDelta(), EmptyDelta(),
            0, 0, 0, 0, 0, false, null, null, out _));
    }

    [Fact]
    public void TryValidateSuppliesTakeAndRemainder_ExactSplit_Accepts()
    {
        var pool = new[] { Item("grain", 5) };

        Assert.True(VillageHostileActionInterface.TryValidateSuppliesTakeAndRemainder(
            pool,
            new[] { (Item("grain", 3), 0) },
            new[] { ("grain", (string)null, 2) },
            out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateSuppliesTakeAndRemainder_DuplicatedTake_Rejects()
    {
        // Take of pool plus remainder of pool passes the independent checks but
        // must fail jointly.
        var pool = new[] { Item("grain", 5) };

        Assert.False(VillageHostileActionInterface.TryValidateSuppliesTakeAndRemainder(
            pool,
            new[] { (Item("grain", 5), 0) },
            new[] { ("grain", (string)null, 5) },
            out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidateSuppliesTakeAndRemainder_UnknownItem_Rejects()
    {
        var pool = new[] { Item("grain", 5) };

        Assert.False(VillageHostileActionInterface.TryValidateSuppliesTakeAndRemainder(
            pool,
            new[] { (Item("iron", 1), 0) },
            Array.Empty<(string, string, int)>(),
            out _));
    }

    private static TroopRosterData EmptyDelta()
    {
        return new TroopRosterData(Array.Empty<TroopRosterElementData>());
    }

    private static ItemRosterElementData Item(string id, int amount)
    {
        return new ItemRosterElementData(new ItemObjectData(id, null, true), amount);
    }

    private static TroopRosterData Delta(params (string id, int number)[] entries)
    {
        var data = new TroopRosterElementData[entries.Length];
        for (int i = 0; i < entries.Length; i++)
            data[i] = new TroopRosterElementData(entries[i].id, entries[i].number, 0, 0);
        return new TroopRosterData(data);
    }

    private sealed class StubObjectManager : IObjectManager
    {
        public bool Contains(object obj) { throw new NotImplementedException(); }
        public bool Contains(string id) { throw new NotImplementedException(); }
        public bool TryGetId(object obj, out string id) { throw new NotImplementedException(); }
        public bool TryGetIdWithLogging<T>(T obj, out string id) { throw new NotImplementedException(); }
        public bool TryGetObject<T>(string id, out T obj) { throw new NotImplementedException(); }
        public bool TryGetObjectWithLogging<T>(string id, out T obj) { throw new NotImplementedException(); }
        public bool AddExisting(string id, object obj) { throw new NotImplementedException(); }
        public bool AddNewObject(object obj, out string newId) { throw new NotImplementedException(); }
        public bool RunRegistrationTransaction(Func<bool> registerAndValidate) { throw new NotImplementedException(); }
        public bool Remove(object obj) { throw new NotImplementedException(); }
        public void Clear() { throw new NotImplementedException(); }
        public string CreateNewId(object obj, string baseId) { throw new NotImplementedException(); }
        public int GetUniqueTypeId(object obj) { throw new NotImplementedException(); }
        public int EnsureNextUniqueIdAbove(object obj, int value) { throw new NotImplementedException(); }
    }
}
