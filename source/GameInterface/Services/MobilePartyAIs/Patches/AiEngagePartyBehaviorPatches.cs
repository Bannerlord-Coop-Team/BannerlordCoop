using Common;
using GameInterface.Services.MapEvents.Patches;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;

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

/// <summary>
/// Drops hourly engage targets inside a player battle closed to AI joins.
/// Vanilla scores as GoAroundParty and only switches to EngageParty near the target.
/// </summary>
[HarmonyPatch(typeof(AiEngagePartyBehavior))]
internal class SkipClosedPlayerBattleEngagePatch
{
    [HarmonyPatch(nameof(AiEngagePartyBehavior.AiHourlyTick))]
    [HarmonyPostfix]
    public static void AiHourlyTickPostfix(PartyThinkParams p)
    {
        p._aiBehaviorScores.RemoveAll(score =>
            score.Item1.AiBehavior == AiBehavior.GoAroundParty &&
            score.Item1.Party is MobileParty target &&
            InteractionPatches.IsClosedToAiJoins(target.MapEvent));
    }
}
