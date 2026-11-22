using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>Restores the movement destination for a pursuing party during map updates.</summary>
[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.CheckAiForMapChangeAndUpdateIfNeeded))]
internal static class MobilePartyMapUpdatePatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var behaviorParty = AccessTools.PropertyGetter(typeof(MobilePartyAi), nameof(MobilePartyAi.AiBehaviorPartyBase));
        var resolveTarget = AccessTools.Method(typeof(MobilePartyMapUpdatePatch), nameof(ResolveGoAroundPartyTarget));
        return instructions.MethodReplacer(behaviorParty, resolveTarget);
    }

    private static PartyBase ResolveGoAroundPartyTarget(MobilePartyAi ai)
    {
        // SetMoveGoAroundParty saves TargetParty even when short-term AI has no target.
        return ai.AiBehaviorPartyBase ?? ai._mobileParty.TargetParty?.Party;
    }
}
