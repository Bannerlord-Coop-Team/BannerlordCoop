using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Settlements.Patches.Disable;


[HarmonyPatch(typeof(RebellionsCampaignBehavior))]
internal class RebellionsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(RebellionsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
