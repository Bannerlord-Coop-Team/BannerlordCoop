using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(EncounterGameMenuBehavior))]
internal class EncounterGameMenuBehaviorPatches
{
    [HarmonyPatch(nameof(EncounterGameMenuBehavior.game_menu_encounter_leave_your_soldiers_behind_accept_on_consequence))]
    [HarmonyPrefix]
    public static bool GameMenuEncounterLeaveYourSoldiersBehindAcceptOnConsequencePrefix(EncounterGameMenuBehavior __instance)
    {
        if (ModInformation.IsServer) return false;

        MessageBroker.Instance.Publish(__instance, new PlayerLeftSoldiersBehind(Hero.MainHero, MobileParty.MainParty));

        if (Campaign.Current.CurrentMenuContext != null)
        {
            GameMenu.SwitchToMenu("try_to_get_away_debrief");
            return false;
        }
        GameMenu.ActivateGameMenu("try_to_get_away_debrief");

        return false;
    }
}
