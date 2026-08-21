using Common.Messaging;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.MobileParties.Extensions;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents;

/// <summary>Finds and adds AI parties selected by the native encounter model.</summary>
internal interface INearbyPartyReinforcer
{
    void Reinforce(MapEvent mapEvent);
    void ReinforceOpenPlayerBattles();
    void RemoveReinforcementsIfNoPlayers(MapEvent mapEvent, MobileParty removedParty);
}

/// <summary>Applies nearby AI reinforcements to open player battles on the server.</summary>
internal sealed class NearbyPartyReinforcer : INearbyPartyReinforcer
{
    // Campaign-time scheduling stops follow-up scans while the map is paused.
    private const float FollowUpScanIntervalHours = 0.25f;

    private sealed class FollowUpScanState
    {
        public long NextScanAtTicks { get; set; }
    }

    private readonly IMessageBroker messageBroker;
    private readonly Dictionary<MapEvent, FollowUpScanState> followUpScans = new();
    private readonly List<MapEvent> completedFollowUpScans = new();
    private long nextFollowUpScanAtTicks = long.MaxValue;

    public NearbyPartyReinforcer(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
    }

    public void Reinforce(MapEvent mapEvent)
    {
        var encounterModel = Campaign.Current?.Models?.EncounterModel;
        if (encounterModel == null)
            return;

        Reinforce(
            mapEvent,
            encounterModel.FindNonAttachedNpcPartiesWhoWillJoinPlayerEncounter,
            (side, party) => side.AddNearbyPartyToPlayerMapEvent(party));
    }

    public void ReinforceOpenPlayerBattles()
    {
        long currentTicks = CampaignTime.Now.NumTicks;
        if (currentTicks < nextFollowUpScanAtTicks)
            return;

        var encounterModel = Campaign.Current?.Models?.EncounterModel;
        if (encounterModel == null)
        {
            nextFollowUpScanAtTicks = currentTicks + FollowUpScanIntervalTicks;
            return;
        }

        ReinforceOpenPlayerBattles(
            currentTicks,
            encounterModel.FindNonAttachedNpcPartiesWhoWillJoinPlayerEncounter,
            (side, party) => side.AddNearbyPartyToPlayerMapEvent(party));
    }

    internal void ReinforceOpenPlayerBattles(
        long currentTicks,
        Action<List<MobileParty>, List<MobileParty>> selectJoiners,
        Action<MapEventSide, MobileParty> addJoiner)
    {
        if (currentTicks < nextFollowUpScanAtTicks)
            return;

        completedFollowUpScans.Clear();
        nextFollowUpScanAtTicks = long.MaxValue;

        foreach (var pair in followUpScans)
        {
            var mapEvent = pair.Key;
            var state = pair.Value;
            if (!CanReinforce(mapEvent))
            {
                completedFollowUpScans.Add(mapEvent);
                continue;
            }

            bool scanIsDue = IsFollowUpScanDue(mapEvent, currentTicks);
            if (scanIsDue)
                state.NextScanAtTicks = currentTicks + FollowUpScanIntervalTicks;

            nextFollowUpScanAtTicks = Math.Min(nextFollowUpScanAtTicks, state.NextScanAtTicks);

            if (scanIsDue)
                ReinforceCore(mapEvent, selectJoiners, addJoiner);
        }

        foreach (var mapEvent in completedFollowUpScans)
            followUpScans.Remove(mapEvent);
    }

    internal static long FollowUpScanIntervalTicks => CampaignTime.Hours(FollowUpScanIntervalHours).NumTicks;

    public void RemoveReinforcementsIfNoPlayers(MapEvent mapEvent, MobileParty removedParty)
    {
        RemoveReinforcementsIfNoPlayers(mapEvent, removedParty, RemoveReinforcements);
    }

    internal void RemoveReinforcementsIfNoPlayers(
        MapEvent mapEvent,
        MobileParty removedParty,
        Action<MapEventSide> removeReinforcements)
    {
        if (mapEvent == null || removedParty == null)
            return;

        RemoveTrackedParty(mapEvent.AttackerSide, removedParty);
        RemoveTrackedParty(mapEvent.DefenderSide, removedParty);

        if (mapEvent.State != MapEventState.Wait
            || !removedParty.IsPlayerParty()
            || mapEvent.ContainsPlayerParty())
        {
            return;
        }

        followUpScans.Remove(mapEvent);
        RecalculateNextFollowUpScan();
        removeReinforcements(mapEvent.AttackerSide);
        removeReinforcements(mapEvent.DefenderSide);
    }

    private void RemoveReinforcements(MapEventSide side)
    {
        if (side == null || side._nearbyPartiesAddedToPlayerMapEvent.Count == 0)
            return;

        var nearbyParties = new List<MobileParty>(side._nearbyPartiesAddedToPlayerMapEvent);

        // Clear first because each authoritative removal re-enters the removal patch.
        side._nearbyPartiesAddedToPlayerMapEvent.Clear();
        foreach (var nearbyParty in nearbyParties)
        {
            if (nearbyParty?.MapEventSide != side)
                continue;

            MapEventParty removedParty = null;
            foreach (var mapEventParty in side.Parties)
            {
                if (mapEventParty.Party == nearbyParty.Party)
                {
                    removedParty = mapEventParty;
                    break;
                }
            }

            nearbyParty.MapEventSide = null;
            if (removedParty != null)
                messageBroker.Publish(side, new MapEventPartyRemoved(side, removedParty));
        }
    }

    private static void RemoveTrackedParty(MapEventSide side, MobileParty removedParty)
    {
        side?._nearbyPartiesAddedToPlayerMapEvent.Remove(removedParty);
    }

    private void RecalculateNextFollowUpScan()
    {
        nextFollowUpScanAtTicks = long.MaxValue;
        foreach (var state in followUpScans.Values)
            nextFollowUpScanAtTicks = Math.Min(nextFollowUpScanAtTicks, state.NextScanAtTicks);
    }

    internal void Reinforce(
        MapEvent mapEvent,
        Action<List<MobileParty>, List<MobileParty>> selectJoiners,
        Action<MapEventSide, MobileParty> addJoiner)
    {
        if (!CanReinforce(mapEvent))
            return;

        ScheduleFollowUpScan(mapEvent, CampaignTime.Now.NumTicks);
        ReinforceCore(mapEvent, selectJoiners, addJoiner);
    }

    internal bool IsFollowUpScanDue(MapEvent mapEvent, long currentTicks)
    {
        if (!followUpScans.TryGetValue(mapEvent, out var state) || !CanReinforce(mapEvent))
            return false;

        return currentTicks >= state.NextScanAtTicks;
    }

    private void ReinforceCore(
        MapEvent mapEvent,
        Action<List<MobileParty>, List<MobileParty>> selectJoiners,
        Action<MapEventSide, MobileParty> addJoiner)
    {
        if (!TryGetPlayerParty(mapEvent, out var playerParty, out var playerSide))
            return;

        var enemySide = playerSide.GetOppositeSide();
        var playerParties = CollectMobileParties(mapEvent, playerSide);
        var enemyParties = CollectMobileParties(mapEvent, enemySide);
        int playerPartyCount = playerParties.Count;
        int enemyPartyCount = enemyParties.Count;

        SelectJoiners(mapEvent, playerParty, playerSide, playerParties, enemyParties, selectJoiners);

        if (!CanReinforce(mapEvent))
            return;

        AddJoiners(mapEvent.GetMapEventSide(playerSide), playerParties, playerPartyCount, addJoiner);
        AddJoiners(mapEvent.GetMapEventSide(enemySide), enemyParties, enemyPartyCount, addJoiner);
    }

    private void ScheduleFollowUpScan(MapEvent mapEvent, long currentTicks)
    {
        if (!followUpScans.TryGetValue(mapEvent, out var state))
        {
            state = new FollowUpScanState();
            followUpScans.Add(mapEvent, state);
        }

        state.NextScanAtTicks = currentTicks + FollowUpScanIntervalTicks;
        nextFollowUpScanAtTicks = Math.Min(nextFollowUpScanAtTicks, state.NextScanAtTicks);
    }

    internal static bool CanReinforce(MapEvent mapEvent)
    {
        if (mapEvent == null || mapEvent.IsFinalized || mapEvent.BattleState != BattleState.None)
            return false;

        if (mapEvent.IsRaid || mapEvent.IsSiegeAssault || mapEvent.IsForcingSupplies ||
            mapEvent.IsForcingVolunteers || mapEvent.MapEventSettlement?.IsHideout == true)
            return false;

        return InteractionPatches.IsWithinAiJoinWindow(mapEvent) && mapEvent.ContainsPlayerParty();
    }

    private static void SelectJoiners(
        MapEvent mapEvent,
        MobileParty playerParty,
        BattleSideEnum playerSide,
        List<MobileParty> playerParties,
        List<MobileParty> enemyParties,
        Action<List<MobileParty>, List<MobileParty>> selectJoiners)
    {
        // Vanilla reads these encounter statics, so scope them to the actual client party without creating a host party.
        var campaign = Campaign.Current;
        var previousMainParty = campaign.MainParty;
        var previousEncounter = campaign.PlayerEncounter;
        var encounter = new PlayerEncounter
        {
            _mapEvent = mapEvent,
            EncounterSettlementAux = mapEvent.MapEventSettlement,
            PlayerSide = playerSide,
            OpponentSide = playerSide.GetOppositeSide()
        };

        try
        {
            campaign.MainParty = playerParty;
            campaign.PlayerEncounter = encounter;
            selectJoiners(playerParties, enemyParties);
        }
        finally
        {
            campaign.PlayerEncounter = previousEncounter;
            campaign.MainParty = previousMainParty;
        }
    }

    private static bool TryGetPlayerParty(
        MapEvent mapEvent,
        out MobileParty playerParty,
        out BattleSideEnum playerSide)
    {
        foreach (var side in new[] { BattleSideEnum.Attacker, BattleSideEnum.Defender })
        {
            foreach (var mapEventParty in mapEvent.PartiesOnSide(side))
            {
                var mobileParty = mapEventParty?.Party?.MobileParty;
                if (mobileParty?.IsPlayerParty() != true)
                    continue;

                playerParty = mobileParty;
                playerSide = side;
                return true;
            }
        }

        playerParty = null;
        playerSide = BattleSideEnum.None;
        return false;
    }

    private static List<MobileParty> CollectMobileParties(MapEvent mapEvent, BattleSideEnum side)
    {
        var parties = new List<MobileParty>();
        foreach (var mapEventParty in mapEvent.PartiesOnSide(side))
        {
            if (mapEventParty?.Party?.IsMobile == true)
                parties.Add(mapEventParty.Party.MobileParty);
        }

        return parties;
    }

    private static void AddJoiners(
        MapEventSide side,
        List<MobileParty> parties,
        int existingPartyCount,
        Action<MapEventSide, MobileParty> addJoiner)
    {
        if (side == null)
            return;

        for (int i = existingPartyCount; i < parties.Count; i++)
        {
            var party = parties[i];
            if (party == null || party.IsPlayerParty())
                continue;

            addJoiner(side, party);
        }
    }
}
