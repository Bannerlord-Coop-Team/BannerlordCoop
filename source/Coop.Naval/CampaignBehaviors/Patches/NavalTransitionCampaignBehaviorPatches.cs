using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(NavalTransitionCampaignBehavior))]
internal class NavalTransitionCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(NavalTransitionCampaignBehavior.wait_here_on_condition))]
    [HarmonyPostfix]
    public static void WaitHereOnConditionPostfix(ref bool __result)
    {
        __result = false;
    }

    // Leave through the server like the land leave button, the reply finishes the encounter at sea
    [HarmonyPatch(nameof(NavalTransitionCampaignBehavior.set_sail_consequence))]
    [HarmonyPrefix]
    public static bool SetSailConsequencePrefix()
    {
        var party = MobileParty.MainParty;
        MessageBroker.Instance.Publish(party, new EndSettlementEncounterAttempted(party, SettlementLeaveType.SetSail));

        return false;
    }
}
