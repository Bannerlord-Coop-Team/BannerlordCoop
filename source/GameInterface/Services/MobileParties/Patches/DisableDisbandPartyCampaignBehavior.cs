using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(DisbandPartyCampaignBehavior))]
internal class DisableDisbandPartyCampaignBehavior
{
    [HarmonyPatch(nameof(DisbandPartyCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
