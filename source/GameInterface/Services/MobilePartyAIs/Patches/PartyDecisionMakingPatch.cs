using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;

namespace GameInterface.Services.MobilePartyAIs.Patches;

[HarmonyPatch]
internal class AiEngagePartyBehaviorPatches
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(AiEngagePartyBehavior), nameof(AiEngagePartyBehavior.RegisterEvents)),
        AccessTools.Method(typeof(AiMilitaryBehavior), nameof(AiMilitaryBehavior.RegisterEvents)),
        AccessTools.Method(typeof(AiPartyThinkBehavior), nameof(AiPartyThinkBehavior.RegisterEvents)),
        AccessTools.Method(typeof(AiVisitSettlementBehavior), nameof(AiVisitSettlementBehavior.RegisterEvents))
    };

    static bool Prefix() => ModInformation.IsServer;
}
