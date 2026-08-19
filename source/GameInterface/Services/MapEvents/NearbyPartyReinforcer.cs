using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MobileParties.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
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
}

/// <summary>Applies nearby AI reinforcements to open player battles on the server.</summary>
internal sealed class NearbyPartyReinforcer : INearbyPartyReinforcer
{
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
        var mapEvents = Campaign.Current?.MapEventManager?.MapEvents;
        if (mapEvents == null)
            return;

        foreach (var mapEvent in mapEvents.ToArray())
            Reinforce(mapEvent);
    }

    internal void Reinforce(
        MapEvent mapEvent,
        Action<List<MobileParty>, List<MobileParty>> selectJoiners,
        Action<MapEventSide, MobileParty> addJoiner)
    {
        if (!CanReinforce(mapEvent) || !TryGetPlayerParty(mapEvent, out var playerParty, out var playerSide))
            return;

        var enemySide = playerSide.GetOppositeSide();
        var playerParties = CollectMobileParties(mapEvent, playerSide);
        var enemyParties = CollectMobileParties(mapEvent, enemySide);
        int playerPartyCount = playerParties.Count;
        int enemyPartyCount = enemyParties.Count;

        SelectJoiners(mapEvent, playerParty, playerSide, playerParties, enemyParties, selectJoiners);

        AddJoiners(mapEvent.GetMapEventSide(playerSide), playerParties, playerPartyCount, addJoiner);
        AddJoiners(mapEvent.GetMapEventSide(enemySide), enemyParties, enemyPartyCount, addJoiner);
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
