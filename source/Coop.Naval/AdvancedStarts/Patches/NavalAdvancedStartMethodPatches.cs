using GameInterface.Services.AdvancedStart.Patches;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using System.Collections.Generic;
using System.Reflection;

namespace Coop.Naval.AdvancedStarts.Patches;

/// <summary>
/// Gives War Sails' starts and personal ship the same shared-world rewrite as vanilla's starts: a seed mixed with the
/// joining hero, so players with the same start don't all get the same castle, port town or ship, and a merchant
/// venturer workshop another player doesn't own.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class NavalAdvancedStartMethodPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var type = typeof(NavalAdvancedStartingPlayerOptionsCampaignBehavior);
        yield return AccessTools.Method(type, nameof(NavalAdvancedStartingPlayerOptionsCampaignBehavior.StartGameAsFleetAdmiral));
        yield return AccessTools.Method(type, nameof(NavalAdvancedStartingPlayerOptionsCampaignBehavior.StartGameAsMerchantVenturer));
        yield return AccessTools.Method(type, nameof(NavalAdvancedStartingPlayerOptionsCampaignBehavior.GivePersonalShip));
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        AdvancedStartMethodPatches.Transpiler(instructions);
}
