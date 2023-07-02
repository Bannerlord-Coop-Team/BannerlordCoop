using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;

namespace GameInterface.Services.Armies.Patches;

[HarmonyPatch(typeof(AiArmyMemberBehavior))]
internal class DisableAiArmyMemberBehavior
{
    [HarmonyPatch(nameof(AiArmyMemberBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
