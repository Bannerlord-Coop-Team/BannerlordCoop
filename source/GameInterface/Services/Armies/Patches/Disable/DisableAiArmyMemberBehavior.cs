using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;

namespace GameInterface.Services.Armies.Patches.Disable;

[HarmonyPatch(typeof(AiArmyMemberBehavior))]
internal class DisableAiArmyMemberBehavior
{
    [HarmonyPatch(nameof(AiArmyMemberBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
