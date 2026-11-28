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

/// <summary>
/// Joins a client to a village raid, force supplies or force volunteers event through the server's party add.
/// </summary>
[HarmonyPatch]
internal class VillageHostileActionJoinEncounterPatch
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

        var side = __originalMethod.Name == HelpAttackersConsequence ? BattleSideEnum.Attacker : BattleSideEnum.Defender;
        if (side == BattleSideEnum.Attacker && mapEvent.IsRaidHostileAction())
        {
            if (mapEvent.IsActiveSlowVillageRaid())
            {
                GameMenu.SwitchToMenu("raid_occupied");
                return false;
            }

            PlayerEncounter.JoinBattle(BattleSideEnum.Attacker);
            SwitchToJoinedEncounterMenu(mapEvent);
            return false;
        }

        // Vanilla sends an undefended attacker that doesn't lead an army to army_wait or menu_siege_strategies, which never read its side.
        var vanillaSkipsEncounter = side == BattleSideEnum.Attacker &&
            mapEvent.DefenderSide.TroopCount == 0 && MobileParty.MainParty.Army?.LeaderParty != MobileParty.MainParty;
        if (vanillaSkipsEncounter)
            return true;

        // Encounter menu init reads the joiner's side, so MapEventSideDataHandler opens it once the server's add lands.
        PlayerEncounter.JoinBattle(side);
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