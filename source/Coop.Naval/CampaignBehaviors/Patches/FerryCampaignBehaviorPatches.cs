using Common.Messaging;
using GameInterface.Services.Ferries.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Naval.CampaignBehaviors.Patches;

/// <summary>
/// The ferry's fare, fleet and landing are applied on the server, and the client boards once the server lets its party leave.
/// </summary>
[HarmonyPatch(typeof(FerryCampaignBehavior))]
[HarmonyPatchCategory(GameInterface.GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class FerryCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(FerryCampaignBehavior.game_menu_ferry_target_on_consequence))]
    [HarmonyPrefix]
    public static bool GameMenuFerryTargetOnConsequencePrefix()
    {
        var party = MobileParty.MainParty;
        MessageBroker.Instance.Publish(party, new EndSettlementEncounterAttempted(party, SettlementLeaveType.Ferry));

        return false;
    }

    [HarmonyPatch(nameof(FerryCampaignBehavior.player_ferry_state_end_continue_on_consequence))]
    [HarmonyPrefix]
    public static bool PlayerFerryStateEndContinueOnConsequencePrefix()
    {
        GameMenu.ExitToLast();
        Campaign.Current.PlayerDataForNavalAutoTravel = null;

        var party = MobileParty.MainParty;
        MessageBroker.Instance.Publish(party, new PlayerFerryEndAttempted(party));

        return false;
    }
}
