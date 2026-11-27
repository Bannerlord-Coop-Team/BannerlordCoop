using Common;
using Common.Messaging;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Only the server decides which parties a diplomatic change pulls out of a siege, and those leaves replicate.
/// Clients re-fire this check when they apply a kingdom, stance or owner change, and a client copy that differs
/// would lift sieges the server keeps. Vanilla also gives the main party its "no longer enemies" menu here,
/// which the server has no main party for, so it announces each player party the check pulled out.
/// </summary>
[HarmonyPatch(typeof(PartyDiplomaticHandlerCampaignBehavior), nameof(PartyDiplomaticHandlerCampaignBehavior.CheckSiegeEventContinuity))]
internal class SiegeDiplomaticLeavePatches
{
    private sealed class PlayerSiegeParties
    {
        public MobileParty[] Besiegers { get; }
        public MobileParty[] Defenders { get; }

        public PlayerSiegeParties(MobileParty[] besiegers, MobileParty[] defenders)
        {
            Besiegers = besiegers;
            Defenders = defenders;
        }
    }

    [HarmonyPrefix]
    private static bool Prefix(SiegeEvent siegeEvent, out PlayerSiegeParties __state)
    {
        __state = null;
        if (ModInformation.IsClient) return false;

        // Player parties failing vanilla's join checks, read before vanilla removes anyone. The postfix keeps the
        // ones that actually left.
        var settlement = siegeEvent.BesiegedSettlement;
        var failedBesiegers = siegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType(siegeEvent.GetCurrentBattleType())
            .Where(party => party.IsPlayerParty() && !siegeEvent.CanPartyJoinSide(party, BattleSideEnum.Attacker))
            .Select(party => party.MobileParty)
            .ToArray();
        var failedDefenders = settlement.Parties
            .Where(defender => defender.Party.IsPlayerParty() &&
                settlement.MapFaction != defender.MapFaction &&
                !siegeEvent.CanPartyJoinSide(defender.Party, BattleSideEnum.Defender))
            .ToArray();

        __state = new PlayerSiegeParties(failedBesiegers, failedDefenders);
        return true;
    }

    [HarmonyPostfix]
    private static void Postfix(SiegeEvent siegeEvent, PlayerSiegeParties __state)
    {
        if (__state == null) return;

        var settlement = siegeEvent.BesiegedSettlement;
        foreach (var party in __state.Besiegers)
        {
            if (party.BesiegerCamp == siegeEvent.BesiegerCamp) continue;

            MessageBroker.Instance.Publish(party, new PartyLeftSiegeByDiplomacy(party, settlement.MapFaction));
        }

        foreach (var party in __state.Defenders)
        {
            // Vanilla only evicts settlement parties while besiegers remain, and names the camp leader's faction.
            if (party.CurrentSettlement == settlement) continue;

            MessageBroker.Instance.Publish(party,
                new PartyLeftSiegeByDiplomacy(party, siegeEvent.BesiegerCamp.LeaderParty.MapFaction));
        }
    }
}
