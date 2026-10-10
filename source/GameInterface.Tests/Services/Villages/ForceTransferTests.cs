using Common.Messaging;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Interfaces;
using System;
using System.Collections.Generic;
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
        var delta = Delta((1u, 8));

        Assert.True(VillageHostileActionInterface.TryValidateVolunteersTake(1u, 8, delta, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateVolunteersTake_OverTake_Rejects()
    {
        var delta = Delta((1u, 9));

        Assert.False(VillageHostileActionInterface.TryValidateVolunteersTake(1u, 8, delta, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidateVolunteersTake_NegativeDelta_AllowsAbandoningOwnTroops()
    {
        var delta = new TroopRosterData(new[]
        {
            new TroopRosterElementData(1u, 8, 0, 0),
            new TroopRosterElementData(1u, -2, 0, 0),
        });

        Assert.True(VillageHostileActionInterface.TryValidateVolunteersTake(1u, 8, delta, out _));
    }

    [Fact]
    public void TryValidateVolunteersTake_MissingPool_Rejects()
    {
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersTake(0, 0, Delta((1u, 1)), out _));
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersTake(1u, 0, Delta((1u, 1)), out _));
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
    public void ForceTransferPool_StaysConsumableUntilConsumed()
    {
        // An earned pool stays valid until consumed one-shot: peek and consume
        // succeed, and a second consume fails.
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        var pool = subject.AuthorizeForceTransfer(
            VillageHostileAction.ForceVolunteers, "party", "settlement", null, "imperial_recruit", 8);

        Assert.True(subject.TryPeekForceTransfer(pool.RequestId, "party", out _));
        Assert.True(subject.TryConsumeForceTransfer(pool.RequestId, "party", out _));
        Assert.False(subject.TryConsumeForceTransfer(pool.RequestId, "party", out _));
    }

    [Fact]
    public void ForceTransferPool_SecondAuthorizeForSameParty_SupersedesFirst()
    {
        // A party earns one transfer at a time: a newer authorization drops the
        // older unconsumed pool instead of holding it forever.
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        var first = subject.AuthorizeForceTransfer(
            VillageHostileAction.ForceVolunteers, "party", "settlement", null, "imperial_recruit", 8);
        var second = subject.AuthorizeForceTransfer(
            VillageHostileAction.ForceSupplies, "party", "settlement", new[] { Item("grain", 5) }, null, 0);

        Assert.False(subject.TryConsumeForceTransfer(first.RequestId, "party", out _));
        Assert.True(subject.TryConsumeForceTransfer(second.RequestId, "party", out _));
        Assert.False(subject.HasPendingForceTransferForParty("party"));
    }

    [Fact]
    public void ForceTransferPool_DifferentParties_Coexist()
    {
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        var first = subject.AuthorizeForceTransfer(
            VillageHostileAction.ForceVolunteers, "party-a", "settlement", null, "imperial_recruit", 8);
        var second = subject.AuthorizeForceTransfer(
            VillageHostileAction.ForceVolunteers, "party-b", "settlement", null, "imperial_recruit", 8);

        Assert.True(subject.TryPeekForceTransfer(first.RequestId, "party-a", out _));
        Assert.True(subject.TryPeekForceTransfer(second.RequestId, "party-b", out _));
        Assert.True(subject.TryConsumeForceTransfer(first.RequestId, "party-a", out _));
        Assert.True(subject.TryConsumeForceTransfer(second.RequestId, "party-b", out _));
    }

    [Fact]
    public void ForceTransferPool_CapEviction_EvictsOldestFirst()
    {
        // 128 live pools max: authorizing beyond the cap evicts the oldest
        // entries first via a monotonic sequence number, so the newest 128
        // pools stay valid.
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        var pools = new System.Collections.Generic.List<(string requestId, string partyId)>();
        for (int i = 0; i < 133; i++)
        {
            var pool = subject.AuthorizeForceTransfer(
                VillageHostileAction.ForceVolunteers, $"party-{i}", "settlement", null, "imperial_recruit", 8);
            pools.Add((pool.RequestId, $"party-{i}"));
        }

        for (int i = 0; i < 5; i++)
        {
            Assert.False(subject.TryPeekForceTransfer(pools[i].requestId, pools[i].partyId, out _));
            Assert.False(subject.TryConsumeForceTransfer(pools[i].requestId, pools[i].partyId, out _));
        }

        int live = 0;
        for (int i = 5; i < pools.Count; i++)
        {
            if (subject.TryPeekForceTransfer(pools[i].requestId, pools[i].partyId, out _))
                live++;
        }

        Assert.Equal(128, live);
    }

    [Fact]
    public void HasPendingForceTransferForParty_TrueUntilConsumedOrSuperseded()
    {
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        Assert.False(subject.HasPendingForceTransferForParty("party"));

        var pool = subject.AuthorizeForceTransfer(
            VillageHostileAction.ForceVolunteers, "party", "settlement", null, "imperial_recruit", 8);
        Assert.True(subject.HasPendingForceTransferForParty("party"));
        Assert.False(subject.HasPendingForceTransferForParty("other-party"));

        var newer = subject.AuthorizeForceTransfer(
            VillageHostileAction.ForceSupplies, "party", "settlement", new[] { Item("grain", 5) }, null, 0);
        Assert.False(subject.TryConsumeForceTransfer(pool.RequestId, "party", out _));
        Assert.True(subject.HasPendingForceTransferForParty("party"));

        Assert.True(subject.TryConsumeForceTransfer(newer.RequestId, "party", out _));
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

        Assert.False(subject.TryTakeParkedForceTransferScreen(out _));
    }

    [Fact]
    public void DeferredForceScreen_ParkThenTake_ReturnsPoolOnce()
    {
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        var pool = DeferredPool("req-1");
        subject.ParkForceTransferScreen(pool);

        Assert.True(subject.TryTakeParkedForceTransferScreen(out var taken));
        Assert.Equal("req-1", taken.RequestId);
        Assert.False(subject.TryTakeParkedForceTransferScreen(out _));
    }

    [Fact]
    public void DeferredForceScreen_ParkReplacesExistingSlot()
    {
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        subject.ParkForceTransferScreen(DeferredPool("req-1"));
        subject.ParkForceTransferScreen(DeferredPool("req-2"));

        Assert.True(subject.TryTakeParkedForceTransferScreen(out var taken));
        Assert.Equal("req-2", taken.RequestId);
    }

    [Fact]
    public void DeferredForceScreen_ParkedPoolStaysUntilTaken()
    {
        // A pool parked while in a mission opens on the first eligible tick.
        // The server consume decides validity at Done time.
        var subject = new VillageHostileActionInterface(new MessageBroker(), new StubObjectManager());
        subject.ParkForceTransferScreen(DeferredPool("req-1"));

        Assert.True(subject.TryTakeParkedForceTransferScreen(out var taken));
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
            Array.Empty<(ItemRosterElementData, int)>(),
            Array.Empty<(string, string, int)>(),
            out _));
        Assert.True(subject.TryConsumeForceTransfer(pool.RequestId, "party", out _));
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
            1u, 8,
            Delta((1u, 8)),
            Delta((1u, -8)),
            out var error));
        Assert.Null(error);
        Assert.True(VolunteersCommit(
            1u, 8,
            Delta((1u, 5)),
            Delta((1u, -5)),
            out _));
    }

    [Fact]
    public void TryValidateVolunteersCommit_LeftPoolPhantom_Rejects()
    {
        // Gaining phantom pool troops on the dummy left side (lost on apply,
        // or fabricated).
        Assert.False(VolunteersCommit(
            1u, 8,
            EmptyDelta(),
            Delta((1u, 2)),
            out _));
    }

    [Fact]
    public void TryValidateVolunteersCommit_DismissOwnedTroop_Accepts()
    {
        // Taking the authorized recruits while moving an existing non-pool
        // troop onto the dummy left to free party room. The dummy is dropped
        // on apply, so the dismissal grants nothing; the take stays bounded.
        Assert.True(VolunteersCommit(
            1u, 8,
            Delta((1u, 8), (2u, -1)),
            Delta((1u, -8), (2u, 1)),
            out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateVolunteersCommit_SameTypeDismissOnly_Accepts()
    {
        // Dismissing one already-owned recruit of the pool type: left final is
        // pool + 1 with the matching right-side loss proving it was owned.
        Assert.True(VolunteersCommit(
            1u, 8,
            Delta((1u, -1)),
            Delta((1u, 1)),
            out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateVolunteersCommit_TakePlusSameTypeDismiss_Accepts()
    {
        // Take the 8 authorized recruits while dismissing one owned recruit of
        // the same type to free party room.
        Assert.True(VolunteersCommit(
            1u, 8,
            Delta((1u, 8), (1u, -1)),
            Delta((1u, -8), (1u, 1)),
            out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateVolunteersCommit_SameTypeUnmatchedGain_Rejects()
    {
        // Left +1 of the pool type with no matching right-side loss is a
        // fabricated pool remainder, not an owned dismissal.
        Assert.False(VolunteersCommit(
            1u, 8,
            EmptyDelta(),
            Delta((1u, 1)),
            out _));
        // Dismissing 1 owned but gaining 2 on the left still exceeds the
        // owned-dismissal allowance.
        Assert.False(VolunteersCommit(
            1u, 8,
            Delta((1u, -1)),
            Delta((1u, 2)),
            out _));
    }

    [Fact]
    public void TryValidateVolunteersCommit_TakeFromEmptyDummy_Rejects()
    {
        // A negative left delta for a troop the dummy never held.
        Assert.False(VolunteersCommit(
            1u, 8,
            EmptyDelta(),
            Delta((2u, -1)),
            out _));
    }

    [Fact]
    public void TryValidateVolunteersCommit_UpgradeGoldChange_Rejects()
    {
        // Upgrades are blocked up front while a force screen is open, so any
        // gold movement fails the commit even with recorded upgrade history.
        var upgrades = new[] { (fromId: 1u, toId: 3u, number: 1) };
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersCommit(
            1u, 8,
            Delta((1u, 7), (3u, 1)),
            Delta((1u, -8)),
            EmptyDelta(), EmptyDelta(),
            0, 0, -100, 0, 0, false, null, upgrades, out _));
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
            1u, 8,
            Delta((1u, 8)),
            Delta((1u, -8)),
            EmptyDelta(),
            EmptyDelta(),
            taken, recruited, gold, influence, morale, false, null, null, out _));
    }

    [Fact]
    public void TryValidateVolunteersCommit_PrisonerActionsOrDonation_Rejects()
    {
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersCommit(
            1u, 8,
            Delta((1u, 8)),
            Delta((1u, -8)),
            EmptyDelta(), EmptyDelta(),
            0, 0, 0, 0, 0, true, null, null, out _));
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersCommit(
            1u, 8,
            Delta((1u, 8)),
            Delta((1u, -8)),
            EmptyDelta(), EmptyDelta(),
            0, 0, 0, 0, 0, false, "town_ES1", null, out _));
    }

    [Fact]
    public void TryValidateVolunteersCommit_PrisonerDeltas_RejectsGains()
    {
        // Taking prisoners through the volunteers screen is rejected.
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersCommit(
            1u, 8,
            Delta((1u, 8)),
            Delta((1u, -8)),
            EmptyDelta(),
            Delta((1u, 1)),
            0, 0, 0, 0, 0, false, null, null, out _));
        // A non-empty left prisoner delta is rejected.
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersCommit(
            1u, 8,
            Delta((1u, 8)),
            Delta((1u, -8)),
            Delta((1u, -1)),
            EmptyDelta(),
            0, 0, 0, 0, 0, false, null, null, out _));
    }

    [Fact]
    public void TryValidateVolunteersCommit_TakePlusOwnedPrisonerRelease_Rejects()
    {
        // Exact finding-1 repro: take the village recruits while releasing an
        // already-owned ordinary prisoner. The screen must block this up front;
        // the commit rejects it as backstop.
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersCommit(
            1u, 8,
            Delta((1u, 8)),
            Delta((1u, -8)),
            Delta((2u, -1)),
            EmptyDelta(),
            1, 0, 0, 0, 0, true, null, null, out _));
    }

    [Fact]
    public void TryValidateVolunteersCommit_TakePlusPrisonerRecruit_Rejects()
    {
        // Take the village recruits while recruiting an eligible existing
        // prisoner: the recruited gain and history fail the commit backstop.
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersCommit(
            1u, 8,
            Delta((1u, 8), (2u, 1)),
            Delta((1u, -8)),
            EmptyDelta(), EmptyDelta(),
            0, 1, 0, 0, 0, false, null, null, out _));
    }
    private static bool VolunteersCommit(
        uint troopId, int count, TroopRosterData rightDelta, TroopRosterData leftDelta, out string error)
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
            new TroopRosterElementData(1u, 8, 0, 0),
            new TroopRosterElementData(2u, 1, 0, 0),
        });

        Assert.False(VillageHostileActionInterface.TryValidateVolunteersTake(
            1u, 8, delta, out _));
    }

    [Fact]
    public void TryValidateVolunteersTake_UpgradeCoveredGain_Accepts()
    {
        var delta = new TroopRosterData(new[]
        {
            new TroopRosterElementData(1u, 7, 0, 0),
            new TroopRosterElementData(3u, 1, 0, 0),
        });
        var upgrades = new[] { (fromId: 1u, toId: 3u, number: 1) };

        Assert.True(VillageHostileActionInterface.TryValidateVolunteersTake(
            1u, 8, delta, out _, upgrades));
    }

    [Fact]
    public void TryValidateVolunteersCommit_OtherTroopGain_Rejects()
    {
        Assert.False(VillageHostileActionInterface.TryValidateVolunteersCommit(
            1u, 8,
            Delta((1u, 8), (2u, 1)),
            Delta((1u, -8)),
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
            Array.Empty<(ItemRosterElementData, int)>(),
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
            Array.Empty<(ItemRosterElementData, int)>(),
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
            Array.Empty<(ItemRosterElementData, int)>(),
            Array.Empty<(string, string, int)>(),
            out _));
    }

    [Fact]
    public void TryValidateSuppliesTakeAndRemainder_TakePlusOwnedDiscard_Accepts()
    {
        // Pool of 5 grain: take 3, discard 1 owned sword into the dummy. The
        // left remainder holds 2 grain plus the discarded sword; crediting the
        // sold sword keeps the net pool outflow at 5 grain.
        var pool = new[] { Item("grain", 5) };

        Assert.True(VillageHostileActionInterface.TryValidateSuppliesTakeAndRemainder(
            pool,
            new[] { (Item("grain", 3), 0) },
            new[] { (Item("sword", 1), 0) },
            new[] { ("grain", (string)null, 2), ("sword", (string)null, 1) },
            out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateSuppliesTakeAndRemainder_DiscardOwnedPoolItem_Accepts()
    {
        // Same-key discard: take 3 pool grain, discard 2 owned grain. Net pool
        // outflow is still 5.
        var pool = new[] { Item("grain", 5) };

        Assert.True(VillageHostileActionInterface.TryValidateSuppliesTakeAndRemainder(
            pool,
            new[] { (Item("grain", 3), 0) },
            new[] { (Item("grain", 2), 0) },
            new[] { ("grain", (string)null, 4) },
            out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateSuppliesTakeAndRemainder_SoldCannotMaskOverTake_Rejects()
    {
        // Fabricated sold entries cannot cover taking more than the pool: the
        // take itself is bounded independently of the net check.
        var pool = new[] { Item("grain", 5) };

        Assert.False(VillageHostileActionInterface.TryValidateSuppliesTakeAndRemainder(
            pool,
            new[] { (Item("grain", 6), 0) },
            new[] { (Item("grain", 1), 0) },
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

    private static TroopRosterData Delta(params (uint id, int number)[] entries)
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
        public bool TryGetHandle(object obj, out uint handle) { throw new NotImplementedException(); }
        public bool TryGetHandleWithLogging<T>(T obj, out uint handle) { throw new NotImplementedException(); }
        public bool TryGetObject<T>(string id, out T obj) { throw new NotImplementedException(); }
        public bool TryGetObjectWithLogging<T>(string id, out T obj) { throw new NotImplementedException(); }
        public bool TryGetObject<T>(uint handle, out T obj) { throw new NotImplementedException(); }
        public bool TryGetObjectWithLogging<T>(uint handle, out T obj) { throw new NotImplementedException(); }
        public bool AddExisting(string id, object obj) { throw new NotImplementedException(); }
        public bool AddExisting(string id, object obj, uint handle) { throw new NotImplementedException(); }
        public bool AddNewObject(object obj, out string newId) { throw new NotImplementedException(); }
        public IReadOnlyDictionary<string, uint> GetHandleMap() { throw new NotImplementedException(); }
        public void SetJoinHandleMap(IReadOnlyDictionary<string, uint> handles) { throw new NotImplementedException(); }
        public void ClearJoinHandleMap() { throw new NotImplementedException(); }
        public bool RunRegistrationTransaction(Func<bool> registerAndValidate) { throw new NotImplementedException(); }
        public bool Remove(object obj) { throw new NotImplementedException(); }
        public void Clear() { throw new NotImplementedException(); }
        public string CreateNewId(object obj, string baseId) { throw new NotImplementedException(); }
        public int GetUniqueTypeId(object obj) { throw new NotImplementedException(); }
        public int EnsureNextUniqueIdAbove(object obj, int value) { throw new NotImplementedException(); }
    }
}
