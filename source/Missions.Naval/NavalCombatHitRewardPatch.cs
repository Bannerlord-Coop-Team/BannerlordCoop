using HarmonyLib;
using NavalDLC.CharacterDevelopment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

// The server applies every client's hit rewards (HitRewardHandler) and runs no mission, so vanilla's
// Mission.Current.IsNavalBattle check throws there before any hit XP lands; ask the attacker hero's map event instead.
[HarmonyPatch(typeof(NavalSkillLevellingManager), nameof(NavalSkillLevellingManager.OnCombatHit))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalCombatHitRewardPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var current = AccessTools.PropertyGetter(typeof(Mission), nameof(Mission.Current));
        var isNavalBattle = AccessTools.PropertyGetter(typeof(Mission), nameof(Mission.IsNavalBattle));
        var result = instructions.ToList();
        int replaced = 0;
        for (int index = 0; index + 1 < result.Count; index++)
        {
            if (!result[index].Calls(current) || !result[index + 1].Calls(isNavalBattle)) continue;

            // Labels stay on the first instruction; the stack still ends in one bool.
            result[index].opcode = OpCodes.Ldarg_1;
            result[index].operand = null;
            result[index + 1].opcode = OpCodes.Call;
            result[index + 1].operand = AccessTools.DeclaredMethod(typeof(NavalCombatHitRewardPatch), nameof(IsNavalHit),
                new[] { typeof(CharacterObject) });
            replaced++;
        }

        if (replaced != 1) throw new InvalidOperationException("Naval combat hit check changed.");
        return result;
    }

    internal static bool IsNavalHit(CharacterObject affectorCharacter) =>
        IsNavalHit(Mission.Current?.IsNavalBattle, affectorCharacter?.HeroObject?.PartyBelongedTo?.MapEvent?.IsNavalMapEvent);

    /// <summary>A local mission answers as vanilla does; without one, the attacker hero's party's map event does.</summary>
    internal static bool IsNavalHit(bool? missionIsNaval, bool? affectorMapEventIsNaval) =>
        missionIsNaval ?? affectorMapEventIsNaval == true;
}
