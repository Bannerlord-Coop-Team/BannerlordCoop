using Common.Messaging;
using GameInterface.Services.Armies.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies.Patches;

/// <summary>
/// Patch that sends the removal of the party to the server.
/// </summary>
[HarmonyPatch]
internal class PlayerArmyWaitBehaviorPatches
{
    [HarmonyPatch(typeof(PlayerArmyWaitBehavior), nameof(PlayerArmyWaitBehavior.wait_menu_army_leave_on_consequence))]
    [HarmonyPrefix]
    private static bool WaitMenuLeavePrefix(PlayerArmyWaitBehavior __instance, MenuCallbackArgs args)
    {
        if (PlayerEncounter.Current != null)
        {
            PlayerEncounter.Finish(true);
        }
        else
        {
            GameMenu.ExitToLast();
        }
        if (Settlement.CurrentSettlement != null)
        {
            LeaveSettlementAction.ApplyForParty(MobileParty.MainParty);
            PartyBase.MainParty.SetVisualAsDirty();
        }
        var message = new MobilePartyInArmyRemoved(MobileParty.MainParty.Army, MobileParty.MainParty, MobileParty.MainParty);
        MessageBroker.Instance.Publish(__instance, message);
        return false;
    }
}