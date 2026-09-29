using Common;
using Common.Messaging;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.SiegeEvents.Patches;

[HarmonyPatch]
internal class SiegeBreakOutPatches
{
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), "break_out_menu_accept_on_consequence")]
    [HarmonyPrefix]
    private static bool AcceptPrefix(EncounterGameMenuBehavior __instance)
    {
        if (ModInformation.IsServer || __instance._isBreakingOutFromPort) return true;
        MessageBroker.Instance.Publish(null, new BreakOutAttempted(MobileParty.MainParty));
        return false;
    }

    [HarmonyPatch(typeof(EncounterGameMenuBehavior), "break_out_debrief_continue_on_consequence")]
    [HarmonyPrefix]
    private static bool ContinuePrefix(EncounterGameMenuBehavior __instance)
    {
        if (ModInformation.IsServer || __instance._isBreakingOutFromPort) return true;
        if (ContainerProvider.TryResolve<ISiegeBreakOut>(out var breakOut)) breakOut.Continue();
        return false;
    }

    [HarmonyPatch(typeof(EncounterGameMenuBehavior), "break_in_out_debrief_menu_on_init")]
    [HarmonyPostfix]
    private static void DebriefPostfix(EncounterGameMenuBehavior __instance, MenuCallbackArgs args)
    {
        if (__instance._isBreakingOutFromPort || args.MenuContext.GameMenu.StringId != "break_out_debrief_menu") return;
        var text = new TextObject("{=coop_break_out_debrief}You slip out through the gates and break through the besiegers. You lose {CASUALTIES} on the way out.");
        text.SetTextVariable("CASUALTIES", PartyBaseHelper.PrintRegularTroopCategories(__instance._breakInOutCasualties));
        if (__instance._breakInOutArmyCasualties > 0)
        {
            var armyText = new TextObject("{=coop_break_out_army_losses} Other parties in your army lose {COUNT} troops.");
            armyText.SetTextVariable("COUNT", __instance._breakInOutArmyCasualties);
            text = new TextObject(text.ToString() + armyText.ToString());
        }
        args.MenuContext.GameMenu.GetText().SetTextVariable("BREAK_IN_DEBRIEF", text);
    }
}
