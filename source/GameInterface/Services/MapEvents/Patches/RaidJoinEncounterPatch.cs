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
using TaleWorlds.Localization;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch]
internal class RaidJoinEncounterConditionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_join_encounter_help_attackers_on_condition");
        yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_join_encounter_help_defenders_on_condition");
    }

    // Greys the join option while vanilla's condition would throw, before the server sets the battle up on this
    // client or after the encounter closes.
    [HarmonyPrefix]
    private static bool Prefix(MenuCallbackArgs args, ref bool __result)
    {
        if (ModInformation.IsServer) return true;
        if (HasLeaderParties(PlayerEncounterExtensions.GetEncounteredBattleOrNull())) return true;

        __result = false;
        if (args != null)
        {
            args.IsEnabled = false;
            args.Tooltip = BattleModeEncounterOptionsPatch.BattleNotReadyTooltip;
        }
        return false;
    }

    // Vanilla reads the leader of the side not being joined, and a client's sides and leaders arrive after the battle.
    private static bool HasLeaderParties(MapEvent battle) =>
        battle?.AttackerSide?.LeaderParty != null && battle.DefenderSide?.LeaderParty != null;

    [HarmonyPostfix]
    private static void Postfix(MethodBase __originalMethod, MenuCallbackArgs args, ref bool __result)
    {
        var mapEvent = PlayerEncounterExtensions.GetEncounteredBattleOrNull();
        if (mapEvent == null)
            return;

        if (!mapEvent.IsActiveSlowVillageRaid())
            return;

        if (!RaidJoinEncounterPatch.TryGetJoinSide(__originalMethod, out var side) ||
            side == BattleSideEnum.Defender)
        {
            return;
        }

        __result = false;
        args.IsEnabled = false;
        args.Tooltip = TextObject.GetEmpty();
    }
}

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

        var mapEvent = PlayerEncounter.EncounteredBattle;
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

    internal static bool TryGetJoinSide(MethodBase method, out BattleSideEnum side)
    {
        if (method?.Name == "game_menu_join_encounter_help_attackers_on_consequence" ||
            method?.Name == "game_menu_join_encounter_help_attackers_on_condition")
        {
            side = BattleSideEnum.Attacker;
            return true;
        }

        if (method?.Name == "game_menu_join_encounter_help_defenders_on_consequence" ||
            method?.Name == "game_menu_join_encounter_help_defenders_on_condition")
        {
            side = BattleSideEnum.Defender;
            return true;
        }

        side = BattleSideEnum.None;
        return false;
    }
}