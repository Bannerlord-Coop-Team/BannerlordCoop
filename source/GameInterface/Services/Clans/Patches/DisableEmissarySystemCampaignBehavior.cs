using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(EmissarySystemCampaignBehavior))]
internal class DisableEmissarySystemCampaignBehavior
{
    [HarmonyPatch(nameof(EmissarySystemCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
