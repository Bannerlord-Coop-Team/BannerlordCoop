using Common.Util;
using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Control;
using Newtonsoft.Json.Linq;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Disables all map encounters/events
/// </summary>
[HarmonyPatch(typeof(EncounterManager))]
internal class EncounterManagerPatches
{
    private static AllowedInstance<MobileParty> _allowedInstance;

    [HarmonyPatch("StartPartyEncounter")]
    [HarmonyPrefix]
    private static bool StartPartyEncounterPrefix() => false;

    [HarmonyPatch("StartSettlementEncounter")]
    [HarmonyPrefix]
    private static bool StartSettlementEncounterPrefix(MobileParty attackerParty, Settlement settlement) 
    {
        if (attackerParty != MobileParty.MainParty) return false;

        MessageBroker.Instance.Publish(attackerParty, new SettlementEntered(settlement.StringId, attackerParty.StringId));

        return false;
    }

    public static void RunOriginalStartSettlementEncounter(MobileParty attackerParty, Settlement settlement)
    {

        using (_allowedInstance = new AllowedInstance<MobileParty>(attackerParty))
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                EncounterManager.StartSettlementEncounter(attackerParty, settlement);
            }, true);
        }
    }
}
