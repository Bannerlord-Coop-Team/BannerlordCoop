using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(AiPartyThinkBehavior))]
internal class DisableAiPartyThinkBehavior
{
    [HarmonyPatch(nameof(AiPartyThinkBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
