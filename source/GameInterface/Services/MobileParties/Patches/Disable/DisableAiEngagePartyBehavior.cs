using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(AiEngagePartyBehavior))]
internal class DisableAiEngagePartyBehavior
{
    [HarmonyPatch(nameof(AiEngagePartyBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
