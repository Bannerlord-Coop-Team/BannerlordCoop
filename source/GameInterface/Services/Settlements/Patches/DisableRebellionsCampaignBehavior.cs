using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Settlements.Patches;


[HarmonyPatch(typeof(RebellionsCampaignBehavior))]
internal class DisableRebellionsCampaignBehavior
{
    [HarmonyPatch(nameof(RebellionsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
