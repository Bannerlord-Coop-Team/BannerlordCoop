using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(AiMilitaryBehavior))]
internal class DisableAiMilitaryBehavior
{
    [HarmonyPatch(nameof(AiMilitaryBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
