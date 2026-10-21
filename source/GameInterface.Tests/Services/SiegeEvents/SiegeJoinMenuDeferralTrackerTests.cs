using Common.Util;
using GameInterface.Services.SiegeEvents;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

/// <summary>
/// Covers when the siege join menu deferral holds, opens, or drops a join_siege_event activation.
/// </summary>
public class SiegeJoinMenuDeferralTrackerTests
{
    private readonly SiegeJoinMenuDeferralTracker tracker = new SiegeJoinMenuDeferralTracker();
    private readonly MapEvent siege = CreateMapEvent(MapEvent.BattleTypes.Siege);
    private readonly PartyBase mainParty = ObjectHelper.SkipConstructor<PartyBase>();

    [Fact]
    public void TryDefer_MainPartyJoinNotApplied_Defers()
    {
        tracker.Arm(siege, mainParty);

        Assert.True(tracker.TryDefer(CreateEncounter(siege), mainParty));
    }

    [Fact]
    public void TryDefer_NonSiegeBattle_DoesNotDefer()
    {
        var fieldBattle = CreateMapEvent(MapEvent.BattleTypes.FieldBattle);
        tracker.Arm(fieldBattle, mainParty);

        Assert.False(tracker.TryDefer(CreateEncounter(fieldBattle), mainParty));
    }

    [Fact]
    public void TryDefer_JoinByOtherParty_DoesNotDefer()
    {
        tracker.Arm(siege, ObjectHelper.SkipConstructor<PartyBase>());

        Assert.False(tracker.TryDefer(CreateEncounter(siege), mainParty));
    }

    [Fact]
    public void TryDefer_MainPartyAlreadyJoined_DropsDeferral()
    {
        tracker.Arm(siege, mainParty);
        var encounter = CreateEncounter(siege);
        AttachToMapEvent(mainParty, siege);

        Assert.False(tracker.TryDefer(encounter, mainParty));

        // A leftover deferral would defer again once the main party is off the map event.
        mainParty._mapEventSide = null;
        Assert.False(tracker.TryDefer(encounter, mainParty));
    }

    [Fact]
    public void Consume_SameEncounterAfterJoin_Opens()
    {
        tracker.Arm(siege, mainParty);
        var encounter = CreateEncounter(siege);
        Assert.True(tracker.TryDefer(encounter, mainParty));
        AttachToMapEvent(mainParty, siege);

        Assert.Equal(SiegeJoinMenuDeferralResult.Open, tracker.Consume(siege, encounter, mainParty));
        Assert.Equal(SiegeJoinMenuDeferralResult.None, tracker.Consume(siege, encounter, mainParty));
    }

    [Fact]
    public void Consume_MainPartyNotJoinedYet_KeepsDeferral()
    {
        tracker.Arm(siege, mainParty);
        var encounter = CreateEncounter(siege);
        Assert.True(tracker.TryDefer(encounter, mainParty));

        Assert.Equal(SiegeJoinMenuDeferralResult.None, tracker.Consume(siege, encounter, mainParty));

        AttachToMapEvent(mainParty, siege);
        Assert.Equal(SiegeJoinMenuDeferralResult.Open, tracker.Consume(siege, encounter, mainParty));
    }

    [Fact]
    public void Consume_EncounterChanged_DropsDeferral()
    {
        tracker.Arm(siege, mainParty);
        Assert.True(tracker.TryDefer(CreateEncounter(siege), mainParty));
        var changedEncounter = CreateEncounter(siege);
        AttachToMapEvent(mainParty, siege);

        Assert.Equal(SiegeJoinMenuDeferralResult.DroppedForChangedEncounter, tracker.Consume(siege, changedEncounter, mainParty));
        Assert.Equal(SiegeJoinMenuDeferralResult.None, tracker.Consume(siege, changedEncounter, mainParty));
    }

    [Fact]
    public void Consume_ArmedButNeverDeferred_ClearsSoLaterActivationIsNotDeferred()
    {
        tracker.Arm(siege, mainParty);
        var encounter = CreateEncounter(siege);

        Assert.Equal(SiegeJoinMenuDeferralResult.None, tracker.Consume(siege, encounter, mainParty));
        Assert.False(tracker.TryDefer(encounter, mainParty));
    }

    [Fact]
    public void Consume_OtherMapEvent_KeepsDeferral()
    {
        tracker.Arm(siege, mainParty);
        var encounter = CreateEncounter(siege);
        Assert.True(tracker.TryDefer(encounter, mainParty));

        Assert.Equal(SiegeJoinMenuDeferralResult.None,
            tracker.Consume(CreateMapEvent(MapEvent.BattleTypes.Siege), encounter, mainParty));

        AttachToMapEvent(mainParty, siege);
        Assert.Equal(SiegeJoinMenuDeferralResult.Open, tracker.Consume(siege, encounter, mainParty));
    }

    [Fact]
    public void Clear_DropsDeferral()
    {
        tracker.Arm(siege, mainParty);
        var encounter = CreateEncounter(siege);
        Assert.True(tracker.TryDefer(encounter, mainParty));

        tracker.Clear();
        AttachToMapEvent(mainParty, siege);

        Assert.Equal(SiegeJoinMenuDeferralResult.None, tracker.Consume(siege, encounter, mainParty));
    }

    /// <summary>Creates a bare map event of the given battle type.</summary>
    private static MapEvent CreateMapEvent(MapEvent.BattleTypes battleType)
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        mapEvent._mapEventType = battleType;
        return mapEvent;
    }

    /// <summary>Creates a player encounter on the given map event.</summary>
    private static PlayerEncounter CreateEncounter(MapEvent mapEvent)
    {
        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        encounter._mapEvent = mapEvent;
        return encounter;
    }

    /// <summary>Puts the party on a side of the map event, as the applied join does.</summary>
    private static void AttachToMapEvent(PartyBase party, MapEvent mapEvent)
    {
        party._mapEventSide = new MapEventSide(mapEvent, BattleSideEnum.Attacker, party);
    }
}
