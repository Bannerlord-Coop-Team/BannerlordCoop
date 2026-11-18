using Common;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Extensions;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch]
internal class RaidJoinEncounterPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_join_encounter_help_attackers_on_consequence");
        yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_join_encounter_help_defenders_on_consequence");
    }

    [HarmonyPrefix]
    private static bool Prefix(MethodBase __originalMethod)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        var mapEvent = PlayerEncounter.Current.GetEncounteredBattleOrNull();
        var mainParty = MobileParty.MainParty?.Party;
        if (!mapEvent.IsRaidHostileAction() || mainParty == null)
            return true;

        if (!TryGetJoinSide(__originalMethod, out var side))
            return true;

        if (mapEvent.IsActiveSlowVillageRaid() && side == BattleSideEnum.Attacker)
        {
            GameMenu.SwitchToMenu("raid_occupied");
            return false;
        }

        PlayerEncounter.JoinBattle(side);
        SwitchToJoinedEncounterMenu(mapEvent, side);
        return false;
    }

    private static void SwitchToJoinedEncounterMenu(MapEvent mapEvent, BattleSideEnum side)
    {
        if (side == BattleSideEnum.Defender)
        {
            GameMenu.ActivateGameMenu("encounter");
            return;
        }

        if (mapEvent?.DefenderSide?.TroopCount > 0 && !mapEvent.IsActiveSlowVillageRaid())
        {
            GameMenu.SwitchToMenu("encounter");
            return;
        }

        if (mapEvent.IsRaidHostileAction())
        {
            GameMenu.SwitchToMenu("raiding_village");
            MobileParty.MainParty.SetMoveModeHold();
            return;
        }

        GameMenu.SwitchToMenu("encounter");
    }

    private static bool TryGetJoinSide(MethodBase method, out BattleSideEnum side)
    {
        if (method?.Name == "game_menu_join_encounter_help_attackers_on_consequence")
        {
            side = BattleSideEnum.Attacker;
            return true;
        }

        if (method?.Name == "game_menu_join_encounter_help_defenders_on_consequence")
        {
            side = BattleSideEnum.Defender;
            return true;
        }

        side = BattleSideEnum.None;
        return false;
    }
}