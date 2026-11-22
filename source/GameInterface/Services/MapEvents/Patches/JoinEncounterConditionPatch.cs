using Common;
using GameInterface.Services.MapEvents.Extensions;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Localization;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Decides the help attackers and defenders join options on a client while the battle is not ready or is an active slow raid.
/// </summary>
[HarmonyPatch]
internal class JoinEncounterConditionPatch
{
    private const string HelpAttackersCondition = nameof(EncounterGameMenuBehavior.game_menu_join_encounter_help_attackers_on_condition);
    private const string HelpDefendersCondition = nameof(EncounterGameMenuBehavior.game_menu_join_encounter_help_defenders_on_condition);

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), HelpAttackersCondition);
        yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), HelpDefendersCondition);
    }

    // Refuses the join option while vanilla's condition would throw, before the server sets the battle up on this
    // client or after the encounter closes.
    [HarmonyPrefix]
    private static bool Prefix(MenuCallbackArgs args, ref bool __result)
    {
        if (ModInformation.IsServer) return true;

        var battle = PlayerEncounter.Current.GetEncounteredBattleOrNull();
        if (HasLeaderParties(battle)) return true;

        __result = false;
        args.IsEnabled = false;
        args.Tooltip = BattleModeEncounterOptionsPatch.BattleNotReadyTooltip;
        return false;
    }

    // Vanilla reads the leader of the side not being joined, and a client's sides and leaders arrive after the battle.
    private static bool HasLeaderParties(MapEvent battle) =>
        battle?.AttackerSide?.LeaderParty != null && battle.DefenderSide?.LeaderParty != null;

    // Refuses helping the attackers of an active slow village raid; it also runs after the prefix refused.
    [HarmonyPostfix]
    private static void Postfix(MethodBase __originalMethod, MenuCallbackArgs args, ref bool __result)
    {
        if (__originalMethod.Name != HelpAttackersCondition) return;
        if (!PlayerEncounter.Current.GetEncounteredBattleOrNull().IsActiveSlowVillageRaid()) return;

        __result = false;
        args.IsEnabled = false;
        args.Tooltip = TextObject.GetEmpty();
    }
}
