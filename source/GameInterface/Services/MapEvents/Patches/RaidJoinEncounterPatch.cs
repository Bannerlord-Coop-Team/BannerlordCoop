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
    private const string HelpAttackersConsequence = nameof(EncounterGameMenuBehavior.game_menu_join_encounter_help_attackers_on_consequence);
    private const string HelpDefendersConsequence = nameof(EncounterGameMenuBehavior.game_menu_join_encounter_help_defenders_on_consequence);

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), HelpAttackersConsequence);
        yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), HelpDefendersConsequence);
    }

    [HarmonyPrefix]
    private static bool Prefix(MethodBase __originalMethod)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        var mapEvent = PlayerEncounter.Current.GetEncounteredBattleOrNull();
        if (!mapEvent.IsVillageHostileAction())
            return true;

        if (__originalMethod.Name == HelpDefendersConsequence)
        {
            // Encounter menu init reads the joiner's side, so MapEventSideDataHandler opens it once the server's add lands.
            PlayerEncounter.JoinBattle(BattleSideEnum.Defender);
            return false;
        }

        if (!mapEvent.IsRaidHostileAction())
            return true;

        if (mapEvent.IsActiveSlowVillageRaid())
        {
            GameMenu.SwitchToMenu("raid_occupied");
            return false;
        }

        PlayerEncounter.JoinBattle(BattleSideEnum.Attacker);
        SwitchToJoinedEncounterMenu(mapEvent);
        return false;
    }

    private static void SwitchToJoinedEncounterMenu(MapEvent mapEvent)
    {
        if (mapEvent.DefenderSide.TroopCount > 0)
        {
            GameMenu.SwitchToMenu("encounter");
            return;
        }

        GameMenu.SwitchToMenu("raiding_village");
        MobileParty.MainParty.SetMoveModeHold();
    }
}