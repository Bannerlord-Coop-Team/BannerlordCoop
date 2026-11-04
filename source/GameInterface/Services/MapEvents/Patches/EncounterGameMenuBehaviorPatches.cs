using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(EncounterGameMenuBehavior))]
internal class EncounterGameMenuBehaviorPatches
{
    [HarmonyPatch(nameof(EncounterGameMenuBehavior.UpdateVillageHostileActionEncounter))]
    [HarmonyPrefix]
    private static bool UpdateVillageHostileActionEncounterPrefix()
    {
        var battle = PlayerEncounter.Battle;
        // The owner waits for authoritative surrender instead of detaching defenders from its replica.
        return ModInformation.IsServer || !battle.IsRaidHostileAction() || battle.DefenderSide.TroopCount == 0;
    }

    [HarmonyPatch(nameof(EncounterGameMenuBehavior.game_menu_village_raid_no_resist_on_consequence))]
    [HarmonyPrefix]
    private static bool RaidNoResistanceConsequencePrefix()
    {
        if (ModInformation.IsServer) return true;

        if (PlayerEncounter.Current != null)
        {
            if (PlayerEncounter.InsideSettlement)
                PlayerEncounter.LeaveSettlement();
            GameMenu.ActivateGameMenu("raiding_village");
        }

        return false;
    }

    // Runs before SiegeEntryFlowPatches routes the siege break so the server sacrifices while the party is still in its map event.
    [HarmonyPatch(nameof(EncounterGameMenuBehavior.game_menu_encounter_leave_your_soldiers_behind_accept_on_consequence))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void PublishPlayerLeftSoldiersBehindPrefix(EncounterGameMenuBehavior __instance)
    {
        if (ModInformation.IsServer) return;

        MessageBroker.Instance.Publish(__instance, new PlayerLeftSoldiersBehind(Hero.MainHero, MobileParty.MainParty));
    }

    [HarmonyPatch(nameof(EncounterGameMenuBehavior.game_menu_encounter_leave_your_soldiers_behind_accept_on_consequence))]
    [HarmonyPrefix]
    public static bool GameMenuEncounterLeaveYourSoldiersBehindAcceptOnConsequencePrefix()
    {
        if (ModInformation.IsServer) return false;

        if (Campaign.Current.CurrentMenuContext != null)
        {
            GameMenu.SwitchToMenu("try_to_get_away_debrief");
            return false;
        }
        GameMenu.ActivateGameMenu("try_to_get_away_debrief");

        return false;
    }
}
