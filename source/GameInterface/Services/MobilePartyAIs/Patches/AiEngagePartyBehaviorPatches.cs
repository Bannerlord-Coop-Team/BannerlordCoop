using Common;
using GameInterface.Services.MapEvents.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;

/// <summary>
/// Runs <see cref="AiEngagePartyBehavior"/> on the server only and keeps it out of player battles closed to AI joins.
/// </summary>
[HarmonyPatch(typeof(AiEngagePartyBehavior))]
internal class AiEngagePartyBehaviorPatches
{
    // Registers the listeners on the server only, which decides for every AI party.
    [HarmonyPatch(nameof(AiEngagePartyBehavior.RegisterEvents))]
    [HarmonyPrefix]
    private static bool RegisterEventsPrefix() => ModInformation.IsServer;

    // Drops hourly engage targets inside a player battle closed to AI joins.
    // Vanilla scores as GoAroundParty and only switches to EngageParty near the target.
    [HarmonyPatch(nameof(AiEngagePartyBehavior.AiHourlyTick))]
    [HarmonyPostfix]
    private static void AiHourlyTickPostfix(PartyThinkParams p)
    {
        p._aiBehaviorScores.RemoveAll(score =>
            score.Item1.AiBehavior == AiBehavior.GoAroundParty &&
            score.Item1.Party is MobileParty target &&
            InteractionPatches.IsClosedToAiJoins(target.MapEvent));
    }
}
