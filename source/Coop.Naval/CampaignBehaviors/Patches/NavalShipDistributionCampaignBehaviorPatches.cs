using Common;
using Common.Messaging;
using Coop.Naval.Notifications.Messages;
using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(NavalShipDistributionCampaignBehavior))]
internal class NavalShipDistributionCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(NavalShipDistributionCampaignBehavior.OnPartyDisbanded))]
    [HarmonyPrefix]
    public static bool OnPartyDisbandedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalShipDistributionCampaignBehavior.OnMobilePartyDestroyed))]
    [HarmonyPrefix]
    public static bool OnMobilePartyDestroyedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalShipDistributionCampaignBehavior.RecoverGoldFromRemainingShipsAfterDistribution))]
    [HarmonyPrefix]
    public static bool RecoverGoldFromRemainingShipsAfterDistributionPrefix(MobileParty party)
    {
        // Non-player clans use the vanilla path
        if (!party.ActualClan.IsPlayerClan()) return true;

        // Replace Clan.PlayerClan usage
        Hero clanLeader = party.ActualClan.Leader;
        if (clanLeader == null || !clanLeader.IsActive || party.Ships.Count <= 0) return false;

        var shipCostModel = Campaign.Current.Models.ShipCostModel;
        int gold = (int)party.Ships.Sum(ship => shipCostModel.GetShipTradeValue(ship, party.Party, null));
        gold = (int)(gold * shipCostModel.GetShipSellingPenalty());

        if (party.Owner != null)
        {
            // Clients in this clan show vanilla's disbanded party message
            MessageBroker.Instance.Publish(party, new NotifyShipsGoldRecovered(party.ActualClan, party.Owner, gold));
        }

        GiveGoldAction.ApplyBetweenCharacters(null, clanLeader, gold, false);
        return false;
    }
}
