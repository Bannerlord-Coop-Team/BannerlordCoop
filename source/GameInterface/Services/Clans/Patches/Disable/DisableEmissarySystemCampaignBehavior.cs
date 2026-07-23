using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Clans.Patches.Disable;

[HarmonyPatch(typeof(EmissarySystemCampaignBehavior))]
internal class DisableEmissarySystemCampaignBehavior
{
    [HarmonyPatch(nameof(EmissarySystemCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
