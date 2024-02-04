using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(NotablesCampaignBehavior))]
internal class DisableNotablesCampaignBehavior
{
    [HarmonyPatch(nameof(NotablesCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
