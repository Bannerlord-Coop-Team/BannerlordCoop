using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch(typeof(InfluenceGainCampaignBehavior))]
internal class DisableInfluenceGainCampaignBehavior
{
    [HarmonyPatch(nameof(InfluenceGainCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
