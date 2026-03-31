using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Battles.Patches.Disable;

[HarmonyPatch(typeof(CampaignWarManagerBehavior))]
internal class DisableCampaignWarManagerBehavior
{
    [HarmonyPatch(nameof(CampaignWarManagerBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
