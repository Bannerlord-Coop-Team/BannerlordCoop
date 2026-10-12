using Common.Messaging;
using Coop.Naval.CampaignBehaviors.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

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

    [HarmonyPatch(nameof(NavalTransitionCampaignBehavior.repair_ships_consequence))]
    [HarmonyPrefix]
    public static bool RepairShipsConsequencePrefix(NavalTransitionCampaignBehavior __instance, MenuCallbackArgs args)
    {
        MessageBroker.Instance.Publish(__instance, new RequestRepairShipsAtPort(MobileParty.MainParty, Settlement.CurrentSettlement));

        return false;
    }
}
