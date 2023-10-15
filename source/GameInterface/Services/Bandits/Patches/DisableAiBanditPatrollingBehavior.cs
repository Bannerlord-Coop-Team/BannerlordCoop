using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;

namespace GameInterface.Services.Bandits.Patches;

[HarmonyPatch(typeof(AiBanditPatrollingBehavior))]
internal class DisableAiBanditPatrollingBehavior
{
    [HarmonyPatch(nameof(AiBanditPatrollingBehavior.RegisterEvents))]
    static bool Prefix() => true;
}
